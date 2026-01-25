"""Authentication service for token share users."""
import base64
import io
import uuid
from datetime import timedelta

import pyotp
import qrcode
from fastapi import HTTPException, status
from sqlalchemy.orm import Session

from app.core import security
from app.core.config import get_settings
from app.core.mfa import verify_otp
from app.core.time import utcnow
from app.models.token_user_login_challenge import TokenUserLoginChallenge
from app.models.token_user import TokenUser, TokenUserAssignment


def _cleanup_old_token_user_challenges(db: Session, token_user_id: str) -> None:
    """Remove expired challenges and any existing ones for this token user."""
    now = utcnow()
    db.query(TokenUserLoginChallenge).filter(
        (TokenUserLoginChallenge.expires_at_utc < now) | (TokenUserLoginChallenge.token_user_id == token_user_id)
    ).delete(synchronize_session=False)
    db.commit()


def create_token_user_login_challenge(
    db: Session, email: str, password: str
) -> tuple[str, str | None, list[dict] | None]:
    """
    Create login challenge for token share user.
    Returns (challenge_id, temp_mfa_secret, tokens_list).
    - temp_mfa_secret is only returned if user hasn't set up MFA yet.
    - tokens_list contains all tokens this email has access to (if multiple)
    """
    # Find user by email
    user = (
        db.query(TokenUser)
        .filter(TokenUser.email == email)
        .first()
    )
    
    if not user:
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED, 
            detail="Invalid credentials"
        )
    
    # Verify password
    if not user.password_hash:
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED, 
            detail="Password not set for this user. Contact administrator."
        )
    
    if not security.verify_password(password, user.password_hash):
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED, 
            detail="Invalid credentials"
        )

    # Get all token assignments for this user
    assignments = (
        db.query(TokenUserAssignment)
        .filter(TokenUserAssignment.user_id == user.id)
        .all()
    )
    
    if not assignments:
        raise HTTPException(
            status_code=status.HTTP_403_FORBIDDEN,
            detail="User has no token assignments"
        )

    settings = get_settings()
    temp_secret = None if user.mfa_secret else pyotp.random_base32()
    challenge_id = str(uuid.uuid4())

    # Store challenge
    _cleanup_old_token_user_challenges(db, user.id)

    challenge = TokenUserLoginChallenge(
        id=challenge_id,
        token_user_id=user.id,
        expires_at_utc=utcnow() + timedelta(minutes=settings.auth_challenge_minutes),
        temp_mfa_secret=temp_secret,
    )
    db.add(challenge)
    db.commit()
    
    # If user has access to multiple tokens, return the list
    tokens_list = None
    if len(assignments) > 1:
        from app.models.token_deployment import TokenDeployment
        tokens_list = []
        for assignment in assignments:
            token = db.query(TokenDeployment).filter(TokenDeployment.id == assignment.token_deployment_id).first()
            if token:
                tokens_list.append({
                    "token_id": token.id,
                    "token_name": token.token_name,
                    "contract_address": token.contract_address
                })
    
    return challenge_id, temp_secret, tokens_list


def build_otpauth_and_qr(email: str, secret: str) -> tuple[str, str]:
    """
    Build OTP auth URL and QR code image (base64-encoded).
    Used for initial MFA setup.
    """
    settings = get_settings()
    issuer = "Aegis Mint - Share Portal"
    otpauth_url = pyotp.TOTP(secret).provisioning_uri(name=email, issuer_name=issuer)
    
    # Generate QR code
    qr = qrcode.QRCode(version=1, box_size=10, border=4)
    qr.add_data(otpauth_url)
    qr.make(fit=True)
    img = qr.make_image(fill_color="black", back_color="white")
    
    # Convert to base64
    buffer = io.BytesIO()
    img.save(buffer, format="PNG")
    qr_base64 = base64.b64encode(buffer.getvalue()).decode()
    
    return otpauth_url, qr_base64


def verify_token_user_otp_and_issue_tokens(db: Session, challenge_id: str, otp: str, selected_token_id: str | None = None):
    """
    Verify OTP and issue JWT tokens for token share user.
    If temp_mfa_secret was provided, saves it permanently.
    If selected_token_id is provided, uses that specific token for the session.
    """
    challenge = (
        db.query(TokenUserLoginChallenge)
        .filter(TokenUserLoginChallenge.id == challenge_id)
        .first()
    )
    if not challenge:
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED, 
            detail="Challenge expired or invalid"
        )
    
    exp = challenge.expires_at_utc
    now = utcnow()
    if exp and exp.tzinfo is None:
        exp = exp.replace(tzinfo=now.tzinfo)
    if exp < now:
        db.delete(challenge)
        db.commit()
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED, 
            detail="Challenge expired"
        )

    # Get the user
    user = (
        db.query(TokenUser)
        .filter(TokenUser.id == challenge.token_user_id)
        .first()
    )
    if not user:
        db.delete(challenge)
        db.commit()
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED, 
            detail="User not found"
        )
    
    # If selected_token_id is provided, verify user has access to it
    if selected_token_id:
        assignment = (
            db.query(TokenUserAssignment)
            .filter(
                TokenUserAssignment.user_id == user.id,
                TokenUserAssignment.token_deployment_id == selected_token_id
            )
            .first()
        )
        if not assignment:
            raise HTTPException(
                status_code=status.HTTP_400_BAD_REQUEST,
                detail="Selected token is not valid for this user"
            )
        token_deployment_id = selected_token_id
    else:
        # No token selection - use the first assignment (single token case)
        assignment = (
            db.query(TokenUserAssignment)
            .filter(TokenUserAssignment.user_id == user.id)
            .first()
        )
        if not assignment:
            raise HTTPException(
                status_code=status.HTTP_403_FORBIDDEN,
                detail="User has no token assignments"
            )
        token_deployment_id = assignment.token_deployment_id

    secret = challenge.temp_mfa_secret or user.mfa_secret
    if not secret:
        db.delete(challenge)
        db.commit()
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED, 
            detail="MFA not initialized"
        )
    
    if not verify_otp(secret, otp):
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED, 
            detail="Invalid OTP"
        )

    # Save MFA secret if this is first-time setup
    if not user.mfa_secret:
        user.mfa_secret = secret
        user.mfa_enabled = True
        db.add(user)
        db.commit()
        db.refresh(user)

    # Create tokens with custom payload for token users
    # Using role "TokenShareUser" to distinguish from system users
    # Include token_deployment_id in the token payload
    access = security.create_access_token(user.id, "TokenShareUser", token_deployment_id=token_deployment_id)
    refresh = security.create_refresh_token(user.id, "TokenShareUser", token_deployment_id=token_deployment_id)

    db.delete(challenge)
    db.commit()

    return {
        "access_token": access,
        "refresh_token": refresh,
        "expires_at": utcnow() + timedelta(minutes=get_settings().access_token_exp_minutes),
        "user_id": user.id,
        "user_email": user.email,
        "user_name": user.name,
        "token_deployment_id": token_deployment_id,
    }


def refresh_token_user_access_token(db: Session, refresh_token: str):
    """Refresh access token for token share user using refresh token."""
    try:
        payload = security.decode_jwt(refresh_token)
        user_id = payload.get("sub")
        role = payload.get("role")
        token_deployment_id = payload.get("token_deployment_id")
        
        if not user_id or role != "TokenShareUser":
            raise HTTPException(
                status_code=status.HTTP_401_UNAUTHORIZED,
                detail="Invalid token"
            )
        
        user = (
            db.query(TokenUser)
            .filter(TokenUser.id == user_id)
            .first()
        )
        if not user:
            raise HTTPException(
                status_code=status.HTTP_401_UNAUTHORIZED,
                detail="User not found"
            )
        
        # Verify user still has access to the token
        if token_deployment_id:
            assignment = (
                db.query(TokenUserAssignment)
                .filter(
                    TokenUserAssignment.user_id == user.id,
                    TokenUserAssignment.token_deployment_id == token_deployment_id
                )
                .first()
            )
            if not assignment:
                raise HTTPException(
                    status_code=status.HTTP_403_FORBIDDEN,
                    detail="User no longer has access to this token"
                )
        
        # Issue new tokens
        access = security.create_access_token(user.id, "TokenShareUser", token_deployment_id=token_deployment_id)
        new_refresh = security.create_refresh_token(user.id, "TokenShareUser", token_deployment_id=token_deployment_id)
        
        return {
            "access_token": access,
            "refresh_token": new_refresh,
            "expires_at": utcnow() + timedelta(minutes=get_settings().access_token_exp_minutes),
            "user_id": user.id,
            "user_email": user.email,
            "user_name": user.name,
            "token_deployment_id": token_deployment_id,
        }
    except Exception as e:
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="Invalid or expired token"
        )


def change_token_user_password(
    db: Session, user_id: str, current_password: str, new_password: str
) -> None:
    """Change password for token share user."""
    user = (
        db.query(TokenUser)
        .filter(TokenUser.id == user_id)
        .first()
    )
    if not user:
        raise HTTPException(
            status_code=status.HTTP_404_NOT_FOUND,
            detail="User not found"
        )
    
    if not user.password_hash or not security.verify_password(current_password, user.password_hash):
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="Current password is incorrect"
        )
    
    user.password_hash = security.hash_password(new_password)
    db.add(user)
    db.commit()

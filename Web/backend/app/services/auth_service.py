import base64
import io
import uuid
from datetime import timedelta

import pyotp
import qrcode
from fastapi import Depends, HTTPException, status
from sqlalchemy.orm import Session

from app.core import security
from app.core.config import get_settings
from app.core.mfa import verify_otp
from app.core.time import utcnow
from app.models import User, UserRole
from app.api.deps import get_current_user

login_challenges: dict[str, dict] = {}


def create_login_challenge(db: Session, email: str, password: str) -> str:
    user = db.query(User).filter(User.email == email, User.is_active.is_(True)).first()
    if not user or not security.verify_password(password, user.password_hash):
        raise HTTPException(status_code=status.HTTP_401_UNAUTHORIZED, detail="Invalid credentials")

    temp_secret = None if user.mfa_secret else pyotp.random_base32()
    settings = get_settings()
    challenge_id = str(uuid.uuid4())
    login_challenges[challenge_id] = {
        "user_id": user.id,
        "expires_at": utcnow() + timedelta(minutes=settings.auth_challenge_minutes),
        **({"mfa_secret": temp_secret} if temp_secret else {}),
        "email": user.email,
    }
    return challenge_id


def verify_otp_and_issue_tokens(db: Session, challenge_id: str, otp: str):
    payload = login_challenges.get(challenge_id)
    if not payload:
        raise HTTPException(status_code=status.HTTP_401_UNAUTHORIZED, detail="Challenge expired or invalid")
    if payload["expires_at"] < utcnow():
        login_challenges.pop(challenge_id, None)
        raise HTTPException(status_code=status.HTTP_401_UNAUTHORIZED, detail="Challenge expired")

    user = db.query(User).filter(User.id == payload["user_id"], User.is_active.is_(True)).first()
    if not user:
        raise HTTPException(status_code=status.HTTP_401_UNAUTHORIZED, detail="User not found")

    secret = payload.get("mfa_secret") or user.mfa_secret
    if not secret:
        raise HTTPException(status_code=status.HTTP_401_UNAUTHORIZED, detail="MFA not initialized")
    if not verify_otp(secret, otp):
        raise HTTPException(status_code=status.HTTP_401_UNAUTHORIZED, detail="Invalid OTP")

    if not user.mfa_secret:
        user.mfa_secret = secret
        db.add(user)
        db.commit()
        db.refresh(user)

    access = security.create_access_token(user.id, user.role.value)
    refresh = security.create_refresh_token(user.id, user.role.value)
    login_challenges.pop(challenge_id, None)

    return {
        "access_token": access,
        "refresh_token": refresh,
        "expires_at": utcnow() + timedelta(minutes=get_settings().access_token_exp_minutes),
        "role": user.role,
        "user": user,
    }


def ensure_super_admin_exists(db: Session):
    """Seed a single SuperAdmin for dev environments if none exist."""
    existing_admin = db.query(User).filter(User.role == UserRole.SUPER_ADMIN).first()
    if existing_admin:
        # Refresh default password hash to current scheme
        existing_admin.password_hash = security.hash_password("ChangeMe123!")
        db.add(existing_admin)
        db.commit()
        db.refresh(existing_admin)
        return existing_admin

    settings = get_settings()
    user = User(
        email="admin@example.com",
        password_hash=security.hash_password("ChangeMe123!"),
        role=UserRole.SUPER_ADMIN,
        mfa_secret="JBSWY3DPEHPK3PXP",
        is_active=True,
    )
    db.add(user)
    db.commit()
    db.refresh(user)
    return user


def require_active_user(user: User = Depends(get_current_user)) -> User:
    return user


def change_password(db: Session, user: User, current_password: str, new_password: str):
    if not security.verify_password(current_password, user.password_hash):
        raise HTTPException(status_code=status.HTTP_400_BAD_REQUEST, detail="Current password invalid")
    user.password_hash = security.hash_password(new_password)
    db.add(user)
    db.commit()
    db.refresh(user)
    return user


def build_otpauth_and_qr(email: str, secret: str):
    settings = get_settings()
    otpauth = f"otpauth://totp/{settings.totp_issuer}:{email}?secret={secret}&issuer={settings.totp_issuer}"
    img = qrcode.make(otpauth)
    buf = io.BytesIO()
    img.save(buf, format="PNG")
    encoded = base64.b64encode(buf.getvalue()).decode("ascii")
    return otpauth, f"data:image/png;base64,{encoded}"

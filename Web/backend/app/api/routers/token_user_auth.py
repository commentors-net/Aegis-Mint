"""Authentication endpoints for token share users (external users accessing shares)."""
from fastapi import APIRouter, Depends
from sqlalchemy.orm import Session

from app.api.deps import get_db
from app.schemas.token_user_auth import (
    TokenUserLoginRequest,
    TokenUserLoginResponse,
    TokenUserVerifyOtpRequest,
    TokenUserVerifyOtpResponse,
    TokenUserRefreshTokenRequest,
    TokenUserChangePasswordRequest,
)
from app.services import token_user_auth_service

router = APIRouter(prefix="/api/token-user-auth", tags=["token-user-auth"])


@router.post("/login", response_model=TokenUserLoginResponse)
def token_user_login(body: TokenUserLoginRequest, db: Session = Depends(get_db)):
    """
    Token user login with email and password.
    Returns challenge_id for OTP verification.
    If MFA not set up, returns temp secret for QR code generation.
    If user has access to multiple tokens, returns tokens list for selection.
    """
    challenge_id, temp_secret, tokens_list = token_user_auth_service.create_token_user_login_challenge(
        db, body.email, body.password
    )
    otpauth_url = None
    mfa_qr_base64 = None
    if temp_secret:
        otpauth_url, mfa_qr_base64 = token_user_auth_service.build_otpauth_and_qr(
            body.email, temp_secret
        )
    return TokenUserLoginResponse(
        challenge_id=challenge_id,
        mfa_secret_base32=temp_secret,
        otpauth_url=otpauth_url,
        mfa_qr_base64=mfa_qr_base64,
        tokens=tokens_list,  # Will be None if user has access to only one token
    )


@router.post("/verify-otp", response_model=TokenUserVerifyOtpResponse)
def token_user_verify_otp(body: TokenUserVerifyOtpRequest, db: Session = Depends(get_db)):
    """
    Verify OTP code and issue access/refresh tokens.
    If temp_secret was provided, saves it to user's mfa_secret.
    If selected_token_id is provided, issues tokens for that specific token.
    """
    result = token_user_auth_service.verify_token_user_otp_and_issue_tokens(
        db, body.challenge_id, body.otp, body.selected_token_id
    )
    return TokenUserVerifyOtpResponse(
        access_token=result["access_token"],
        refresh_token=result["refresh_token"],
        expires_at=result["expires_at"],
        user_id=result["user_id"],
        user_email=result["user_email"],
        user_name=result["user_name"],
        token_deployment_id=result["token_deployment_id"],
    )


@router.post("/refresh", response_model=TokenUserVerifyOtpResponse)
def token_user_refresh_token(body: TokenUserRefreshTokenRequest, db: Session = Depends(get_db)):
    """Refresh access token using refresh token for token share user."""
    result = token_user_auth_service.refresh_token_user_access_token(db, body.refresh_token)
    return TokenUserVerifyOtpResponse(
        access_token=result["access_token"],
        refresh_token=result["refresh_token"],
        expires_at=result["expires_at"],
        user_id=result["user_id"],
        user_email=result["user_email"],
        user_name=result["user_name"],
        token_deployment_id=result["token_deployment_id"],
    )


@router.post("/change-password")
def token_user_change_password(
    body: TokenUserChangePasswordRequest,
    db: Session = Depends(get_db),
):
    """
    Change password for token share user.
    Requires current password and new password.
    Note: This endpoint needs to be called with valid access token.
    For now, it's open - you should add get_current_token_user dependency.
    """
    token_user_auth_service.change_token_user_password(
        db, body.user_id, body.current_password, body.new_password
    )
    return {"status": "ok"}

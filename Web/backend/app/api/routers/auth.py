from fastapi import APIRouter, Depends
from sqlalchemy.orm import Session

from app.api.deps import get_db, get_current_user
from app.schemas.auth import ChangePasswordRequest, LoginRequest, LoginResponse, VerifyOtpRequest, VerifyOtpResponse
from app.services import auth_service
from app.core.config import get_settings

router = APIRouter(prefix="/auth", tags=["auth"])


@router.post("/login", response_model=LoginResponse)
def login(body: LoginRequest, db: Session = Depends(get_db)):
    challenge_id, temp_secret = auth_service.create_login_challenge(db, body.email, body.password)
    otpauth_url = None
    mfa_qr_base64 = None
    if temp_secret:
        otpauth_url, mfa_qr_base64 = auth_service.build_otpauth_and_qr(body.email, temp_secret)
    return LoginResponse(
        challenge_id=challenge_id,
        mfa_secret_base32=temp_secret,
        otpauth_url=otpauth_url,
        mfa_qr_base64=mfa_qr_base64,
    )


@router.post("/verify-otp", response_model=VerifyOtpResponse)
def verify_otp(body: VerifyOtpRequest, db: Session = Depends(get_db)):
    result = auth_service.verify_otp_and_issue_tokens(db, body.challenge_id, body.otp)
    return VerifyOtpResponse(
        access_token=result["access_token"],
        refresh_token=result["refresh_token"],
        expires_at=result["expires_at"],
        role=result["role"],
        user=result["user"],
    )


@router.post("/change-password")
def change_password(
    body: ChangePasswordRequest,
    db: Session = Depends(get_db),
    user=Depends(get_current_user),
):
    auth_service.change_password(db, user, body.current_password, body.new_password)
    return {"status": "ok"}

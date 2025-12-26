from datetime import datetime
from typing import Optional

from pydantic import BaseModel, EmailStr, Field

from .admin import UserOut
from app.models.user import UserRole


class LoginRequest(BaseModel):
    email: EmailStr
    password: str


class LoginResponse(BaseModel):
    mfa_required: bool = True
    challenge_id: str
    mfa_secret_base32: str | None = None
    otpauth_url: str | None = None
    mfa_qr_base64: str | None = None


class VerifyOtpRequest(BaseModel):
    challenge_id: str
    otp: str = Field(..., min_length=6, max_length=6)


class TokenPair(BaseModel):
    access_token: str
    refresh_token: Optional[str] = None
    token_type: str = "bearer"
    expires_at: datetime


class VerifyOtpResponse(TokenPair):
    role: UserRole
    user: UserOut


class ChangePasswordRequest(BaseModel):
    current_password: str
    new_password: str = Field(..., min_length=8)

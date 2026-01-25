"""Pydantic schemas for token user authentication."""
from datetime import datetime
from typing import Optional

from pydantic import BaseModel, EmailStr, Field


class TokenUserLoginRequest(BaseModel):
    """Request body for token user login."""
    email: EmailStr
    password: str = Field(..., min_length=8)


class TokenUserLoginResponse(BaseModel):
    """Response from token user login (before OTP verification)."""
    challenge_id: str
    mfa_secret_base32: Optional[str] = None  # Only if MFA not set up yet
    otpauth_url: Optional[str] = None  # For QR code generation
    mfa_qr_base64: Optional[str] = None  # Base64-encoded QR code image
    tokens: Optional[list[dict]] = None  # List of tokens if user has access to multiple


class TokenUserVerifyOtpRequest(BaseModel):
    """Request body for OTP verification."""
    challenge_id: str
    otp: str = Field(..., min_length=6, max_length=6)
    selected_token_id: Optional[str] = None  # Required if user has access to multiple tokens


class TokenUserVerifyOtpResponse(BaseModel):
    """Response after successful OTP verification with tokens."""
    access_token: str
    refresh_token: str
    expires_at: datetime
    user_id: str
    user_email: str
    user_name: str
    token_deployment_id: str


class TokenUserRefreshTokenRequest(BaseModel):
    """Request body for token refresh."""
    refresh_token: str


class TokenUserChangePasswordRequest(BaseModel):
    """Request body for changing password."""
    user_id: str
    current_password: str = Field(..., min_length=8)
    new_password: str = Field(..., min_length=8)

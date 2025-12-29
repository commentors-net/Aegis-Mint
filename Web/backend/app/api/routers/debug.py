from datetime import timedelta

from fastapi import APIRouter, Depends, HTTPException, status
from pydantic import BaseModel, EmailStr
from sqlalchemy.orm import Session

from app.api.deps import get_db
from app.core import security
from app.core.config import get_settings
from app.core.mfa import verify_otp
from app.core.time import utcnow
from app.models import User
from app.schemas.auth import TokenPair

router = APIRouter(prefix="/debug", tags=["debug"])


class DebugLoginRequest(BaseModel):
    email: EmailStr
    password: str
    otp: str | None = None


@router.post("/fast-login", response_model=TokenPair)
def fast_login(body: DebugLoginRequest, db: Session = Depends(get_db)):
    """
    Local-only helper: single-call JWT issuance.
    Requires email/password; if the user already has MFA, an OTP is required.
    Enabled only when TOKENCONTROL_ENABLE_DOCS=true.
    """
    settings = get_settings()
    if not settings.enable_docs:
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail="Not available")

    user = db.query(User).filter(User.email == body.email, User.is_active.is_(True)).first()
    if not user or not security.verify_password(body.password, user.password_hash):
        raise HTTPException(status_code=status.HTTP_401_UNAUTHORIZED, detail="Invalid credentials")

    if user.mfa_secret:
        if not body.otp:
            raise HTTPException(status_code=status.HTTP_400_BAD_REQUEST, detail="OTP required")
        if not verify_otp(user.mfa_secret, body.otp):
            raise HTTPException(status_code=status.HTTP_401_UNAUTHORIZED, detail="Invalid OTP")

    access = security.create_access_token(user.id, user.role.value)
    refresh = security.create_refresh_token(user.id, user.role.value)
    return TokenPair(
        access_token=access,
        refresh_token=refresh,
        expires_at=utcnow() + timedelta(minutes=settings.access_token_exp_minutes),
    )

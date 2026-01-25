from datetime import datetime
from typing import Optional

from pydantic import BaseModel, EmailStr, Field

from app.models.user import UserRole


class UserBase(BaseModel):
    email: EmailStr
    role: UserRole
    phone: Optional[str] = Field(None, max_length=20)
    is_active: bool = True


class UserCreate(UserBase):
    password: str = Field(..., min_length=8)
    mfa_secret: str | None = Field(None, description="Base32 TOTP secret (optional; will be generated on first login)")


class UserUpdate(BaseModel):
    password: Optional[str] = Field(None, min_length=8)
    role: Optional[UserRole] = None
    phone: Optional[str] = Field(None, max_length=20)
    is_active: Optional[bool] = None
    mfa_secret: Optional[str] = None


class UserOut(UserBase):
    id: str
    created_at_utc: datetime

    model_config = {"from_attributes": True}

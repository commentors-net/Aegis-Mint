from datetime import datetime
from typing import Optional

from pydantic import BaseModel, Field

from app.models.desktop import DesktopStatus
from app.models.session import SessionStatus


class DesktopRegisterRequest(BaseModel):
    desktopAppId: str
    machineName: Optional[str] = None
    tokenControlVersion: Optional[str] = None
    osUser: Optional[str] = None
    nameLabel: Optional[str] = None

    model_config = {"populate_by_name": True}


class DesktopRegisterResponse(BaseModel):
    desktopStatus: DesktopStatus
    requiredApprovalsN: int
    unlockMinutes: int


class DesktopHeartbeatRequest(BaseModel):
    machineName: Optional[str] = None
    tokenControlVersion: Optional[str] = None
    osUser: Optional[str] = None


class UnlockStatusResponse(BaseModel):
    desktopStatus: DesktopStatus
    isUnlocked: bool
    unlockedUntilUtc: Optional[datetime]
    remainingSeconds: int
    requiredApprovalsN: int
    approvalsSoFar: int
    sessionStatus: SessionStatus


class DesktopUpdateRequest(BaseModel):
    requiredApprovalsN: Optional[int] = Field(None, ge=1)
    unlockMinutes: Optional[int] = Field(None, ge=1)
    nameLabel: Optional[str] = None
    status: Optional[DesktopStatus] = None


class DesktopAdminOut(BaseModel):
    desktopAppId: str = Field(alias="desktop_app_id")
    nameLabel: Optional[str] = Field(None, alias="name_label")
    status: DesktopStatus
    requiredApprovalsN: int = Field(alias="required_approvals_n")
    unlockMinutes: int = Field(alias="unlock_minutes")
    lastSeenAtUtc: Optional[datetime] = Field(None, alias="last_seen_at_utc")

    model_config = {"from_attributes": True, "populate_by_name": True}


class AdminDesktopCreate(BaseModel):
    desktopAppId: str
    nameLabel: Optional[str] = None
    requiredApprovalsN: Optional[int] = Field(None, ge=1)
    unlockMinutes: Optional[int] = Field(None, ge=1)

    model_config = {"populate_by_name": True}


class AdminDesktopApprove(BaseModel):
    requiredApprovalsN: Optional[int] = Field(None, ge=1)
    unlockMinutes: Optional[int] = Field(None, ge=1)

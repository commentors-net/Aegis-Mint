from datetime import datetime
from typing import List, Optional

from pydantic import BaseModel

from app.models.desktop import DesktopStatus
from app.models.session import SessionStatus


class AssignedDesktop(BaseModel):
    desktopAppId: str
    nameLabel: Optional[str] = None
    appType: Optional[str] = None
    lastSeenAtUtc: Optional[datetime] = None
    requiredApprovalsN: int
    approvalsSoFar: int
    status: DesktopStatus
    sessionStatus: SessionStatus
    unlockedUntilUtc: Optional[datetime] = None
    alreadyApproved: bool = False
    remainingSeconds: int = 0

    model_config = {"from_attributes": True}


class ApprovalItem(BaseModel):
    approverUserId: str
    approvedAtUtc: datetime
    approverEmail: Optional[str] = None

    model_config = {"from_attributes": True}


class ApprovalSummary(BaseModel):
    sessionId: str
    desktopAppId: str
    status: SessionStatus
    requiredApprovalsSnapshot: int
    unlockedUntilUtc: Optional[datetime] = None
    remainingSeconds: int = 0
    approvals: List[ApprovalItem]

    model_config = {"from_attributes": True}

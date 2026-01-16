from datetime import datetime, timezone
from typing import List, Optional

from fastapi import APIRouter, Depends, HTTPException, Path, status
from sqlalchemy.orm import Session

from app.api.deps import get_db, require_role
from app.core.time import utcnow
from app.models import Approval, Desktop, SessionStatus, User, UserRole
from app.schemas.governance import ApprovalSummary, AssignedDesktop
from app.services import approval_service, desktop_service

router = APIRouter(prefix="/api/governance", tags=["governance"])


def _remaining_seconds(until: Optional[datetime]) -> int:
    if not until:
        return 0
    now = utcnow()
    target = until
    if target.tzinfo is None:
        target = target.replace(tzinfo=timezone.utc)
    diff = (target - now).total_seconds()
    return int(diff) if diff > 0 else 0


@router.get("/desktops", response_model=List[AssignedDesktop])
def list_assigned(
    db: Session = Depends(get_db), user: User = Depends(require_role(UserRole.GOVERNANCE_AUTHORITY))
):
    desktops = desktop_service.list_assigned_desktops(db, user)
    payload: list[AssignedDesktop] = []
    for d in desktops:
        session = approval_service.get_latest_session(db, d.desktop_app_id)
        status = session.status if session else SessionStatus.NONE
        unlocked = session.unlocked_until_utc if session else None
        approvals_count = len(session.approvals) if session else 0
        already_approved = False
        if session:
            already_approved = any(a.approver_user_id == user.id for a in session.approvals)
        remaining = _remaining_seconds(unlocked) if session else 0
        payload.append(
            AssignedDesktop(
                desktopAppId=d.desktop_app_id,
                nameLabel=d.name_label,
                appType=d.app_type,
                requiredApprovalsN=d.required_approvals_n,
                approvalsSoFar=approvals_count,
                status=d.status,
                sessionStatus=status,
                unlockedUntilUtc=unlocked,
                lastSeenAtUtc=d.last_seen_at_utc,
                alreadyApproved=already_approved,
                remainingSeconds=remaining,
            )
        )
    return payload


@router.post("/desktops/{desktop_app_id}/approve", response_model=ApprovalSummary)
def approve_desktop(
    desktop_app_id: str = Path(..., description="DesktopAppId"),
    db: Session = Depends(get_db),
    user: User = Depends(require_role(UserRole.GOVERNANCE_AUTHORITY)),
):
    desktop = db.query(Desktop).filter(Desktop.desktop_app_id == desktop_app_id).first()
    if not desktop:
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail="Desktop not found")

    session = approval_service.add_approval(db, desktop, user)
    remaining = _remaining_seconds(session.unlocked_until_utc)
    return ApprovalSummary(
        sessionId=session.id,
        desktopAppId=session.desktop_app_id,
        status=session.status,
        requiredApprovalsSnapshot=session.required_approvals_snapshot,
        unlockedUntilUtc=session.unlocked_until_utc,
        remainingSeconds=remaining,
        approvals=[
            {
                "approverUserId": a.approver_user_id,
                "approvedAtUtc": a.approved_at_utc,
                "approverEmail": db.query(User).filter(User.id == a.approver_user_id).first().email
                if a.approver_user_id
                else None,
            }
            for a in session.approvals
        ],
    )


@router.get("/desktops/{desktop_app_id}/history", response_model=ApprovalSummary | None)
def desktop_history(
    desktop_app_id: str,
    db: Session = Depends(get_db),
    user: User = Depends(require_role(UserRole.GOVERNANCE_AUTHORITY)),
):
    desktop = db.query(Desktop).filter(Desktop.desktop_app_id == desktop_app_id).first()
    if not desktop:
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail="Desktop not found")
    session = approval_service.get_latest_session(db, desktop.desktop_app_id)
    if not session:
        return None
    remaining = _remaining_seconds(session.unlocked_until_utc)
    return ApprovalSummary(
        sessionId=session.id,
        desktopAppId=session.desktop_app_id,
        status=session.status,
        requiredApprovalsSnapshot=session.required_approvals_snapshot,
        unlockedUntilUtc=session.unlocked_until_utc,
        remainingSeconds=remaining,
        approvals=[
            {
                "approverUserId": a.approver_user_id,
                "approvedAtUtc": a.approved_at_utc,
                "approverEmail": db.query(User).filter(User.id == a.approver_user_id).first().email
                if a.approver_user_id
                else None,
            }
            for a in session.approvals
        ],
    )

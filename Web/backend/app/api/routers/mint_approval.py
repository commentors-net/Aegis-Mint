from typing import List

from fastapi import APIRouter, Depends, HTTPException, status
from pydantic import BaseModel
from sqlalchemy.orm import Session

from app.api.deps import get_db, require_role
from app.models import Desktop, DesktopStatus, User, UserRole
from app.schemas.desktop import DesktopAdminOut
from app.schemas.governance import ApprovalSummary
from app.services.audit_service import log_audit
from app.services import approval_service
from datetime import datetime, timezone
from app.core.time import utcnow

router = APIRouter(prefix="/api/admin/mint", tags=["mint-approval"])


class MintApprovalRequest(BaseModel):
    unlockMinutes: int = 15


def _remaining_seconds(until: datetime | None) -> int:
    if not until:
        return 0
    now = utcnow()
    target = until
    if target.tzinfo is None:
        target = target.replace(tzinfo=timezone.utc)
    diff = (target - now).total_seconds()
    return int(diff) if diff > 0 else 0


@router.get("/desktops", response_model=List[DesktopAdminOut])
def list_mint_desktops(
    db: Session = Depends(get_db),
    _: User = Depends(require_role(UserRole.SUPER_ADMIN))
):
    """List all Mint desktops (pending and active)"""
    desktops = db.query(Desktop).filter(Desktop.app_type == "Mint").all()
    return desktops


@router.post("/desktops/{desktop_app_id}/approve", response_model=DesktopAdminOut)
def approve_mint_desktop(
    desktop_app_id: str,
    body: MintApprovalRequest | None = None,
    db: Session = Depends(get_db),
    current_user: User = Depends(require_role(UserRole.SUPER_ADMIN)),
):
    """
    Approve a Mint desktop and automatically assign it to all admin users.
    Sets required_approvals_n=1 and unlock_minutes=15 by default.
    """
    desktop = db.query(Desktop).filter(
        Desktop.desktop_app_id == desktop_app_id,
        Desktop.app_type == "Mint"
    ).first()
    
    if not desktop:
        raise HTTPException(
            status_code=status.HTTP_404_NOT_FOUND,
            detail="Mint desktop not found"
        )
    
    # Set Mint-specific defaults
    desktop.status = DesktopStatus.ACTIVE
    desktop.required_approvals_n = 1  # Always 1 for Mint
    desktop.unlock_minutes = body.unlockMinutes if body else 15
    
    db.add(desktop)
    db.commit()
    
    # Auto-assign to all admin users
    from app.models import GovernanceAssignment
    
    admin_users = db.query(User).filter(User.role == UserRole.SUPER_ADMIN).all()
    
    # Remove existing assignments for this desktop
    db.query(GovernanceAssignment).filter(
        GovernanceAssignment.desktop_id == desktop.id
    ).delete()
    
    # Create new assignments for all admins
    for admin in admin_users:
        assignment = GovernanceAssignment(
            user_id=admin.id,
            desktop_id=desktop.id,
            desktop_app_id=desktop.desktop_app_id
        )
        db.add(assignment)
    
    db.commit()
    db.refresh(desktop)
    
    log_audit(
        db,
        action="MINT_DESKTOP_APPROVED",
        desktop_app_id=desktop.desktop_app_id,
        actor_user_id=current_user.id,
        details={
            "requiredApprovalsN": desktop.required_approvals_n,
            "unlockMinutes": desktop.unlock_minutes,
            "status": desktop.status.value,
            "assignedAdmins": len(admin_users)
        },
    )
    
    return desktop


@router.post("/desktops/{desktop_app_id}/approve-session", response_model=ApprovalSummary)
def approve_mint_session(
    desktop_app_id: str,
    db: Session = Depends(get_db),
    current_user: User = Depends(require_role(UserRole.SUPER_ADMIN)),
):
    """
    Approve an unlock session request from a Mint desktop.
    Only admins can approve Mint unlock sessions.
    """
    desktop = db.query(Desktop).filter(
        Desktop.desktop_app_id == desktop_app_id,
        Desktop.app_type == "Mint"
    ).first()
    
    if not desktop:
        raise HTTPException(
            status_code=status.HTTP_404_NOT_FOUND,
            detail="Mint desktop not found"
        )
    
    if desktop.status != DesktopStatus.ACTIVE:
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail="Desktop must be active to approve sessions"
        )
    
    # Add approval using the approval service
    session = approval_service.add_approval(db, desktop, current_user)
    
    remaining = _remaining_seconds(session.unlocked_until_utc)
    
    # Build approval items list
    from app.schemas.governance import ApprovalItem
    approval_items = []
    for approval in session.approvals:
        approval_items.append(
            ApprovalItem(
                approverUserId=approval.approver_user_id,
                approvedAtUtc=approval.approved_at_utc,
                approverEmail=approval.approver.email if approval.approver else None
            )
        )
    
    return ApprovalSummary(
        sessionId=session.id,
        desktopAppId=session.desktop_app_id,
        status=session.status,
        requiredApprovalsSnapshot=session.required_approvals_snapshot,
        unlockedUntilUtc=session.unlocked_until_utc,
        remainingSeconds=remaining,
        approvals=approval_items,
    )

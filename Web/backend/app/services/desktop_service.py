from fastapi import HTTPException, status
from sqlalchemy.orm import Session

from app.core.time import utcnow
from app.models import Desktop, DesktopStatus, GovernanceAssignment, SessionStatus, User
from app.schemas.desktop import DesktopHeartbeatRequest, DesktopRegisterRequest, DesktopUpdateRequest
from .approval_service import get_latest_session
from .audit_service import log_audit


def register_desktop(db: Session, body: DesktopRegisterRequest) -> Desktop:
    desktop = db.query(Desktop).filter(Desktop.desktop_app_id == body.desktopAppId).first()
    created = False
    if not desktop:
        desktop = Desktop(desktop_app_id=body.desktopAppId, status=DesktopStatus.PENDING)
        created = True

    desktop.machine_name = body.machineName or desktop.machine_name
    desktop.token_control_version = body.tokenControlVersion or desktop.token_control_version
    desktop.os_user = body.osUser or desktop.os_user
    desktop.name_label = body.nameLabel or desktop.name_label
    desktop.last_seen_at_utc = utcnow()

    db.add(desktop)
    db.commit()
    db.refresh(desktop)

    if created:
        log_audit(db, action="REGISTERED", desktop_app_id=desktop.desktop_app_id, details={"nameLabel": desktop.name_label})
    else:
        log_audit(
            db,
            action="HEARTBEAT",
            desktop_app_id=desktop.desktop_app_id,
            details={
                "machineName": desktop.machine_name,
                "tokenControlVersion": desktop.token_control_version,
                "osUser": desktop.os_user,
            },
        )

    return desktop


def heartbeat(db: Session, desktop_app_id: str, body: DesktopHeartbeatRequest) -> Desktop:
    desktop = db.query(Desktop).filter(Desktop.desktop_app_id == desktop_app_id).first()
    if not desktop:
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail="Desktop not found")

    desktop.machine_name = body.machineName or desktop.machine_name
    desktop.token_control_version = body.tokenControlVersion or desktop.token_control_version
    desktop.os_user = body.osUser or desktop.os_user
    desktop.last_seen_at_utc = utcnow()

    db.add(desktop)
    db.commit()
    db.refresh(desktop)
    return desktop


def unlock_status(db: Session, desktop_app_id: str):
    desktop = db.query(Desktop).filter(Desktop.desktop_app_id == desktop_app_id).first()
    if not desktop:
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail="Desktop not found")

    session = get_latest_session(db, desktop.desktop_app_id)
    now = utcnow()
    session_status = session.status if session else SessionStatus.NONE
    unlocked_until = session.unlocked_until_utc if session else None
    is_unlocked = bool(unlocked_until and unlocked_until > now and session_status == SessionStatus.UNLOCKED)
    remaining = int((unlocked_until - now).total_seconds()) if unlocked_until and unlocked_until > now else 0
    approvals_so_far = len(session.approvals) if session else 0

    return {
        "desktopStatus": desktop.status,
        "isUnlocked": is_unlocked,
        "unlockedUntilUtc": unlocked_until,
        "remainingSeconds": remaining,
        "requiredApprovalsN": desktop.required_approvals_n,
        "approvalsSoFar": approvals_so_far,
        "sessionStatus": session_status,
    }


def update_desktop(db: Session, desktop_app_id: str, body: DesktopUpdateRequest) -> Desktop:
    desktop = db.query(Desktop).filter(Desktop.desktop_app_id == desktop_app_id).first()
    if not desktop:
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail="Desktop not found")

    if body.requiredApprovalsN is not None:
        desktop.required_approvals_n = body.requiredApprovalsN
    if body.unlockMinutes is not None:
        desktop.unlock_minutes = body.unlockMinutes
    if body.nameLabel is not None:
        desktop.name_label = body.nameLabel
    if body.status is not None:
        desktop.status = body.status

    db.add(desktop)
    db.commit()
    db.refresh(desktop)
    log_audit(
        db,
        action="DESKTOP_UPDATED",
        desktop_app_id=desktop.desktop_app_id,
        details={
            "requiredApprovalsN": desktop.required_approvals_n,
            "unlockMinutes": desktop.unlock_minutes,
            "nameLabel": desktop.name_label,
            "status": desktop.status.value,
        },
    )
    return desktop


def assign_authorities(db: Session, desktop: Desktop, authority_ids: list[str]):
    # Remove existing
    db.query(GovernanceAssignment).filter(GovernanceAssignment.desktop_app_id == desktop.desktop_app_id).delete()
    for user_id in authority_ids:
        db.add(GovernanceAssignment(user_id=user_id, desktop_app_id=desktop.desktop_app_id))
    db.commit()


def list_assigned_desktops(db: Session, user: User):
    rows = (
        db.query(Desktop)
        .join(GovernanceAssignment, GovernanceAssignment.desktop_app_id == Desktop.desktop_app_id)
        .filter(GovernanceAssignment.user_id == user.id)
        .all()
    )
    return rows

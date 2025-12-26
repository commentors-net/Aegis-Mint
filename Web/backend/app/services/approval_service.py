from datetime import timedelta

from fastapi import HTTPException, status
from sqlalchemy.orm import Session

from app.core.time import utcnow
from app.models import Approval, ApprovalSession, Desktop, SessionStatus, User
from app.models.desktop import DesktopStatus
from .audit_service import log_audit


def _expire_if_needed(session: ApprovalSession, db: Session | None = None) -> None:
    now = utcnow()
    if session.unlocked_until_utc and session.status == SessionStatus.UNLOCKED:
        # ensure both datetimes are tz-aware
        unlocked_until = session.unlocked_until_utc
        if unlocked_until.tzinfo is None:
            unlocked_until = unlocked_until.replace(tzinfo=now.tzinfo)
        if now >= unlocked_until:
            session.status = SessionStatus.EXPIRED
            if db:
                db.add(session)
                db.commit()
                db.refresh(session)


def _get_latest_session(db: Session, desktop_app_id: str) -> ApprovalSession | None:
    return (
        db.query(ApprovalSession)
        .filter(ApprovalSession.desktop_app_id == desktop_app_id)
        .order_by(ApprovalSession.created_at_utc.desc())
        .first()
    )


def get_latest_session(db: Session, desktop_app_id: str) -> ApprovalSession | None:
    session = _get_latest_session(db, desktop_app_id)
    if session:
        _expire_if_needed(session, db)
    return session


def get_or_create_active_session(db: Session, desktop: Desktop) -> ApprovalSession:
    latest = _get_latest_session(db, desktop.desktop_app_id)
    if latest:
        _expire_if_needed(latest, db)
        if latest.status in (SessionStatus.PENDING, SessionStatus.UNLOCKED):
            now = utcnow()
            unlocked_until = latest.unlocked_until_utc
            if not unlocked_until or unlocked_until.tzinfo is None:
                unlocked_until = unlocked_until.replace(tzinfo=now.tzinfo) if unlocked_until else None
            if not unlocked_until or now < unlocked_until:
                return latest

    session = ApprovalSession(
        desktop_app_id=desktop.desktop_app_id,
        required_approvals_snapshot=desktop.required_approvals_n,
        status=SessionStatus.PENDING,
    )
    db.add(session)
    db.commit()
    db.refresh(session)
    log_audit(db, action="SESSION_CREATED", desktop_app_id=desktop.desktop_app_id, session_id=session.id)
    return session


def add_approval(db: Session, desktop: Desktop, approver: User) -> ApprovalSession:
    if desktop.status != DesktopStatus.ACTIVE:
        raise HTTPException(status_code=status.HTTP_400_BAD_REQUEST, detail="Desktop not active")

    session = get_or_create_active_session(db, desktop)
    # Refresh status if window expired
    _expire_if_needed(session, db)
    if session.status == SessionStatus.EXPIRED:
        db.add(session)
        db.commit()
        session = get_or_create_active_session(db, desktop)

    existing = (
        db.query(Approval)
        .filter(Approval.session_id == session.id, Approval.approver_user_id == approver.id)
        .first()
    )
    if existing:
        raise HTTPException(status_code=status.HTTP_409_CONFLICT, detail="Already approved in this session")

    approval = Approval(session_id=session.id, approver_user_id=approver.id)
    db.add(approval)
    db.commit()
    db.refresh(approval)

    approvals_count = db.query(Approval).filter(Approval.session_id == session.id).count()
    required = session.required_approvals_snapshot

    if approvals_count >= required and session.status != SessionStatus.UNLOCKED:
        now = utcnow()
        session.unlocked_at_utc = now
        session.unlocked_until_utc = now + timedelta(minutes=desktop.unlock_minutes)
        session.status = SessionStatus.UNLOCKED
        db.add(session)
        db.commit()
        log_audit(
            db,
            action="UNLOCKED",
            actor_user_id=approver.id,
            desktop_app_id=desktop.desktop_app_id,
            session_id=session.id,
            details={"unlockedUntilUtc": session.unlocked_until_utc.isoformat()},
        )

    log_audit(
        db,
        action="APPROVED",
        actor_user_id=approver.id,
        desktop_app_id=desktop.desktop_app_id,
        session_id=session.id,
        details={"approver": approver.email},
    )
    db.refresh(session)
    return session


def session_summary(db: Session, session: ApprovalSession):
    return {
        "sessionId": session.id,
        "desktopAppId": session.desktop_app_id,
        "status": session.status,
        "requiredApprovalsSnapshot": session.required_approvals_snapshot,
        "unlockedUntilUtc": session.unlocked_until_utc,
        "approvals": list(session.approvals),
    }

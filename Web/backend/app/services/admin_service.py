from fastapi import HTTPException, status
from sqlalchemy.orm import Session

from app.core import security
from app.models import Desktop, DesktopStatus, GovernanceAssignment, User
from app.schemas.admin import UserCreate, UserUpdate
from app.schemas.desktop import AdminDesktopApprove, AdminDesktopCreate, DesktopUpdateRequest
from .audit_service import log_audit
from .desktop_service import update_desktop
from app.models.setting import SystemSetting
from app.schemas.settings import SystemSettings
from app.core.config import get_settings as load_env_settings


def list_users(db: Session):
    return db.query(User).all()


def create_user(db: Session, body: UserCreate) -> User:
    existing = db.query(User).filter(User.email == body.email).first()
    if existing:
        raise HTTPException(status_code=status.HTTP_400_BAD_REQUEST, detail="Email already exists")
    mfa_secret = body.mfa_secret  # optional; will be set on first login if missing
    user = User(
        email=body.email,
        role=body.role,
        is_active=body.is_active,
        password_hash=security.hash_password(body.password),
        mfa_secret=mfa_secret,
    )
    db.add(user)
    db.commit()
    db.refresh(user)
    log_audit(db, action="USER_CREATED", actor_user_id=user.id, details={"email": user.email, "role": user.role.value})
    return user


def update_user(db: Session, user_id: str, body: UserUpdate) -> User:
    user = db.query(User).filter(User.id == user_id).first()
    if not user:
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail="User not found")
    if body.password:
        user.password_hash = security.hash_password(body.password)
    if body.role is not None:
        user.role = body.role
    if body.is_active is not None:
        user.is_active = body.is_active
    if body.mfa_secret is not None:
        user.mfa_secret = body.mfa_secret
    db.add(user)
    db.commit()
    db.refresh(user)
    log_audit(
        db,
        action="USER_UPDATED",
        actor_user_id=user.id,
        details={"email": user.email, "role": user.role.value, "is_active": user.is_active},
    )
    return user


def list_desktops(db: Session):
    return db.query(Desktop).all()


def admin_update_desktop(db: Session, desktop_app_id: str, body: DesktopUpdateRequest) -> Desktop:
    desktop = update_desktop(db, desktop_app_id, body)
    return desktop


def get_user_assignments(db: Session, user_id: str) -> list[str]:
    return [
        row.desktop_app_id
        for row in db.query(GovernanceAssignment.desktop_app_id)
        .filter(GovernanceAssignment.user_id == user_id)
        .all()
    ]


def set_user_assignments(db: Session, user_id: str, desktop_ids: list[str]) -> list[str]:
    db.query(GovernanceAssignment).filter(GovernanceAssignment.user_id == user_id).delete()
    for desktop_id in desktop_ids:
        db.add(GovernanceAssignment(user_id=user_id, desktop_app_id=desktop_id))
    db.commit()
    return desktop_ids


def create_desktop(db: Session, body: AdminDesktopCreate) -> Desktop:
    existing = db.query(Desktop).filter(Desktop.desktop_app_id == body.desktopAppId).first()
    if existing:
        raise HTTPException(status_code=status.HTTP_400_BAD_REQUEST, detail="Desktop already exists")
    desktop = Desktop(
        desktop_app_id=body.desktopAppId,
        name_label=body.nameLabel,
        app_type=body.appType or "TokenControl",
        required_approvals_n=body.requiredApprovalsN or Desktop.required_approvals_n.default.arg,
        unlock_minutes=body.unlockMinutes or Desktop.unlock_minutes.default.arg,
        status=DesktopStatus.PENDING,
    )
    db.add(desktop)
    db.commit()
    db.refresh(desktop)
    log_audit(
        db,
        action="DESKTOP_REGISTERED",
        desktop_app_id=desktop.desktop_app_id,
        details={
            "requiredApprovalsN": desktop.required_approvals_n,
            "unlockMinutes": desktop.unlock_minutes,
            "nameLabel": desktop.name_label,
            "appType": desktop.app_type,
        },
    )
    return desktop


def approve_desktop(db: Session, desktop_app_id: str, body: AdminDesktopApprove) -> Desktop:
    desktop = db.query(Desktop).filter(Desktop.desktop_app_id == desktop_app_id).first()
    if not desktop:
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail="Desktop not found")
    desktop.status = DesktopStatus.ACTIVE
    if body.requiredApprovalsN is not None:
        desktop.required_approvals_n = body.requiredApprovalsN
    if body.unlockMinutes is not None:
        desktop.unlock_minutes = body.unlockMinutes
    db.add(desktop)
    db.commit()
    db.refresh(desktop)
    log_audit(
        db,
        action="DESKTOP_APPROVED",
        desktop_app_id=desktop.desktop_app_id,
        details={
            "requiredApprovalsN": desktop.required_approvals_n,
            "unlockMinutes": desktop.unlock_minutes,
            "status": desktop.status.value,
        },
    )
    return desktop


def reject_desktop(db: Session, desktop_app_id: str) -> Desktop:
    desktop = db.query(Desktop).filter(Desktop.desktop_app_id == desktop_app_id).first()
    if not desktop:
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail="Desktop not found")
    desktop.status = DesktopStatus.DISABLED
    db.add(desktop)
    db.commit()
    db.refresh(desktop)
    log_audit(
        db,
        action="DESKTOP_REJECTED",
        desktop_app_id=desktop.desktop_app_id,
        details={"status": desktop.status.value},
    )
    return desktop


def get_system_settings(db: Session) -> SystemSettings:
    env_settings = load_env_settings()
    required = env_settings.required_approvals_default
    unlock = env_settings.unlock_minutes_default
    rows = db.query(SystemSetting).all()
    for row in rows:
        if row.key == "required_approvals_default":
            try:
                required = int(row.value)
            except ValueError:
                pass
        if row.key == "unlock_minutes_default":
            try:
                unlock = int(row.value)
            except ValueError:
                pass
    return SystemSettings(required_approvals_default=required, unlock_minutes_default=unlock)


def update_system_settings(db: Session, payload: SystemSettings, actor_user_id: str | None = None) -> SystemSettings:
    current = get_system_settings(db)
    for key, value in [
        ("required_approvals_default", str(payload.requiredApprovalsDefault or current.requiredApprovalsDefault)),
        ("unlock_minutes_default", str(payload.unlockMinutesDefault or current.unlockMinutesDefault)),
    ]:
        row = db.query(SystemSetting).filter(SystemSetting.key == key).first()
        if not row:
            row = SystemSetting(key=key, value=value)
        else:
            row.value = value
        db.add(row)
    db.commit()
    log_audit(
        db,
        action="SETTINGS_UPDATED",
        actor_user_id=actor_user_id,
        details={
            "requiredApprovalsDefault": payload.requiredApprovalsDefault or current.requiredApprovalsDefault,
            "unlockMinutesDefault": payload.unlockMinutesDefault or current.unlockMinutesDefault,
        },
    )
    return get_system_settings(db)

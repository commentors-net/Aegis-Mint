from typing import List

from fastapi import APIRouter, Depends, HTTPException, Path, status
from pydantic import BaseModel
from sqlalchemy.orm import Session

from app.api.deps import get_db, require_role
from app.models import AuditLog, Desktop, GovernanceAssignment, User, UserRole
from app.schemas.admin import UserCreate, UserOut, UserUpdate
from app.schemas.desktop import AdminDesktopApprove, AdminDesktopCreate, DesktopAdminOut, DesktopUpdateRequest
from app.schemas.audit import AuditLogEntry, AuditPage
from app.schemas.settings import SystemSettings
from app.services import admin_service
from app.services.audit_service import log_audit
from app.services.desktop_service import assign_authorities

router = APIRouter(prefix="/api/admin", tags=["admin"])


@router.get("/users", response_model=List[UserOut])
def list_users(db: Session = Depends(get_db), _: User = Depends(require_role(UserRole.SUPER_ADMIN))):
    return admin_service.list_users(db)


@router.post("/users", response_model=UserOut)
def create_user(body: UserCreate, db: Session = Depends(get_db), _: User = Depends(require_role(UserRole.SUPER_ADMIN))):
    return admin_service.create_user(db, body)


@router.put("/users/{user_id}", response_model=UserOut)
def update_user(
    user_id: str = Path(...),
    body: UserUpdate | None = None,
    db: Session = Depends(get_db),
    _: User = Depends(require_role(UserRole.SUPER_ADMIN)),
):
    return admin_service.update_user(db, user_id, body or UserUpdate())


@router.get("/desktops", response_model=List[DesktopAdminOut])
def list_desktops(db: Session = Depends(get_db), _: User = Depends(require_role(UserRole.SUPER_ADMIN))):
    return admin_service.list_desktops(db)


@router.post("/desktops", response_model=DesktopAdminOut)
def create_desktop(
    body: AdminDesktopCreate,
    db: Session = Depends(get_db),
    _: User = Depends(require_role(UserRole.SUPER_ADMIN)),
):
    return admin_service.create_desktop(db, body)


@router.put("/desktops/{desktop_app_id}", response_model=DesktopAdminOut)
def update_desktop(
    desktop_app_id: str,
    app_type: str = "TokenControl",
    body: DesktopUpdateRequest | None = None,
    db: Session = Depends(get_db),
    _: User = Depends(require_role(UserRole.SUPER_ADMIN)),
):
    return admin_service.admin_update_desktop(db, desktop_app_id, app_type, body or DesktopUpdateRequest())


class AssignRequest(BaseModel):
    authorityIds: List[str]


class UserAssignRequest(BaseModel):
    desktopAppIds: List[str]


@router.put("/desktops/{desktop_app_id}/assign")
def assign_desktop(
    desktop_app_id: str,
    body: AssignRequest,
    db: Session = Depends(get_db),
    _: User = Depends(require_role(UserRole.SUPER_ADMIN)),
):
    desktop = db.query(Desktop).filter(Desktop.desktop_app_id == desktop_app_id).first()
    if not desktop:
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail="Desktop not found")
    assign_authorities(db, desktop, body.authorityIds)
    log_audit(db, action="ASSIGNED_AUTHORITIES", desktop_app_id=desktop.desktop_app_id)
    return {"desktopAppId": desktop_app_id, "authorityIds": body.authorityIds}


@router.get("/audit", response_model=AuditPage)
def audit(
    page: int = 1,
    page_size: int = 20,
    q: str | None = None,
    db: Session = Depends(get_db),
    _: User = Depends(require_role(UserRole.SUPER_ADMIN)),
):
    qs = db.query(AuditLog)
    if q:
        like = f"%{q}%"
        qs = qs.filter(
            (AuditLog.desktop_app_id.like(like))
            | (AuditLog.action.like(like))
            | (AuditLog.actor_user_id.like(like))
            | (AuditLog.details.like(like))
        )
    total = qs.count()
    page = max(1, page)
    page_size = max(1, min(page_size, 100))
    items = (
        qs.order_by(AuditLog.at_utc.desc())
        .offset((page - 1) * page_size)
        .limit(page_size)
        .all()
    )
    return {"items": items, "total": total, "page": page, "pageSize": page_size}


@router.get("/users/{user_id}/assignments", response_model=List[str])
def get_user_assignments(
    user_id: str,
    db: Session = Depends(get_db),
    _: User = Depends(require_role(UserRole.SUPER_ADMIN)),
):
    return admin_service.get_user_assignments(db, user_id)


@router.post("/users/{user_id}/assignments", response_model=List[str])
def set_user_assignments(
    user_id: str,
    body: UserAssignRequest,
    db: Session = Depends(get_db),
    _: User = Depends(require_role(UserRole.SUPER_ADMIN)),
):
    return admin_service.set_user_assignments(db, user_id, body.desktopAppIds)


@router.post("/desktops/{desktop_app_id}/approve", response_model=DesktopAdminOut)
def approve_desktop(
    desktop_app_id: str,
    app_type: str = "TokenControl",
    body: AdminDesktopApprove | None = None,
    db: Session = Depends(get_db),
    _: User = Depends(require_role(UserRole.SUPER_ADMIN)),
):
    return admin_service.approve_desktop(db, desktop_app_id, app_type, body or AdminDesktopApprove())


@router.post("/desktops/{desktop_app_id}/reject", response_model=DesktopAdminOut)
def reject_desktop(
    desktop_app_id: str,
    app_type: str = "TokenControl",
    db: Session = Depends(get_db),
    _: User = Depends(require_role(UserRole.SUPER_ADMIN)),
):
    return admin_service.reject_desktop(db, desktop_app_id, app_type)


@router.get("/settings", response_model=SystemSettings)
def get_settings_route(
    db: Session = Depends(get_db),
    _: User = Depends(require_role(UserRole.SUPER_ADMIN)),
):
    return admin_service.get_system_settings(db)


@router.put("/settings", response_model=SystemSettings)
def update_settings_route(
    body: SystemSettings,
    db: Session = Depends(get_db),
    user: User = Depends(require_role(UserRole.SUPER_ADMIN)),
):
    return admin_service.update_system_settings(db, body, actor_user_id=user.id)

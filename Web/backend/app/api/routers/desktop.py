from fastapi import APIRouter, Depends, Path
from sqlalchemy.orm import Session

from app.api.deps import get_db
from app.schemas.desktop import (
    DesktopHeartbeatRequest,
    DesktopRegisterRequest,
    DesktopRegisterResponse,
    UnlockStatusResponse,
)
from app.services import desktop_service

router = APIRouter(prefix="/api/desktop", tags=["desktop"])


@router.post("/register", response_model=DesktopRegisterResponse)
def register(body: DesktopRegisterRequest, db: Session = Depends(get_db)):
    desktop = desktop_service.register_desktop(db, body)
    return DesktopRegisterResponse(
        desktopStatus=desktop.status,
        requiredApprovalsN=desktop.required_approvals_n,
        unlockMinutes=desktop.unlock_minutes,
    )


@router.post("/{desktop_app_id}/heartbeat", response_model=DesktopRegisterResponse)
def heartbeat(
    desktop_app_id: str = Path(..., description="DesktopAppId GUID"),
    body: DesktopHeartbeatRequest | None = None,
    db: Session = Depends(get_db),
):
    desktop = desktop_service.heartbeat(db, desktop_app_id, body or DesktopHeartbeatRequest())
    return DesktopRegisterResponse(
        desktopStatus=desktop.status,
        requiredApprovalsN=desktop.required_approvals_n,
        unlockMinutes=desktop.unlock_minutes,
    )


@router.get("/{desktop_app_id}/unlock-status", response_model=UnlockStatusResponse)
def unlock_status(desktop_app_id: str, db: Session = Depends(get_db)):
    return desktop_service.unlock_status(db, desktop_app_id)

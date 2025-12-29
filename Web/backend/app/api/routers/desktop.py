from fastapi import APIRouter, Depends, Path, HTTPException
from sqlalchemy.orm import Session
from pydantic import BaseModel

from app.api.deps import get_db
from app.api.desktop_deps import get_authenticated_desktop
from app.models import Desktop
from app.schemas.desktop import (
    DesktopHeartbeatRequest,
    DesktopRegisterRequest,
    DesktopRegisterResponse,
    UnlockStatusResponse,
)
from app.services import desktop_service
from app.services.desktop_service import DesktopService

router = APIRouter(prefix="/api/desktop", tags=["desktop"])


@router.post("/register", response_model=DesktopRegisterResponse)
def register(body: DesktopRegisterRequest, db: Session = Depends(get_db)):
    """
    Register a new desktop application or update an existing one.
    Note: Registration does not require authentication (first-time setup).
    """
    desktop = desktop_service.register_desktop(db, body)
    secret_key = getattr(desktop, '_secret_key_for_response', None)
    return DesktopRegisterResponse(
        desktopStatus=desktop.status,
        requiredApprovalsN=desktop.required_approvals_n,
        unlockMinutes=desktop.unlock_minutes,
        secretKey=secret_key,
    )


@router.post("/{desktop_app_id}/heartbeat", response_model=DesktopRegisterResponse)
def heartbeat(
    desktop_app_id: str = Path(..., description="DesktopAppId GUID"),
    body: DesktopHeartbeatRequest | None = None,
    desktop: Desktop = Depends(get_authenticated_desktop),
    db: Session = Depends(get_db),
):
    """
    Send a heartbeat to keep the desktop registration alive.
    Requires HMAC authentication.
    """
    desktop = desktop_service.heartbeat(db, desktop_app_id, body or DesktopHeartbeatRequest())
    return DesktopRegisterResponse(
        desktopStatus=desktop.status,
        requiredApprovalsN=desktop.required_approvals_n,
        unlockMinutes=desktop.unlock_minutes,
    )


@router.get("/{desktop_app_id}/unlock-status", response_model=UnlockStatusResponse)
def unlock_status(
    desktop_app_id: str,
    desktop: Desktop = Depends(get_authenticated_desktop),
    db: Session = Depends(get_db)
):
    """
    Check the unlock status of a desktop application.
    Requires HMAC authentication.
    """
    return desktop_service.unlock_status(db, desktop_app_id)


class SubmitCSRRequest(BaseModel):
    """Request to submit Certificate Signing Request"""
    csr_pem: str


class SubmitCSRResponse(BaseModel):
    """Response after CSR submission"""
    success: bool
    message: str
    desktop_app_id: str


@router.post("/{desktop_app_id}/submit-csr", response_model=SubmitCSRResponse)
def submit_csr(
    desktop_app_id: str,
    body: SubmitCSRRequest,
    desktop: Desktop = Depends(get_authenticated_desktop),
    db: Session = Depends(get_db)
):
    """
    Desktop submits Certificate Signing Request for admin approval.
    Requires HMAC authentication.
    """
    try:
        service = DesktopService(db)
        service.submit_csr(desktop_app_id, body.csr_pem)
        
        return SubmitCSRResponse(
            success=True,
            message="CSR submitted successfully. Awaiting admin approval.",
            desktop_app_id=desktop_app_id
        )
    except ValueError as e:
        raise HTTPException(status_code=400, detail=str(e))


@router.get("/{desktop_app_id}/certificate")
def get_certificate(
    desktop_app_id: str,
    desktop: Desktop = Depends(get_authenticated_desktop),
    db: Session = Depends(get_db)
):
    """
    Retrieve signed certificate if available.
    Requires HMAC authentication.
    """
    desktop = db.query(Desktop).filter(Desktop.desktop_app_id == desktop_app_id).first()
    if not desktop:
        raise HTTPException(status_code=404, detail="Desktop not found")
    
    if not desktop.certificate_pem:
        raise HTTPException(status_code=404, detail="Certificate not yet issued")
    
    return {
        "certificate_pem": desktop.certificate_pem,
        "issued_at": desktop.certificate_issued_at,
        "expires_at": desktop.certificate_expires_at
    }

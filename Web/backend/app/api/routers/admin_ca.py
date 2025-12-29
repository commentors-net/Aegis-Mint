"""
Admin API router for Certificate Authority management
"""
from fastapi import APIRouter, Depends, HTTPException
from sqlalchemy.orm import Session
from typing import Optional
from pydantic import BaseModel
from datetime import datetime

from app.api.deps import get_db
from app.services.ca_persistence_service import CAPersistenceService
from app.services.desktop_service import DesktopService


router = APIRouter()


class CAInfoResponse(BaseModel):
    """CA information response"""
    exists: bool
    ca_certificate: Optional[str] = None
    created_at: Optional[datetime] = None
    expires_at: Optional[datetime] = None
    expiring_soon: Optional[bool] = None
    expired: Optional[bool] = None
    days_until_expiry: Optional[int] = None
    subject: Optional[str] = None
    issuer: Optional[str] = None


class GenerateCAResponse(BaseModel):
    """Response after CA generation"""
    success: bool
    message: str
    ca_certificate: str
    created_at: datetime
    expires_at: datetime


class PendingCertificatesResponse(BaseModel):
    """Response for pending certificate requests"""
    pending_requests: list


@router.get("/status", response_model=CAInfoResponse)
def get_ca_status(db: Session = Depends(get_db)):
    """
    Get CA status and information
    
    Returns CA details if exists, otherwise returns exists=False
    """
    ca_info = CAPersistenceService.get_ca_info(db)
    
    if not ca_info:
        return CAInfoResponse(exists=False)
    
    return CAInfoResponse(
        exists=True,
        ca_certificate=ca_info['ca_certificate'],
        created_at=ca_info['created_at'],
        expires_at=ca_info['expires_at'],
        expiring_soon=ca_info['expiring_soon'],
        expired=ca_info['expired'],
        days_until_expiry=ca_info['days_until_expiry'],
        subject=ca_info['subject'],
        issuer=ca_info['issuer']
    )


@router.post("/generate", response_model=GenerateCAResponse)
def generate_ca(db: Session = Depends(get_db)):
    """
    Generate new Certificate Authority
    
    Admin clicks button in UI to create CA.
    Only one active CA allowed at a time.
    """
    try:
        result = CAPersistenceService.generate_and_store_ca(db)
        
        return GenerateCAResponse(
            success=True,
            message="Certificate Authority generated successfully",
            ca_certificate=result['ca_certificate'],
            created_at=result['created_at'],
            expires_at=result['expires_at']
        )
    except ValueError as e:
        raise HTTPException(status_code=400, detail=str(e))
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Failed to generate CA: {str(e)}")


@router.get("/certificate")
def download_ca_certificate(db: Session = Depends(get_db)):
    """
    Download CA certificate (PEM format)
    
    Returns raw PEM certificate for download
    """
    ca_info = CAPersistenceService.get_ca_info(db)
    
    if not ca_info:
        raise HTTPException(status_code=404, detail="No CA certificate found")
    
    return {
        "ca_certificate": ca_info['ca_certificate'],
        "expires_at": ca_info['expires_at'],
        "filename": "aegismint-ca.pem"
    }


@router.get("/pending-certificates", response_model=PendingCertificatesResponse)
def get_pending_certificate_requests(db: Session = Depends(get_db)):
    """
    Get list of desktops that have submitted CSRs awaiting admin approval
    """
    service = DesktopService(db)
    pending_desktops = service.get_pending_certificate_requests()
    
    return PendingCertificatesResponse(pending_requests=pending_desktops)


@router.post("/approve-certificate/{desktop_app_id}")
def approve_certificate_request(
    desktop_app_id: str,
    db: Session = Depends(get_db)
):
    """
    Admin approves desktop certificate request and signs CSR
    
    Returns signed certificate
    """
    service = DesktopService(db)
    
    try:
        result = service.sign_desktop_certificate(desktop_app_id)
        return {
            "success": True,
            "message": "Certificate signed successfully",
            "certificate": result['certificate'],
            "expires_at": result['expires_at']
        }
    except ValueError as e:
        raise HTTPException(status_code=400, detail=str(e))
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Failed to sign certificate: {str(e)}")


@router.post("/reject-certificate/{desktop_app_id}")
def reject_certificate_request(
    desktop_app_id: str,
    db: Session = Depends(get_db)
):
    """
    Admin rejects desktop certificate request
    """
    service = DesktopService(db)
    
    try:
        service.reject_certificate_request(desktop_app_id)
        return {
            "success": True,
            "message": "Certificate request rejected"
        }
    except ValueError as e:
        raise HTTPException(status_code=400, detail=str(e))

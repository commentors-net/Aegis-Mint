from fastapi import HTTPException, status
from sqlalchemy.orm import Session
from datetime import datetime, timezone
from typing import Optional, Tuple

from app.core.time import utcnow
from app.core.hmac_auth import generate_secret_key
from app.models import Desktop, DesktopStatus, GovernanceAssignment, SessionStatus, User
from app.schemas.desktop import DesktopHeartbeatRequest, DesktopRegisterRequest, DesktopUpdateRequest
from .approval_service import get_latest_session
from .audit_service import log_audit
from .key_rotation_service import check_and_rotate_if_needed
from .ca_service import CAService
from .ca_persistence_service import CAPersistenceService


def _make_aware(dt: datetime) -> datetime:
    """Ensure datetime is timezone-aware (UTC)"""
    if dt is None:
        return None
    if dt.tzinfo is None:
        return dt.replace(tzinfo=timezone.utc)
    return dt


def register_desktop(db: Session, body: DesktopRegisterRequest) -> Desktop:
    app_type = body.appType or "TokenControl"
    desktop = db.query(Desktop).filter(
        Desktop.desktop_app_id == body.desktopAppId,
        Desktop.app_type == app_type
    ).first()
    created = False
    if not desktop:
        # Set Mint-specific defaults
        required_approvals = 1 if app_type == "Mint" else Desktop.required_approvals_n.default.arg()
        unlock_minutes = 15 if app_type == "Mint" else Desktop.unlock_minutes.default.arg()
        
        desktop = Desktop(
            desktop_app_id=body.desktopAppId,
            status=DesktopStatus.PENDING,
            app_type=app_type,
            required_approvals_n=required_approvals,
            unlock_minutes=unlock_minutes,
            secret_key=generate_secret_key()  # Generate secret key on first registration
        )
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
        log_audit(db, action="REGISTERED", desktop_app_id=desktop.desktop_app_id, details={"nameLabel": desktop.name_label, "appType": desktop.app_type})
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

    # Store secret_key temporarily for response (only on creation)
    if created:
        desktop._secret_key_for_response = desktop.secret_key
    
    return desktop


def heartbeat(db: Session, desktop_app_id: str, body: DesktopHeartbeatRequest) -> Desktop:
    # Note: desktop is already filtered by app_type in get_authenticated_desktop dependency
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


def unlock_status(db: Session, desktop_app_id: str, app_type: str):
    desktop = db.query(Desktop).filter(
        Desktop.desktop_app_id == desktop_app_id,
        Desktop.app_type == app_type
    ).first()
    if not desktop:
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail="Desktop not found")

    # Check if key rotation is needed and perform it
    rotated, new_key = check_and_rotate_if_needed(db, desktop)

    session = get_latest_session(db, desktop.desktop_app_id, desktop.app_type)
    now = utcnow()
    session_status = session.status if session else SessionStatus.NONE
    unlocked_until = _make_aware(session.unlocked_until_utc) if session else None
    is_unlocked = bool(unlocked_until and unlocked_until > now and session_status == SessionStatus.UNLOCKED)
    remaining = int((unlocked_until - now).total_seconds()) if unlocked_until and unlocked_until > now else 0
    approvals_so_far = len(session.approvals) if session else 0

    response = {
        "desktopStatus": desktop.status,
        "isUnlocked": is_unlocked,
        "unlockedUntilUtc": unlocked_until,
        "remainingSeconds": remaining,
        "requiredApprovalsN": desktop.required_approvals_n,
        "approvalsSoFar": approvals_so_far,
        "sessionStatus": session_status,
    }
    
    # Include new secret key if rotated
    if rotated and new_key:
        response["newSecretKey"] = new_key
    
    return response


def update_desktop(db: Session, desktop_app_id: str, app_type: str, body: DesktopUpdateRequest) -> Desktop:
    # Filter by both desktop_app_id and app_type to target specific desktop
    desktop = db.query(Desktop).filter(
        Desktop.desktop_app_id == desktop_app_id,
        Desktop.app_type == app_type
    ).first()
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
    db.query(GovernanceAssignment).filter(GovernanceAssignment.desktop_id == desktop.id).delete()
    for user_id in authority_ids:
        db.add(GovernanceAssignment(user_id=user_id, desktop_id=desktop.id, desktop_app_id=desktop.desktop_app_id))
    db.commit()


def list_assigned_desktops(db: Session, user: User):
    rows = (
        db.query(Desktop)
        .join(GovernanceAssignment, GovernanceAssignment.desktop_id == Desktop.id)
        .filter(GovernanceAssignment.user_id == user.id)
        .all()
    )
    return rows


class DesktopService:
    """Service class for desktop certificate management"""
    
    def __init__(self, db: Session):
        self.db = db
    
    def submit_csr(self, desktop_app_id: str, csr_pem: str) -> Desktop:
        """
        Desktop submits Certificate Signing Request (CSR)
        
        Args:
            desktop_app_id: Desktop identifier
            csr_pem: Certificate Signing Request in PEM format
            
        Returns:
            Updated desktop with CSR stored
        """
        desktop = self.db.query(Desktop).filter(Desktop.desktop_app_id == desktop_app_id).first()
        if not desktop:
            raise ValueError(f"Desktop {desktop_app_id} not found")
        
        desktop.csr_pem = csr_pem
        desktop.csr_submitted = 1  # Boolean true
        
        self.db.add(desktop)
        self.db.commit()
        self.db.refresh(desktop)
        
        log_audit(
            self.db,
            action="CSR_SUBMITTED",
            desktop_app_id=desktop_app_id,
            details={"csr_length": len(csr_pem)}
        )
        
        return desktop
    
    def get_pending_certificate_requests(self):
        """Get all desktops that have submitted CSRs but not yet received certificates"""
        desktops = self.db.query(Desktop).filter(
            Desktop.csr_submitted == 1,
            Desktop.certificate_pem.is_(None)
        ).all()
        
        return [
            {
                "desktop_app_id": d.desktop_app_id,
                "name_label": d.name_label,
                "machine_name": d.machine_name,
                "os_user": d.os_user,
                "csr_submitted_at": d.last_seen_at_utc,
                "status": d.status
            }
            for d in desktops
        ]
    
    def sign_desktop_certificate(self, desktop_app_id: str) -> dict:
        """
        Admin approves and signs desktop CSR
        
        Args:
            desktop_app_id: Desktop identifier
            
        Returns:
            dict with certificate and expiration
        """
        desktop = self.db.query(Desktop).filter(Desktop.desktop_app_id == desktop_app_id).first()
        if not desktop:
            raise ValueError(f"Desktop {desktop_app_id} not found")
        
        if not desktop.csr_pem:
            raise ValueError(f"Desktop {desktop_app_id} has not submitted a CSR")
        
        # Get CA credentials
        ca_credentials = CAPersistenceService.get_ca_credentials(self.db)
        if not ca_credentials:
            raise ValueError("No CA certificate found. Admin must generate CA first.")
        
        ca_cert_pem, ca_key_pem = ca_credentials
        
        # Get CA expiration date
        ca_info = CAPersistenceService.get_ca_info(self.db)
        ca_expires_at = ca_info['expires_at']
        
        # Sign the CSR (desktop cert expires with CA)
        certificate_pem = CAService.sign_certificate(
            csr_pem=desktop.csr_pem.encode('utf-8'),
            ca_cert_pem=ca_cert_pem,
            ca_key_pem=ca_key_pem,
            desktop_app_id=desktop_app_id,
            validity_days=None  # Match CA expiration
        )
        
        # Store signed certificate
        desktop.certificate_pem = certificate_pem.decode('utf-8')
        desktop.certificate_issued_at = utcnow()
        desktop.certificate_expires_at = ca_expires_at
        
        self.db.add(desktop)
        self.db.commit()
        self.db.refresh(desktop)
        
        log_audit(
            self.db,
            action="CERTIFICATE_SIGNED",
            desktop_app_id=desktop_app_id,
            details={
                "issued_at": desktop.certificate_issued_at.isoformat(),
                "expires_at": desktop.certificate_expires_at.isoformat()
            }
        )
        
        return {
            "certificate": desktop.certificate_pem,
            "expires_at": desktop.certificate_expires_at
        }
    
    def reject_certificate_request(self, desktop_app_id: str):
        """
        Admin rejects desktop certificate request
        
        Args:
            desktop_app_id: Desktop identifier
        """
        desktop = self.db.query(Desktop).filter(Desktop.desktop_app_id == desktop_app_id).first()
        if not desktop:
            raise ValueError(f"Desktop {desktop_app_id} not found")
        
        # Clear CSR data
        desktop.csr_pem = None
        desktop.csr_submitted = 0
        
        self.db.add(desktop)
        self.db.commit()
        
        log_audit(
            self.db,
            action="CERTIFICATE_REJECTED",
            desktop_app_id=desktop_app_id,
            details={}
        )

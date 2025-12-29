"""
Key rotation service for desktop applications
"""
from datetime import datetime, timedelta, timezone
from sqlalchemy.orm import Session

from app.core.hmac_auth import generate_secret_key
from app.core.time import utcnow
from app.models import Desktop
from app.models.auth_log import AuthEventType
from app.services.auth_log_service import log_auth_attempt


# Key rotation policy: rotate keys older than 90 days
KEY_ROTATION_DAYS = 90


def _make_aware(dt: datetime) -> datetime:
    """Ensure datetime is timezone-aware (UTC)"""
    if dt is None:
        return None
    if dt.tzinfo is None:
        return dt.replace(tzinfo=timezone.utc)
    return dt


def should_rotate_key(desktop: Desktop) -> bool:
    """
    Determine if a desktop's secret key should be rotated.
    
    Args:
        desktop: Desktop object
        
    Returns:
        True if key should be rotated, False otherwise
    """
    if not desktop.secret_key:
        return True  # No key exists
    
    if not desktop.secret_key_rotated_at:
        # Key exists but never rotated - check creation date
        if desktop.created_at_utc:
            age = utcnow() - _make_aware(desktop.created_at_utc)
            return age.days >= KEY_ROTATION_DAYS
        return True
    
    # Check if key is older than rotation policy
    age = utcnow() - _make_aware(desktop.secret_key_rotated_at)
    return age.days >= KEY_ROTATION_DAYS


def rotate_desktop_key(db: Session, desktop: Desktop) -> str:
    """
    Rotate the secret key for a desktop application.
    
    Args:
        db: Database session
        desktop: Desktop object
        
    Returns:
        The new secret key (base64-encoded)
    """
    old_key = desktop.secret_key
    new_key = generate_secret_key()
    
    desktop.secret_key = new_key
    desktop.secret_key_rotated_at = utcnow()
    
    db.add(desktop)
    db.commit()
    db.refresh(desktop)
    
    # Log key rotation event
    log_auth_attempt(
        db=db,
        desktop_app_id=desktop.desktop_app_id,
        event_type=AuthEventType.KEY_ROTATION,
        success=True,
        error_message=f"Key rotated (age exceeded {KEY_ROTATION_DAYS} days)",
        machine_name=desktop.machine_name,
        os_user=desktop.os_user,
        token_control_version=desktop.token_control_version
    )
    
    return new_key


def check_and_rotate_if_needed(db: Session, desktop: Desktop) -> tuple[bool, str | None]:
    """
    Check if key rotation is needed and perform it if necessary.
    
    Args:
        db: Database session
        desktop: Desktop object
        
    Returns:
        Tuple of (rotated: bool, new_key: str | None)
        - rotated: True if key was rotated
        - new_key: The new secret key if rotated, None otherwise
    """
    if should_rotate_key(desktop):
        new_key = rotate_desktop_key(db, desktop)
        return (True, new_key)
    
    return (False, None)

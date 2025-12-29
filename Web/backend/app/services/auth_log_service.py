"""
Service for authentication audit logging
"""
import uuid
from datetime import datetime, timedelta
from typing import Optional, List
from sqlalchemy.orm import Session

from app.models.auth_log import AuthenticationLog, AuthEventType
from app.core.time import utcnow


def log_auth_attempt(
    db: Session,
    desktop_app_id: str,
    event_type: AuthEventType,
    success: bool,
    endpoint: Optional[str] = None,
    ip_address: Optional[str] = None,
    user_agent: Optional[str] = None,
    error_message: Optional[str] = None,
    machine_name: Optional[str] = None,
    os_user: Optional[str] = None,
    token_control_version: Optional[str] = None,
) -> AuthenticationLog:
    """
    Log an authentication attempt to the audit trail.
    
    Args:
        db: Database session
        desktop_app_id: Desktop application ID
        event_type: Type of authentication event
        success: Whether the authentication was successful
        endpoint: API endpoint being accessed
        ip_address: Client IP address
        user_agent: User agent string
        error_message: Error message if authentication failed
        machine_name: Desktop machine name
        os_user: OS user name
        token_control_version: Version of TokenControl app
        
    Returns:
        Created AuthenticationLog entry
    """
    log_entry = AuthenticationLog(
        id=str(uuid.uuid4()),
        desktop_app_id=desktop_app_id,
        event_type=event_type,
        success=success,
        endpoint=endpoint,
        ip_address=ip_address,
        user_agent=user_agent,
        error_message=error_message,
        machine_name=machine_name,
        os_user=os_user,
        token_control_version=token_control_version,
        timestamp_utc=utcnow()
    )
    
    db.add(log_entry)
    db.commit()
    db.refresh(log_entry)
    
    return log_entry


def get_auth_logs(
    db: Session,
    desktop_app_id: Optional[str] = None,
    event_type: Optional[AuthEventType] = None,
    success: Optional[bool] = None,
    start_date: Optional[datetime] = None,
    end_date: Optional[datetime] = None,
    limit: int = 100
) -> List[AuthenticationLog]:
    """
    Retrieve authentication logs with optional filters.
    
    Args:
        db: Database session
        desktop_app_id: Filter by desktop ID
        event_type: Filter by event type
        success: Filter by success status
        start_date: Filter by start date
        end_date: Filter by end date
        limit: Maximum number of records to return
        
    Returns:
        List of AuthenticationLog entries
    """
    query = db.query(AuthenticationLog)
    
    if desktop_app_id:
        query = query.filter(AuthenticationLog.desktop_app_id == desktop_app_id)
    
    if event_type:
        query = query.filter(AuthenticationLog.event_type == event_type)
    
    if success is not None:
        query = query.filter(AuthenticationLog.success == success)
    
    if start_date:
        query = query.filter(AuthenticationLog.timestamp_utc >= start_date)
    
    if end_date:
        query = query.filter(AuthenticationLog.timestamp_utc <= end_date)
    
    query = query.order_by(AuthenticationLog.timestamp_utc.desc())
    query = query.limit(limit)
    
    return query.all()


def get_failed_attempts_count(
    db: Session,
    desktop_app_id: str,
    minutes: int = 30
) -> int:
    """
    Count failed authentication attempts for a desktop in the last N minutes.
    Useful for rate limiting or detecting brute force attempts.
    
    Args:
        db: Database session
        desktop_app_id: Desktop application ID
        minutes: Time window in minutes
        
    Returns:
        Number of failed attempts
    """
    cutoff_time = utcnow() - timedelta(minutes=minutes)
    
    count = db.query(AuthenticationLog).filter(
        AuthenticationLog.desktop_app_id == desktop_app_id,
        AuthenticationLog.success == False,
        AuthenticationLog.timestamp_utc >= cutoff_time
    ).count()
    
    return count


def cleanup_old_logs(db: Session, days_to_keep: int = 90) -> int:
    """
    Delete authentication logs older than specified days.
    Should be run periodically to prevent unbounded growth.
    
    Args:
        db: Database session
        days_to_keep: Number of days to retain logs
        
    Returns:
        Number of deleted records
    """
    cutoff_date = utcnow() - timedelta(days=days_to_keep)
    
    deleted = db.query(AuthenticationLog).filter(
        AuthenticationLog.timestamp_utc < cutoff_date
    ).delete()
    
    db.commit()
    
    return deleted

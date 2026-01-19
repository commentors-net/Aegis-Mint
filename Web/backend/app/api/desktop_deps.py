"""
Dependencies for desktop authentication
"""
from fastapi import Header, HTTPException, Depends, status, Request
from sqlalchemy.orm import Session

from app.api.deps import get_db
from app.core.hmac_auth import validate_desktop_auth_headers
from app.models import Desktop
from app.models.auth_log import AuthEventType
from app.services.auth_log_service import log_auth_attempt


async def get_authenticated_desktop(
    request: Request,
    desktop_id: str = Header(None, alias="X-Desktop-Id"),
    app_type: str = Header(None, alias="X-App-Type"),
    timestamp: str = Header(None, alias="X-Desktop-Timestamp"),
    signature: str = Header(None, alias="X-Desktop-Signature"),
    user_agent: str = Header(None, alias="User-Agent"),
    db: Session = Depends(get_db)
) -> Desktop:
    """
    Dependency to authenticate desktop requests using HMAC signatures.
    Logs all authentication attempts to audit trail.
    
    Returns:
        The authenticated Desktop object
        
    Raises:
        HTTPException: If authentication fails
    """
    endpoint = str(request.url.path)
    client_ip = request.client.host if request.client else None
    
    if not desktop_id:
        log_auth_attempt(
            db=db,
            desktop_app_id=desktop_id or "unknown",
            event_type=AuthEventType.AUTH_FAILURE,
            success=False,
            endpoint=endpoint,
            ip_address=client_ip,
            user_agent=user_agent,
            error_message="Missing X-Desktop-Id header"
        )
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="Missing X-Desktop-Id header"
        )
    
    # Default to TokenControl for backward compatibility
    app_type_value = app_type or "TokenControl"
    
    # Get desktop from database - filter by both app_id and app_type
    desktop = db.query(Desktop).filter(
        Desktop.desktop_app_id == desktop_id,
        Desktop.app_type == app_type_value
    ).first()
    if not desktop:
        log_auth_attempt(
            db=db,
            desktop_app_id=desktop_id,
            event_type=AuthEventType.DESKTOP_NOT_FOUND,
            success=False,
            endpoint=endpoint,
            ip_address=client_ip,
            user_agent=user_agent,
            error_message="Desktop not found"
        )
        raise HTTPException(
            status_code=status.HTTP_404_NOT_FOUND,
            detail="Desktop not found"
        )
    
    if not desktop.secret_key:
        log_auth_attempt(
            db=db,
            desktop_app_id=desktop_id,
            event_type=AuthEventType.AUTH_FAILURE,
            success=False,
            endpoint=endpoint,
            ip_address=client_ip,
            user_agent=user_agent,
            error_message="Desktop secret key not configured",
            machine_name=desktop.machine_name,
            os_user=desktop.os_user,
            token_control_version=desktop.token_control_version
        )
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail="Desktop secret key not configured"
        )
    
    # Read body for POST requests
    body = ""
    if request.method == "POST":
        body_bytes = await request.body()
        body = body_bytes.decode('utf-8')
    
    # Validate signature
    try:
        validate_desktop_auth_headers(
            desktop_app_id=desktop_id,
            timestamp=timestamp or "",
            signature=signature or "",
            secret_key=desktop.secret_key,
            body=body
        )
        
        # Log successful authentication
        log_auth_attempt(
            db=db,
            desktop_app_id=desktop_id,
            event_type=AuthEventType.AUTH_SUCCESS,
            success=True,
            endpoint=endpoint,
            ip_address=client_ip,
            user_agent=user_agent,
            machine_name=desktop.machine_name,
            os_user=desktop.os_user,
            token_control_version=desktop.token_control_version
        )
        
    except HTTPException as e:
        # Determine specific error type
        event_type = AuthEventType.AUTH_FAILURE
        if "timestamp" in e.detail.lower():
            event_type = AuthEventType.TIMESTAMP_INVALID
        elif "signature" in e.detail.lower():
            event_type = AuthEventType.INVALID_SIGNATURE
        
        log_auth_attempt(
            db=db,
            desktop_app_id=desktop_id,
            event_type=event_type,
            success=False,
            endpoint=endpoint,
            ip_address=client_ip,
            user_agent=user_agent,
            error_message=e.detail,
            machine_name=desktop.machine_name,
            os_user=desktop.os_user,
            token_control_version=desktop.token_control_version
        )
        raise
    
    return desktop

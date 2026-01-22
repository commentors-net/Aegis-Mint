"""API endpoints for share operation logging from desktop applications."""
from typing import Optional

from fastapi import APIRouter, Depends, HTTPException, Request, status
from pydantic import BaseModel, Field
from sqlalchemy.orm import Session

from app.api.deps import get_db, verify_desktop_auth
from app.core.logging import logger
from app.models import Desktop, ShareOperationLog, ShareOperationType


router = APIRouter()


class ShareOperationLogRequest(BaseModel):
    """Request model for logging share operations."""
    operation_type: ShareOperationType = Field(..., description="Type of operation: Creation or Retrieval")
    success: bool = Field(..., description="Whether the operation succeeded")
    operation_stage: Optional[str] = Field(None, description="Current stage of operation")
    
    # Share metadata
    total_shares: Optional[int] = Field(None, description="Total number of shares")
    threshold: Optional[int] = Field(None, description="Minimum shares needed to recover")
    shares_used: Optional[int] = Field(None, description="Number of shares used (for retrieval)")
    
    # Context
    token_name: Optional[str] = Field(None, description="Token name (for creation)")
    token_address: Optional[str] = Field(None, description="Token contract address")
    network: Optional[str] = Field(None, description="Network name (e.g., sepolia, mainnet)")
    shares_path: Optional[str] = Field(None, description="Path where shares are stored (for creation)")
    
    # Error tracking
    error_message: Optional[str] = Field(None, description="Error message if operation failed")
    notes: Optional[str] = Field(None, description="Additional notes")


class ShareOperationLogResponse(BaseModel):
    """Response model after logging a share operation."""
    id: str
    success: bool
    message: str


@router.post("/log", response_model=ShareOperationLogResponse)
async def log_share_operation(
    request: Request,
    log_request: ShareOperationLogRequest,
    desktop: Desktop = Depends(verify_desktop_auth),
    db: Session = Depends(get_db),
):
    """
    Log a share operation (creation or retrieval) from a desktop application.
    
    This endpoint is called by AegisMint.Mint when shares are created,
    and by AegisMint.TokenControl when shares are retrieved for recovery.
    
    Requires desktop authentication (HMAC or certificate).
    """
    try:
        # Create log entry
        log_entry = ShareOperationLog(
            desktop_app_id=desktop.desktop_app_id,
            app_type=desktop.app_type,
            machine_name=desktop.machine_name,
            operation_type=log_request.operation_type,
            success=log_request.success,
            operation_stage=log_request.operation_stage,
            total_shares=log_request.total_shares,
            threshold=log_request.threshold,
            shares_used=log_request.shares_used,
            token_name=log_request.token_name,
            token_address=log_request.token_address,
            network=log_request.network,
            shares_path=log_request.shares_path,
            error_message=log_request.error_message,
            notes=log_request.notes,
        )
        
        db.add(log_entry)
        db.commit()
        db.refresh(log_entry)
        
        # Log to application logs as well
        log_msg = (
            f"Share {log_request.operation_type.value} - "
            f"Desktop: {desktop.desktop_app_id} ({desktop.app_type}), "
            f"Stage: {log_request.operation_stage}, "
            f"Success: {log_request.success}"
        )
        if log_request.token_name:
            log_msg += f", Token: {log_request.token_name}"
        if log_request.error_message:
            log_msg += f", Error: {log_request.error_message}"
            
        logger.info(log_msg)
        
        return ShareOperationLogResponse(
            id=log_entry.id,
            success=True,
            message="Share operation logged successfully"
        )
        
    except Exception as e:
        logger.error(f"Failed to log share operation: {str(e)}")
        db.rollback()
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail=f"Failed to log share operation: {str(e)}"
        )


@router.get("/logs")
async def get_share_operation_logs(
    desktop_app_id: Optional[str] = None,
    operation_type: Optional[ShareOperationType] = None,
    limit: int = 100,
    desktop: Desktop = Depends(verify_desktop_auth),
    db: Session = Depends(get_db),
):
    """
    Retrieve share operation logs.
    
    Filters:
    - desktop_app_id: Filter by specific desktop
    - operation_type: Filter by Creation or Retrieval
    - limit: Maximum number of records to return (default: 100, max: 500)
    """
    if limit > 500:
        limit = 500
        
    query = db.query(ShareOperationLog)
    
    if desktop_app_id:
        query = query.filter(ShareOperationLog.desktop_app_id == desktop_app_id)
    
    if operation_type:
        query = query.filter(ShareOperationLog.operation_type == operation_type)
    
    logs = query.order_by(ShareOperationLog.at_utc.desc()).limit(limit).all()
    
    return {
        "total": len(logs),
        "logs": [
            {
                "id": log.id,
                "at_utc": log.at_utc.isoformat() if log.at_utc else None,
                "desktop_app_id": log.desktop_app_id,
                "app_type": log.app_type,
                "machine_name": log.machine_name,
                "operation_type": log.operation_type.value,
                "success": log.success,
                "operation_stage": log.operation_stage,
                "total_shares": log.total_shares,
                "threshold": log.threshold,
                "shares_used": log.shares_used,
                "token_name": log.token_name,
                "token_address": log.token_address,
                "network": log.network,
                "shares_path": log.shares_path,
                "error_message": log.error_message,
                "notes": log.notes,
            }
            for log in logs
        ]
    }

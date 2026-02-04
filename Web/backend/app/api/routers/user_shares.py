"""API endpoints for user share download and history."""
import logging
from datetime import datetime
from typing import List, Optional

from fastapi import APIRouter, Depends, HTTPException, Request
from fastapi.responses import Response
from pydantic import BaseModel, Field
from sqlalchemy.orm import Session, joinedload

from app.api.deps import get_current_token_user, get_db
from app.core.time import utcnow
from app.models.share_assignment import ShareAssignment
from app.models.share_download_log import ShareDownloadLog
from app.models.share_file import ShareFile
from app.models.token_deployment import TokenDeployment
from app.models.token_user import TokenUser
from app.models.token_user_login_challenge import TokenUserLoginChallenge

logger = logging.getLogger(__name__)

router = APIRouter(prefix="/api/my-shares", tags=["user-shares"])


class MyShareResponse(BaseModel):
    """Response model for user's assigned share."""
    assignment_id: str
    share_file_id: str
    share_number: int
    token_name: str
    token_symbol: str
    token_address: str | None
    assigned_at_utc: datetime
    download_allowed: bool
    download_count: int
    first_downloaded_at_utc: Optional[datetime]
    last_downloaded_at_utc: Optional[datetime]
    assignment_notes: Optional[str]

    class Config:
        from_attributes = True


class DownloadHistoryItem(BaseModel):
    """Download history entry."""
    id: str
    assignment_id: str
    share_number: int
    token_name: str
    downloaded_at_utc: datetime
    ip_address: Optional[str]
    success: bool
    failure_reason: Optional[str]

    class Config:
        from_attributes = True


@router.get("", response_model=List[MyShareResponse])
def get_my_shares(
    db: Session = Depends(get_db),
    current_user: TokenUser = Depends(get_current_token_user)
):
    """
    Get list of all shares assigned to the current token user.
    
    Returns active assignments with download status and metadata.
    """
    logger.info(f"Token user {current_user.email} requesting their assigned shares")
    
    # Query assignments with related data
    assignments = (
        db.query(ShareAssignment)
        .filter(
            ShareAssignment.user_id == current_user.id,
            ShareAssignment.is_active.is_(True)
        )
        .options(
            joinedload(ShareAssignment.share_file).joinedload(ShareFile.token_deployment)
        )
        .all()
    )
    
    response = []
    for assignment in assignments:
        share_file = assignment.share_file
        token = share_file.token_deployment
        
        response.append(MyShareResponse(
            assignment_id=assignment.id,
            share_file_id=share_file.id,
            share_number=share_file.share_number,
            token_name=token.token_name,
            token_symbol=token.token_symbol,
            token_address=token.contract_address,
            assigned_at_utc=assignment.assigned_at_utc,
            download_allowed=assignment.download_allowed,
            download_count=assignment.download_count,
            first_downloaded_at_utc=assignment.first_downloaded_at_utc,
            last_downloaded_at_utc=assignment.last_downloaded_at_utc,
            assignment_notes=assignment.assignment_notes
        ))
    
    logger.info(f"Returning {len(response)} shares for user {current_user.email}")
    return response


@router.get("/download/{assignment_id}")
def download_share(
    assignment_id: str,
    request: Request,
    db: Session = Depends(get_db),
    current_user: TokenUser = Depends(get_current_token_user)
):
    """
    Download a share file assigned to the current token user.
    
    - Verifies assignment belongs to current user
    - Checks if download is allowed
    - Returns encrypted share content as JSON file
    - Auto-disables download after successful download (one-time by default)
    - Logs download attempt for audit trail
    
    Args:
        assignment_id: ID of the share assignment
        request: FastAPI request for IP/user-agent extraction
        db: Database session
        current_user: Authenticated token user
    
    Returns:
        JSON file with encrypted share content
    """
    logger.info(f"Token user {current_user.email} attempting to download assignment {assignment_id}")
    
    # Get client info for audit log
    ip_address = request.client.host if request.client else None
    user_agent = request.headers.get("user-agent")
    
    # Find assignment
    assignment = (
        db.query(ShareAssignment)
        .filter(
            ShareAssignment.id == assignment_id,
            ShareAssignment.user_id == current_user.id,
            ShareAssignment.is_active.is_(True)
        )
        .options(
            joinedload(ShareAssignment.share_file).joinedload(ShareFile.token_deployment)
        )
        .first()
    )
    
    if not assignment:
        logger.warning(f"Assignment {assignment_id} not found or not accessible by user {current_user.email}")
        
        # Log failed attempt
        log_entry = ShareDownloadLog(
            share_assignment_id=assignment_id,
            user_id=current_user.id,
            downloaded_at_utc=utcnow(),
            ip_address=ip_address,
            user_agent=user_agent,
            success=False,
            failure_reason="Assignment not found or not accessible"
        )
        db.add(log_entry)
        db.commit()
        
        raise HTTPException(
            status_code=404,
            detail="Share assignment not found or you don't have access"
        )
    
    # Check if download is allowed
    if not assignment.download_allowed:
        logger.warning(
            f"Download blocked for assignment {assignment_id} - "
            f"already downloaded {assignment.download_count} time(s)"
        )
        
        # Log failed attempt
        log_entry = ShareDownloadLog(
            share_assignment_id=assignment_id,
            user_id=current_user.id,
            downloaded_at_utc=utcnow(),
            ip_address=ip_address,
            user_agent=user_agent,
            success=False,
            failure_reason=f"Download already used. Downloaded {assignment.download_count} time(s)."
        )
        db.add(log_entry)
        db.commit()
        
        raise HTTPException(
            status_code=403,
            detail=f"Download not allowed. This share has already been downloaded {assignment.download_count} time(s). "
                   f"Contact admin to re-enable download if you lost the file."
        )
    
    share_file = assignment.share_file
    token = share_file.token_deployment
    
    try:
        # Update assignment tracking
        now = utcnow()
        assignment.download_count += 1
        assignment.last_downloaded_at_utc = now
        
        if assignment.download_count == 1:
            assignment.first_downloaded_at_utc = now
        
        # Auto-disable download after first download (one-time policy)
        assignment.download_allowed = False
        
        # Log successful download
        log_entry = ShareDownloadLog(
            share_assignment_id=assignment_id,
            user_id=None,  # Not a system user
            token_user_id=current_user.id,  # Token share user
            downloaded_at_utc=now,
            ip_address=ip_address,
            user_agent=user_agent,
            success=True,
            failure_reason=None
        )
        db.add(log_entry)
        db.commit()
        
        logger.info(
            f"User {current_user.email} successfully downloaded share #{share_file.share_number} "
            f"for token {token.token_name} (assignment {assignment_id})"
        )
        
        # Return share content as encrypted payload
        content_str = share_file.encrypted_content
        if not content_str:
            raise HTTPException(status_code=500, detail="Share content is empty")
        logger.info(f"Serving share #{share_file.share_number} as encrypted payload ({len(content_str)} bytes)")
        
        # Return share file as encrypted payload
        filename = f"{share_file.file_name}"
        
        return Response(
            content=content_str,
            media_type="application/octet-stream",
            headers={
                "Content-Disposition": f'attachment; filename="{filename}"',
                "X-Share-Number": str(share_file.share_number),
                "X-Token-Name": token.token_name,
                "X-Download-Count": str(assignment.download_count)
            }
        )
        
    except Exception as e:
        logger.error(f"Error during share download for assignment {assignment_id}: {str(e)}")
        db.rollback()
        
        # Log failed download
        log_entry = ShareDownloadLog(
            share_assignment_id=assignment_id,
            user_id=None,  # Not a system user
            token_user_id=current_user.id,  # Token share user
            downloaded_at_utc=utcnow(),
            ip_address=ip_address,
            user_agent=user_agent,
            success=False,
            failure_reason=f"Server error: {str(e)}"
        )
        db.add(log_entry)
        db.commit()
        
        raise HTTPException(
            status_code=500,
            detail="Failed to download share. Please try again or contact support."
        )


@router.get("/history", response_model=List[DownloadHistoryItem])
def get_download_history(
    db: Session = Depends(get_db),
    current_user: TokenUser = Depends(get_current_token_user)
):
    """
    Get download history for the current token user.
    
    Returns all download attempts (successful and failed) with timestamps and details.
    """
    logger.info(f"Token user {current_user.email} requesting download history")
    
    # Query download logs with related data
    logs = (
        db.query(ShareDownloadLog)
        .join(ShareAssignment, ShareDownloadLog.share_assignment_id == ShareAssignment.id)
        .join(ShareFile, ShareAssignment.share_file_id == ShareFile.id)
        .join(TokenDeployment, ShareFile.token_deployment_id == TokenDeployment.id)
        .filter(ShareDownloadLog.token_user_id == current_user.id)
        .order_by(ShareDownloadLog.downloaded_at_utc.desc())
        .all()
    )
    
    response = []
    for log in logs:
        assignment = log.assignment
        share_file = assignment.share_file
        token = share_file.token_deployment
        
        response.append(DownloadHistoryItem(
            id=log.id,
            assignment_id=log.share_assignment_id,
            share_number=share_file.share_number,
            token_name=token.token_name,
            downloaded_at_utc=log.downloaded_at_utc,
            ip_address=log.ip_address,
            success=log.success,
            failure_reason=log.failure_reason
        ))
    
    logger.info(f"Returning {len(response)} download log entries for user {current_user.email}")
    return response

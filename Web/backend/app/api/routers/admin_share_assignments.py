"""API endpoints for admin share assignment management."""
import logging
from datetime import datetime
from typing import List, Optional

from fastapi import APIRouter, Depends, HTTPException
from pydantic import BaseModel, Field
from sqlalchemy import func
from sqlalchemy.orm import Session, joinedload

from app.api.deps import get_current_user, get_db
from app.core.time import utcnow
from app.models.share_assignment import ShareAssignment
from app.models.share_file import ShareFile
from app.models.share_operation_log import ShareOperationLog, ShareOperationType
from app.models.token_deployment import TokenDeployment
from app.models.token_share_user import TokenShareUser
from app.models.user import User, UserRole

logger = logging.getLogger(__name__)

router = APIRouter(prefix="/api/admin/share-assignments", tags=["admin-share-assignments"])


class ShareAssignmentCreate(BaseModel):
    """Request model for creating a share assignment."""
    share_file_id: str = Field(..., description="ID of the share file to assign")
    user_id: str = Field(..., description="ID of the user to assign the share to")
    assignment_notes: Optional[str] = Field(None, description="Optional notes about the assignment")


class ShareAssignmentUpdate(BaseModel):
    """Request model for updating a share assignment."""
    download_allowed: Optional[bool] = Field(None, description="Enable/disable download")
    assignment_notes: Optional[str] = Field(None, description="Update assignment notes")
    is_active: Optional[bool] = Field(None, description="Activate/deactivate assignment")


class ShareAssignmentResponse(BaseModel):
    """Response model for share assignment."""
    id: str
    share_file_id: str
    user_id: str
    user_email: str
    assigned_by: str
    assigner_email: str
    assigned_at_utc: datetime
    is_active: bool
    download_allowed: bool
    download_count: int
    first_downloaded_at_utc: Optional[datetime]
    last_downloaded_at_utc: Optional[datetime]
    assignment_notes: Optional[str]
    
    # Share file details
    share_number: int
    file_name: str
    token_name: str
    token_symbol: str
    network: str
    contract_address: str

    class Config:
        from_attributes = True


class ShareAssignmentListItem(BaseModel):
    """Simplified response for list view."""
    id: str
    user_email: str
    share_number: int
    token_name: str
    network: str
    assigned_at_utc: datetime
    download_allowed: bool
    download_count: int
    is_active: bool


def _require_super_admin(current_user: User):
    """Verify user is SuperAdmin."""
    if current_user.role != UserRole.SUPER_ADMIN:
        raise HTTPException(
            status_code=403,
            detail="Only SuperAdmin can manage share assignments"
        )


@router.post("/", response_model=ShareAssignmentResponse)
def create_share_assignment(
    request: ShareAssignmentCreate,
    db: Session = Depends(get_db),
    current_user: User = Depends(get_current_user)
):
    """
    Assign a share file to a user (Admin only).
    
    Creates a new assignment allowing the specified user to download
    the share file. Only one share per user.
    """
    _require_super_admin(current_user)
    
    try:
        # Verify share file exists
        share_file = db.query(ShareFile).options(
            joinedload(ShareFile.token_deployment)
        ).filter(ShareFile.id == request.share_file_id).first()
        
        if not share_file:
            raise HTTPException(status_code=404, detail="Share file not found")
        
        # Verify user exists and is active
        target_user = db.query(TokenShareUser).filter(TokenShareUser.id == request.user_id).first()
        
        if not target_user:
            raise HTTPException(status_code=404, detail="User not found")
        
        if not target_user.is_active:
            raise HTTPException(status_code=400, detail="Cannot assign share to inactive user")
        
        # Check if share is already assigned to this user
        existing = db.query(ShareAssignment).filter(
            ShareAssignment.share_file_id == request.share_file_id,
            ShareAssignment.user_id == request.user_id
        ).first()
        
        if existing:
            raise HTTPException(
                status_code=400,
                detail=f"Share #{share_file.share_number} already assigned to {target_user.email}"
            )
        
        # Create assignment
        assignment = ShareAssignment(
            share_file_id=request.share_file_id,
            user_id=request.user_id,
            assigned_by=current_user.id,
            assigned_at_utc=utcnow(),
            is_active=True,
            download_allowed=True,
            download_count=0,
            assignment_notes=request.assignment_notes
        )
        
        db.add(assignment)
        db.commit()
        db.refresh(assignment)
        
        logger.info(
            f"[OK] Admin {current_user.email} assigned share #{share_file.share_number} "
            f"({share_file.token_deployment.token_name}) to {target_user.email}"
        )
        
        # Build response
        assigner = db.query(User).filter(User.id == current_user.id).first()
        response_data = {
            "id": assignment.id,
            "share_file_id": assignment.share_file_id,
            "user_id": assignment.user_id,
            "user_email": target_user.email,
            "assigned_by": current_user.id,
            "assigner_email": assigner.email if assigner else "Unknown",
            "assigned_at_utc": assignment.assigned_at_utc,
            "is_active": assignment.is_active,
            "download_allowed": assignment.download_allowed,
            "download_count": assignment.download_count,
            "first_downloaded_at_utc": assignment.first_downloaded_at_utc,
            "last_downloaded_at_utc": assignment.last_downloaded_at_utc,
            "assignment_notes": assignment.assignment_notes,
            "share_number": share_file.share_number,
            "file_name": share_file.file_name,
            "token_name": share_file.token_deployment.token_name,
            "token_symbol": share_file.token_deployment.token_symbol,
            "network": share_file.token_deployment.network,
            "contract_address": share_file.token_deployment.contract_address
        }
        
        return ShareAssignmentResponse(**response_data)
    
    except HTTPException:
        db.rollback()
        raise
    except Exception as e:
        db.rollback()
        logger.error(f"Failed to create share assignment: {e}", exc_info=True)
        raise HTTPException(status_code=500, detail=f"Failed to create assignment: {str(e)}")


@router.get("/", response_model=List[ShareAssignmentListItem])
def list_share_assignments(
    token_id: Optional[str] = None,
    user_id: Optional[str] = None,
    is_active: Optional[bool] = None,
    download_allowed: Optional[bool] = None,
    limit: int = 100,
    db: Session = Depends(get_db),
    current_user: User = Depends(get_current_user)
):
    """
    List share assignments with filters (Admin only).
    
    Query parameters:
    - token_id: Filter by token deployment ID
    - user_id: Filter by user ID
    - is_active: Filter by active status
    - download_allowed: Filter by download permission
    - limit: Max results (default 100)
    """
    _require_super_admin(current_user)
    
    try:
        query = db.query(ShareAssignment).options(
            joinedload(ShareAssignment.user),
            joinedload(ShareAssignment.share_file).joinedload(ShareFile.token_deployment)
        )
        
        if token_id:
            query = query.join(ShareFile).filter(ShareFile.token_deployment_id == token_id)
        
        if user_id:
            query = query.filter(ShareAssignment.user_id == user_id)
        
        if is_active is not None:
            query = query.filter(ShareAssignment.is_active == is_active)
        
        if download_allowed is not None:
            query = query.filter(ShareAssignment.download_allowed == download_allowed)
        
        assignments = query.order_by(ShareAssignment.assigned_at_utc.desc()).limit(limit).all()
        
        # Build simplified response
        result = []
        for assignment in assignments:
            item = ShareAssignmentListItem(
                id=assignment.id,
                user_email=assignment.user.email,
                share_number=assignment.share_file.share_number,
                token_name=assignment.share_file.token_deployment.token_name,
                network=assignment.share_file.token_deployment.network,
                assigned_at_utc=assignment.assigned_at_utc,
                download_allowed=assignment.download_allowed,
                download_count=assignment.download_count,
                is_active=assignment.is_active
            )
            result.append(item)
        
        return result
    
    except Exception as e:
        logger.error(f"Failed to list share assignments: {e}", exc_info=True)
        raise HTTPException(status_code=500, detail=f"Failed to list assignments: {str(e)}")


@router.get("/{assignment_id}", response_model=ShareAssignmentResponse)
def get_share_assignment(
    assignment_id: str,
    db: Session = Depends(get_db),
    current_user: User = Depends(get_current_user)
):
    """Get detailed information about a specific share assignment (Admin only)."""
    _require_super_admin(current_user)
    
    try:
        assignment = db.query(ShareAssignment).options(
            joinedload(ShareAssignment.user),
            joinedload(ShareAssignment.assigner),
            joinedload(ShareAssignment.share_file).joinedload(ShareFile.token_deployment)
        ).filter(ShareAssignment.id == assignment_id).first()
        
        if not assignment:
            raise HTTPException(status_code=404, detail="Share assignment not found")
        
        response_data = {
            "id": assignment.id,
            "share_file_id": assignment.share_file_id,
            "user_id": assignment.user_id,
            "user_email": assignment.user.email,
            "assigned_by": assignment.assigned_by,
            "assigner_email": assignment.assigner.email,
            "assigned_at_utc": assignment.assigned_at_utc,
            "is_active": assignment.is_active,
            "download_allowed": assignment.download_allowed,
            "download_count": assignment.download_count,
            "first_downloaded_at_utc": assignment.first_downloaded_at_utc,
            "last_downloaded_at_utc": assignment.last_downloaded_at_utc,
            "assignment_notes": assignment.assignment_notes,
            "share_number": assignment.share_file.share_number,
            "file_name": assignment.share_file.file_name,
            "token_name": assignment.share_file.token_deployment.token_name,
            "token_symbol": assignment.share_file.token_deployment.token_symbol,
            "network": assignment.share_file.token_deployment.network,
            "contract_address": assignment.share_file.token_deployment.contract_address
        }
        
        return ShareAssignmentResponse(**response_data)
    
    except HTTPException:
        raise
    except Exception as e:
        logger.error(f"Failed to get share assignment {assignment_id}: {e}", exc_info=True)
        raise HTTPException(status_code=500, detail=f"Failed to get assignment: {str(e)}")


@router.patch("/{assignment_id}", response_model=ShareAssignmentResponse)
def update_share_assignment(
    assignment_id: str,
    request: ShareAssignmentUpdate,
    db: Session = Depends(get_db),
    current_user: User = Depends(get_current_user)
):
    """
    Update share assignment (Admin only).
    
    Allows admin to:
    - Enable/disable downloads
    - Activate/deactivate assignment
    - Update notes
    """
    _require_super_admin(current_user)
    
    try:
        assignment = db.query(ShareAssignment).options(
            joinedload(ShareAssignment.user),
            joinedload(ShareAssignment.share_file).joinedload(ShareFile.token_deployment)
        ).filter(ShareAssignment.id == assignment_id).first()
        
        if not assignment:
            raise HTTPException(status_code=404, detail="Share assignment not found")
        
        # Track changes for logging
        changes = []
        
        if request.download_allowed is not None and request.download_allowed != assignment.download_allowed:
            old_value = assignment.download_allowed
            assignment.download_allowed = request.download_allowed
            changes.append(f"download_allowed: {old_value} → {request.download_allowed}")
        
        if request.is_active is not None and request.is_active != assignment.is_active:
            old_value = assignment.is_active
            assignment.is_active = request.is_active
            changes.append(f"is_active: {old_value} → {request.is_active}")
        
        if request.assignment_notes is not None:
            assignment.assignment_notes = request.assignment_notes
            changes.append("assignment_notes updated")
        
        if not changes:
            raise HTTPException(status_code=400, detail="No changes provided")
        
        # Log operation
        operation_log = ShareOperationLog(
            operation_type=ShareOperationType.SHARE_UPDATED,
            user_id=current_user.id,
            target_user_id=assignment.user_id,
            share_number=assignment.share_file.share_number,
            token_address=assignment.share_file.token_deployment.contract_address,
            details=f"Admin {current_user.email} updated assignment: {', '.join(changes)}"
        )
        db.add(operation_log)
        
        db.commit()
        db.refresh(assignment)
        
        logger.info(
            f"[OK] Admin {current_user.email} updated assignment {assignment_id}: {', '.join(changes)}"
        )
        
        # Build response
        response_data = {
            "id": assignment.id,
            "share_file_id": assignment.share_file_id,
            "user_id": assignment.user_id,
            "user_email": assignment.user.email,
            "assigned_by": assignment.assigned_by,
            "assigner_email": assignment.assigner.email,
            "assigned_at_utc": assignment.assigned_at_utc,
            "is_active": assignment.is_active,
            "download_allowed": assignment.download_allowed,
            "download_count": assignment.download_count,
            "first_downloaded_at_utc": assignment.first_downloaded_at_utc,
            "last_downloaded_at_utc": assignment.last_downloaded_at_utc,
            "assignment_notes": assignment.assignment_notes,
            "share_number": assignment.share_file.share_number,
            "file_name": assignment.share_file.file_name,
            "token_name": assignment.share_file.token_deployment.token_name,
            "token_symbol": assignment.share_file.token_deployment.token_symbol,
            "network": assignment.share_file.token_deployment.network,
            "contract_address": assignment.share_file.token_deployment.contract_address
        }
        
        return ShareAssignmentResponse(**response_data)
    
    except HTTPException:
        db.rollback()
        raise
    except Exception as e:
        db.rollback()
        logger.error(f"Failed to update share assignment {assignment_id}: {e}", exc_info=True)
        raise HTTPException(status_code=500, detail=f"Failed to update assignment: {str(e)}")


@router.delete("/{assignment_id}")
def delete_share_assignment(
    assignment_id: str,
    db: Session = Depends(get_db),
    current_user: User = Depends(get_current_user)
):
    """
    Delete (unassign) a share assignment (Admin only).
    
    Removes the assignment, making the share available for reassignment.
    """
    _require_super_admin(current_user)
    
    try:
        assignment = db.query(ShareAssignment).options(
            joinedload(ShareAssignment.user),
            joinedload(ShareAssignment.share_file).joinedload(ShareFile.token_deployment)
        ).filter(ShareAssignment.id == assignment_id).first()
        
        if not assignment:
            raise HTTPException(status_code=404, detail="Share assignment not found")
        
        # Store info for logging before deletion
        user_email = assignment.user.email
        share_number = assignment.share_file.share_number
        
        # Delete assignment (download logs will cascade delete)
        db.delete(assignment)
        db.commit()
        
        logger.info(
            f"[OK] Admin {current_user.email} unassigned share #{share_number} from {user_email}"
        )
        
        return {"success": True, "message": f"Share assignment deleted successfully"}
    
    except HTTPException:
        db.rollback()
        raise
    except Exception as e:
        db.rollback()
        logger.error(f"Failed to delete share assignment {assignment_id}: {e}", exc_info=True)
        raise HTTPException(status_code=500, detail=f"Failed to delete assignment: {str(e)}")

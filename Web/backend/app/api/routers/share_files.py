"""API endpoints for share file management (bulk upload from desktop app)."""
import logging
from datetime import datetime
from typing import List

from fastapi import APIRouter, Depends, HTTPException, Request
from pydantic import BaseModel, Field
from sqlalchemy.orm import Session, joinedload

from app.api.deps import get_db
from app.api.desktop_deps import get_authenticated_desktop
from app.core.time import utcnow
from app.models.share_assignment import ShareAssignment
from app.models.share_file import ShareFile
from app.models.token_deployment import TokenDeployment

logger = logging.getLogger(__name__)

router = APIRouter(prefix="/api/share-files", tags=["share-files"])


class ShareFileItem(BaseModel):
    """Individual share file in bulk upload."""
    share_number: int = Field(..., ge=1, description="Share number (1, 2, 3, etc.)")
    file_name: str = Field(..., min_length=1, max_length=255, description="Original filename")
    encrypted_content: str = Field(..., min_length=1, description="Base64 encrypted share payload")
    encryption_key_id: str | None = Field(None, max_length=128, description="Key ID used for encryption")


class ShareFilesBulkCreate(BaseModel):
    """Request model for bulk uploading share files after token deployment."""
    token_deployment_id: str = Field(..., description="ID of the token deployment")
    shares: List[ShareFileItem] = Field(..., min_items=1, description="List of share files")
    replace_existing: bool = Field(False, description="Replace existing share files for this deployment")
    total_shares: int | None = Field(None, ge=2, description="New total share count when replacing")
    gov_threshold: int | None = Field(None, ge=1, description="New threshold when replacing")
    gov_shares: int | None = Field(None, ge=1, description="New governance share count when replacing")
    client_share_count: int | None = Field(None, ge=0, description="New client share count when replacing")
    safekeeping_share_count: int | None = Field(None, ge=1, description="New safekeeping share count when replacing")
    shares_path: str | None = Field(None, max_length=512, description="New shares path when replacing")


class AssignedToInfo(BaseModel):
    """Assignment information for a share."""
    assignment_id: str
    user_id: str
    user_name: str
    user_email: str
    download_allowed: bool
    download_count: int
    
    class Config:
        from_attributes = True


class ShareFileResponse(BaseModel):
    """Response model for a share file."""
    id: str
    token_deployment_id: str
    share_number: int
    file_name: str
    encryption_key_id: str | None
    created_at_utc: datetime
    is_assigned: bool = False
    assigned_to: AssignedToInfo | None = None

    class Config:
        from_attributes = True


class ShareFilesBulkResponse(BaseModel):
    """Response model for bulk upload."""
    created_count: int
    share_file_ids: List[str]
    token_deployment_id: str


class ShareFileValidationItem(BaseModel):
    """Share file to validate against active records."""
    file_name: str = Field(..., min_length=1, max_length=255)
    encrypted_content: str = Field(..., min_length=1)


class ShareFilesValidationRequest(BaseModel):
    """Request model for validating share file activity."""
    shares: List[ShareFileValidationItem] = Field(..., min_items=1)


class ShareFileValidationResult(BaseModel):
    """Validation result for a share file."""
    file_name: str
    is_active: bool
    reason: str | None = None


class ShareFilesValidationResponse(BaseModel):
    """Response model for share validation."""
    results: List[ShareFileValidationResult]


@router.post("/bulk", response_model=ShareFilesBulkResponse)
async def create_share_files_bulk(
    payload: ShareFilesBulkCreate,
    request: Request,
    db: Session = Depends(get_db)
):
    """
    Bulk upload share files after token deployment.
    
    Called by the desktop application after minting a token to store
    individual share files separately in the database.
    """
    try:
        # Verify token deployment exists
        deployment = db.query(TokenDeployment).filter(
            TokenDeployment.id == payload.token_deployment_id
        ).first()
        
        if not deployment:
            raise HTTPException(status_code=404, detail=f"Token deployment {request.token_deployment_id} not found")
        
        # Check if shares already uploaded
        if deployment.shares_uploaded and not payload.replace_existing:
            raise HTTPException(
                status_code=400,
                detail=f"Shares already uploaded for this deployment at {deployment.upload_completed_at_utc}"
            )

        if payload.replace_existing:
            await get_authenticated_desktop(
                request=request,
                desktop_id=request.headers.get("X-Desktop-Id"),
                app_type=request.headers.get("X-App-Type"),
                timestamp=request.headers.get("X-Desktop-Timestamp"),
                signature=request.headers.get("X-Desktop-Signature"),
                user_agent=request.headers.get("User-Agent"),
                db=db
            )

            missing = []
            if payload.total_shares is None:
                missing.append("total_shares")
            if payload.gov_threshold is None:
                missing.append("gov_threshold")
            if payload.gov_shares is None:
                missing.append("gov_shares")
            if payload.client_share_count is None:
                missing.append("client_share_count")
            if payload.safekeeping_share_count is None:
                missing.append("safekeeping_share_count")
            if missing:
                raise HTTPException(
                    status_code=400,
                    detail=f"Missing required fields for replace_existing: {', '.join(missing)}"
                )
        
        # Validate share numbers are sequential and match expected count
        share_numbers = sorted([s.share_number for s in payload.shares])
        expected_numbers = list(range(1, len(payload.shares) + 1))
        
        if share_numbers != expected_numbers:
            raise HTTPException(
                status_code=400,
                detail=f"Share numbers must be sequential starting from 1. Got: {share_numbers}"
            )
        
        # Validate count matches deployment configuration
        expected_total_shares = payload.total_shares if payload.replace_existing else deployment.total_shares
        if len(payload.shares) != expected_total_shares:
            raise HTTPException(
                status_code=400,
                detail=f"Expected {expected_total_shares} shares but got {len(payload.shares)}"
            )
        
        # Create share file records
        created_share_ids = []
        now = utcnow()
        
        if payload.replace_existing:
            existing_shares = (
                db.query(ShareFile)
                .options(joinedload(ShareFile.assignments))
                .filter(
                    ShareFile.token_deployment_id == payload.token_deployment_id,
                    ShareFile.is_active.is_(True)
                )
                .all()
            )
            for existing_share in existing_shares:
                existing_share.is_active = False
                existing_share.replaced_at_utc = now
                for assignment in existing_share.assignments:
                    assignment.is_active = False
                    assignment.download_allowed = False

            deployment.total_shares = payload.total_shares
            deployment.gov_threshold = payload.gov_threshold
            deployment.gov_shares = payload.gov_shares
            deployment.client_share_count = payload.client_share_count
            deployment.safekeeping_share_count = payload.safekeeping_share_count
            if payload.shares_path:
                deployment.shares_path = payload.shares_path
            
            # Flush to persist the is_active=False updates before creating new shares
            db.flush()

        for share_item in payload.shares:
            # Check for duplicate share number (only active shares)
            if not payload.replace_existing:
                existing = db.query(ShareFile).filter(
                    ShareFile.token_deployment_id == payload.token_deployment_id,
                    ShareFile.share_number == share_item.share_number,
                    ShareFile.is_active.is_(True)
                ).first()
                
                if existing:
                    raise HTTPException(
                        status_code=400,
                        detail=f"Share #{share_item.share_number} already exists"
                    )
            
            stored_content = share_item.encrypted_content

            share_file = ShareFile(
                token_deployment_id=payload.token_deployment_id,
                share_number=share_item.share_number,
                file_name=share_item.file_name,
                encrypted_content=stored_content,
                encryption_key_id=share_item.encryption_key_id,
                created_at_utc=now,
                is_active=True
            )
            
            db.add(share_file)
            db.flush()  # Get the ID
            created_share_ids.append(share_file.id)
        
        # Update token deployment status
        deployment.shares_uploaded = True
        deployment.upload_completed_at_utc = now
        deployment.share_files_count = len(payload.shares)
        
        db.commit()
        
        logger.info(
            f"[OK] Bulk uploaded {len(payload.shares)} share files for token {deployment.token_name} "
            f"(deployment_id: {payload.token_deployment_id})"
        )
        
        return ShareFilesBulkResponse(
            created_count=len(created_share_ids),
            share_file_ids=created_share_ids,
            token_deployment_id=payload.token_deployment_id
        )
    
    except HTTPException:
        db.rollback()
        raise
    except Exception as e:
        db.rollback()
        logger.error(f"Failed to bulk upload share files: {e}", exc_info=True)
        raise HTTPException(status_code=500, detail=f"Failed to upload share files: {str(e)}")


@router.post("/validate", response_model=ShareFilesValidationResponse)
async def validate_share_files(
    payload: ShareFilesValidationRequest,
    request: Request,
    db: Session = Depends(get_db)
):
    """
    Validate that provided share files are active in the database.

    Requires desktop authentication.
    """
    await get_authenticated_desktop(
        request=request,
        desktop_id=request.headers.get("X-Desktop-Id"),
        app_type=request.headers.get("X-App-Type"),
        timestamp=request.headers.get("X-Desktop-Timestamp"),
        signature=request.headers.get("X-Desktop-Signature"),
        user_agent=request.headers.get("User-Agent"),
        db=db
    )

    contents = [item.encrypted_content for item in payload.shares]
    matches = (
        db.query(ShareFile)
        .filter(ShareFile.encrypted_content.in_(contents))
        .all()
    )

    status_map: dict[str, dict[str, bool]] = {}
    for share in matches:
        entry = status_map.setdefault(share.encrypted_content, {"active": False, "inactive": False})
        if share.is_active:
            entry["active"] = True
        else:
            entry["inactive"] = True

    results: List[ShareFileValidationResult] = []
    for item in payload.shares:
        status = status_map.get(item.encrypted_content)
        if status is None:
            results.append(ShareFileValidationResult(file_name=item.file_name, is_active=False, reason="not_found"))
        elif status["active"]:
            results.append(ShareFileValidationResult(file_name=item.file_name, is_active=True, reason=None))
        else:
            results.append(ShareFileValidationResult(file_name=item.file_name, is_active=False, reason="inactive"))

    return ShareFilesValidationResponse(results=results)


@router.get("/token/{token_deployment_id}", response_model=List[ShareFileResponse])
def get_token_share_files(
    token_deployment_id: str,
    db: Session = Depends(get_db)
):
    """
    Get all share files for a specific token deployment.
    
    Returns list of shares with assignment status.
    Used by admin interface to see which shares are assigned.
    """
    try:
        # Verify deployment exists
        deployment = db.query(TokenDeployment).filter(
            TokenDeployment.id == token_deployment_id
        ).first()
        
        if not deployment:
            raise HTTPException(status_code=404, detail="Token deployment not found")
        
        # Get all share files with assignment status
        shares = db.query(ShareFile).options(
            joinedload(ShareFile.assignments).joinedload(ShareAssignment.user)
        ).filter(
            ShareFile.token_deployment_id == token_deployment_id,
            ShareFile.is_active.is_(True)
        ).order_by(ShareFile.share_number).all()
        
        # Build response with assignment status
        result = []
        for share in shares:
            # Get active assignment if exists
            active_assignment = None
            for assignment in share.assignments:
                if assignment.is_active:
                    active_assignment = assignment
                    break
            
            is_assigned = active_assignment is not None
            assigned_to = None
            
            if active_assignment:
                assigned_to = AssignedToInfo(
                    assignment_id=active_assignment.id,
                    user_id=active_assignment.user.id,
                    user_name=active_assignment.user.name,
                    user_email=active_assignment.user.email,
                    download_allowed=active_assignment.download_allowed,
                    download_count=active_assignment.download_count
                )
            
            share_dict = {
                "id": share.id,
                "token_deployment_id": share.token_deployment_id,
                "share_number": share.share_number,
                "file_name": share.file_name,
                "encryption_key_id": share.encryption_key_id,
                "created_at_utc": share.created_at_utc,
                "is_assigned": is_assigned,
                "assigned_to": assigned_to
            }
            result.append(ShareFileResponse(**share_dict))
        
        return result
    
    except HTTPException:
        raise
    except Exception as e:
        logger.error(f"Failed to get share files for token {token_deployment_id}: {e}", exc_info=True)
        raise HTTPException(status_code=500, detail=f"Failed to retrieve share files: {str(e)}")

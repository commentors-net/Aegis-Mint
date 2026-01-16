"""Router for share recovery endpoints."""
import json
from typing import List

from fastapi import APIRouter, Depends, File, HTTPException, Request, UploadFile, status
from pydantic import BaseModel
from sqlalchemy.orm import Session

from app.api.deps import get_db, require_role
from app.models import ShareRecoveryLog, User, UserRole
from app.services import share_recovery_service

router = APIRouter(prefix="/admin/shares", tags=["admin-shares"])


class RecoveryResponse(BaseModel):
    """Response for share recovery."""
    mnemonic: str
    token_address: str | None = None


@router.post("/recover", response_model=RecoveryResponse)
async def recover_from_shares(
    request: Request,
    files: List[UploadFile] = File(..., description="2-3 share files"),
    user: User = Depends(require_role(UserRole.SUPER_ADMIN)),
    db: Session = Depends(get_db),
):
    """
    Recover mnemonic from encrypted Shamir shares.
    
    Requires SuperAdmin role. All attempts are logged.
    """
    if len(files) < 2:
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail="At least 2 share files are required"
        )
    
    if len(files) > 3:
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail="Maximum 3 share files allowed"
        )
    
    # Get client IP for audit
    client_ip = request.client.host if request.client else None
    
    share_data_list = []
    error_msg = None
    success = False
    result = None
    token_address = None
    
    try:
        # Read and parse all share files
        for idx, file in enumerate(files):
            if not file.filename or not file.filename.endswith('.json'):
                raise HTTPException(
                    status_code=status.HTTP_400_BAD_REQUEST,
                    detail=f"File {idx + 1}: Must be a JSON file"
                )
            
            content = await file.read()
            try:
                share_data = json.loads(content)
            except json.JSONDecodeError:
                raise HTTPException(
                    status_code=status.HTTP_400_BAD_REQUEST,
                    detail=f"File {idx + 1}: Invalid JSON format"
                )
            
            share_data_list.append(share_data)
        
        # Reconstruct mnemonic from shares
        result = share_recovery_service.reconstruct_from_shares(share_data_list)
        token_address = result.get("token_address")
        success = True
        
    except HTTPException as e:
        error_msg = e.detail
        raise
    except Exception as e:
        error_msg = str(e)
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail=f"Share recovery failed: {str(e)}"
        )
    finally:
        # Log the attempt
        log_entry = ShareRecoveryLog(
            user_id=user.id,
            success=success,
            num_shares=str(len(files)),
            token_address=token_address,
            error_message=error_msg,
            ip_address=client_ip,
        )
        db.add(log_entry)
        db.commit()
    
    return RecoveryResponse(
        mnemonic=result["mnemonic"],
        token_address=result.get("token_address"),
    )


@router.get("/recovery-logs")
async def get_recovery_logs(
    limit: int = 50,
    user: User = Depends(require_role(UserRole.SUPER_ADMIN)),
    db: Session = Depends(get_db),
):
    """Get recent share recovery attempts."""
    logs = (
        db.query(ShareRecoveryLog)
        .order_by(ShareRecoveryLog.at_utc.desc())
        .limit(limit)
        .all()
    )
    return logs

"""API router for token share user management (redesigned for User + TokenUserAssignment)."""
import logging
import uuid
from datetime import datetime
from typing import List

from fastapi import APIRouter, Depends, HTTPException, Query
from pydantic import BaseModel, EmailStr, Field, field_validator
from sqlalchemy.orm import Session

from app.api.deps import get_db, require_role
from app.core import security
from app.models import TokenUser, TokenUserAssignment, TokenDeployment, UserRole

logger = logging.getLogger(__name__)

router = APIRouter(prefix="/api/token-share-users", tags=["token-share-users"])


class TokenShareUserCreate(BaseModel):
    """Request model for creating a token share user (or assigning existing user to token)."""
    token_deployment_id: str = Field(..., min_length=1)
    name: str = Field(..., min_length=1, max_length=255)
    email: EmailStr
    phone: str | None = Field(None, max_length=20)
    password: str = Field(..., min_length=8)


class TokenShareUserUpdate(BaseModel):
    """Request model for updating a token share user."""
    name: str | None = Field(None, min_length=1, max_length=255)
    email: EmailStr | None = None
    phone: str | None = Field(None, max_length=20)
    password: str | None = Field(None, min_length=8)


class TokenShareUserResponse(BaseModel):
    """Response model for token share user (includes assignment info)."""
    id: str  # User ID
    assignment_id: str  # Token assignment ID
    token_deployment_id: str
    name: str
    email: str
    phone: str | None
    created_at_utc: str
    mfa_enabled: bool

    @field_validator('created_at_utc', mode='before')
    @classmethod
    def serialize_datetime(cls, v):
        if isinstance(v, datetime):
            return v.isoformat()
        return v

    class Config:
        from_attributes = True


@router.get("/check-email/{email}")
def check_email_exists(
    email: str,
    token_deployment_id: str = Query(...),
    db: Session = Depends(get_db),
    _=Depends(require_role(UserRole.SUPER_ADMIN))
):
    """
    Check if an email already exists (globally or for specific token).
    Returns user info and assigned tokens.
    """
    logger.info(f"Checking if email '{email}' exists (token_deployment_id={token_deployment_id})")
    
    # Check if user exists
    user = db.query(TokenUser).filter(TokenUser.email == email).first()
    
    if not user:
        return {"exists": False, "tokens": []}
    
    # Check if already assigned to this specific token
    existing_assignment = db.query(TokenUserAssignment).filter(
        TokenUserAssignment.user_id == user.id,
        TokenUserAssignment.token_deployment_id == token_deployment_id
    ).first()
    
    if existing_assignment:
        return {
            "exists": True,
            "already_assigned": True,
            "tokens": [],
            "message": f"User {email} is already assigned to this token"
        }
    
    # Get all token assignments for this user
    assignments = db.query(TokenUserAssignment).filter(
        TokenUserAssignment.user_id == user.id
    ).all()
    
    tokens = []
    for assignment in assignments:
        token = db.query(TokenDeployment).filter(TokenDeployment.id == assignment.token_deployment_id).first()
        if token:
            tokens.append({
                "token_id": token.id,
                "token_name": token.token_name,
                "contract_address": token.contract_address
            })
    
    logger.info(f"User '{email}' exists with {len(tokens)} token assignments")
    return {
        "exists": True,
        "already_assigned": False,
        "tokens": tokens
    }


@router.post("/", response_model=TokenShareUserResponse)
def create_token_share_user(
    body: TokenShareUserCreate,
    db: Session = Depends(get_db),
    _=Depends(require_role(UserRole.SUPER_ADMIN))
):
    """
    Create a new user or assign existing user to a token.
    If email exists, assigns existing user to the token (with confirmation from frontend).
    If email doesn't exist, creates new user and assigns to token.
    """
    try:
        # Verify token deployment exists
        token_deployment = db.query(TokenDeployment).filter(
            TokenDeployment.id == body.token_deployment_id
        ).first()
        
        if not token_deployment:
            raise HTTPException(status_code=404, detail="Token deployment not found")
        
        # Check if user already exists
        user = db.query(TokenUser).filter(TokenUser.email == body.email).first()
        
        if user:
            # User exists - check if already assigned to this token
            existing_assignment = db.query(TokenUserAssignment).filter(
                TokenUserAssignment.user_id == user.id,
                TokenUserAssignment.token_deployment_id == body.token_deployment_id
            ).first()
            
            if existing_assignment:
                raise HTTPException(
                    status_code=400,
                    detail=f"User with email {body.email} is already assigned to this token"
                )
            
            # Create new assignment for existing user
            assignment = TokenUserAssignment(
                id=str(uuid.uuid4()),
                user_id=user.id,
                token_deployment_id=body.token_deployment_id
            )
            db.add(assignment)
            db.commit()
            db.refresh(assignment)
            
            logger.info(f"Assigned existing user {user.email} (id={user.id}) to token {token_deployment.token_name}")
            
            # Return user with assignment info
            return TokenShareUserResponse(
                id=user.id,
                assignment_id=assignment.id,
                token_deployment_id=body.token_deployment_id,
                name=user.name,
                email=user.email,
                phone=user.phone,
                created_at_utc=user.created_at.isoformat(),
                mfa_enabled=user.mfa_enabled
            )
        else:
            # User doesn't exist - create new user and assignment
            user = TokenUser(
                id=str(uuid.uuid4()),
                name=body.name,
                email=body.email,
                phone=body.phone,
                password_hash=security.hash_password(body.password),
                mfa_enabled=False
            )
            db.add(user)
            db.flush()  # Get user.id for assignment
            
            assignment = TokenUserAssignment(
                id=str(uuid.uuid4()),
                user_id=user.id,
                token_deployment_id=body.token_deployment_id
            )
            db.add(assignment)
            db.commit()
            db.refresh(user)
            db.refresh(assignment)
            
            logger.info(f"Created new user {user.email} (id={user.id}) and assigned to token {token_deployment.token_name}")
            
            return TokenShareUserResponse(
                id=user.id,
                assignment_id=assignment.id,
                token_deployment_id=body.token_deployment_id,
                name=user.name,
                email=user.email,
                phone=user.phone,
                created_at_utc=user.created_at.isoformat(),
                mfa_enabled=user.mfa_enabled
            )
    
    except HTTPException:
        raise
    except Exception as e:
        db.rollback()
        logger.error(f"Failed to create/assign token share user: {e}", exc_info=True)
        raise HTTPException(status_code=500, detail=f"Failed to create/assign user: {str(e)}")


@router.get("/token/{token_deployment_id}", response_model=List[TokenShareUserResponse])
def list_token_share_users(
    token_deployment_id: str,
    db: Session = Depends(get_db),
    _=Depends(require_role(UserRole.SUPER_ADMIN))
):
    """Get all users assigned to a specific token deployment."""
    try:
        # Get all assignments for this token
        assignments = db.query(TokenUserAssignment).filter(
            TokenUserAssignment.token_deployment_id == token_deployment_id
        ).all()
        
        result = []
        for assignment in assignments:
            user = db.query(TokenUser).filter(TokenUser.id == assignment.user_id).first()
            if user:
                result.append(TokenShareUserResponse(
                    id=user.id,
                    assignment_id=assignment.id,
                    token_deployment_id=token_deployment_id,
                    name=user.name,
                    email=user.email,
                    phone=user.phone,
                    created_at_utc=user.created_at.isoformat(),
                    mfa_enabled=user.mfa_enabled
                ))
        
        # Sort by name
        result.sort(key=lambda x: x.name)
        
        return result
    
    except Exception as e:
        logger.error(f"Failed to list token share users: {e}", exc_info=True)
        raise HTTPException(status_code=500, detail=f"Failed to retrieve users: {str(e)}")


@router.put("/{user_id}", response_model=TokenShareUserResponse)
def update_token_share_user(
    user_id: str,
    body: TokenShareUserUpdate,
    token_deployment_id: str = Query(..., description="Token deployment ID for response context"),
    db: Session = Depends(get_db),
    _=Depends(require_role(UserRole.SUPER_ADMIN))
):
    """Update a user's information. Changes apply to all their token assignments."""
    try:
        user = db.query(TokenUser).filter(TokenUser.id == user_id).first()
        
        if not user:
            raise HTTPException(status_code=404, detail="User not found")
        
        # Check for duplicate email if email is being changed
        if body.email and body.email != user.email:
            existing = db.query(TokenUser).filter(
                TokenUser.email == body.email,
                TokenUser.id != user_id
            ).first()
            
            if existing:
                raise HTTPException(
                    status_code=400,
                    detail=f"User with email {body.email} already exists"
                )
        
        # Update fields
        if body.name is not None:
            user.name = body.name
        if body.email is not None:
            user.email = body.email
        if body.phone is not None:
            user.phone = body.phone
        if body.password is not None:
            user.password_hash = security.hash_password(body.password)
        
        db.add(user)
        db.commit()
        db.refresh(user)
        
        # Get assignment for response
        assignment = db.query(TokenUserAssignment).filter(
            TokenUserAssignment.user_id == user_id,
            TokenUserAssignment.token_deployment_id == token_deployment_id
        ).first()
        
        if not assignment:
            # User updated but not assigned to this token - create dummy assignment for response
            assignment_id = "N/A"
        else:
            assignment_id = assignment.id
        
        logger.info(f"Updated user {user.id}")
        
        return TokenShareUserResponse(
            id=user.id,
            assignment_id=assignment_id,
            token_deployment_id=token_deployment_id,
            name=user.name,
            email=user.email,
            phone=user.phone,
            created_at_utc=user.created_at.isoformat(),
            mfa_enabled=user.mfa_enabled
        )
    
    except HTTPException:
        raise
    except Exception as e:
        db.rollback()
        logger.error(f"Failed to update token share user: {e}", exc_info=True)
        raise HTTPException(status_code=500, detail=f"Failed to update user: {str(e)}")


@router.delete("/{assignment_id}")
def delete_token_share_user_assignment(
    assignment_id: str,
    db: Session = Depends(get_db),
    _=Depends(require_role(UserRole.SUPER_ADMIN))
):
    """
    Remove a user's assignment to a token.
    If this is the user's last assignment, the user record is also deleted.
    """
    try:
        # Find the assignment
        assignment = db.query(TokenUserAssignment).filter(
            TokenUserAssignment.id == assignment_id
        ).first()
        
        if not assignment:
            raise HTTPException(status_code=404, detail="Assignment not found")
        
        user_id = assignment.user_id
        
        # Delete the assignment
        db.delete(assignment)
        db.commit()
        
        # Check if user has any other assignments
        remaining_assignments = db.query(TokenUserAssignment).filter(
            TokenUserAssignment.user_id == user_id
        ).count()
        
        if remaining_assignments == 0:
            # No more assignments - delete the user
            user = db.query(TokenUser).filter(TokenUser.id == user_id).first()
            if user:
                db.delete(user)
                db.commit()
                logger.info(f"Deleted user {user_id} (no remaining token assignments)")
            else:
                logger.warning(f"User {user_id} not found during cleanup")
        else:
            logger.info(f"Removed assignment {assignment_id} (user has {remaining_assignments} remaining assignments)")
        
        return {"message": "Assignment removed successfully"}
    
    except HTTPException:
        raise
    except Exception as e:
        db.rollback()
        logger.error(f"Failed to delete token share user assignment: {e}", exc_info=True)
        raise HTTPException(status_code=500, detail=f"Failed to delete assignment: {str(e)}")

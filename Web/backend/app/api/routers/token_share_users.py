"""API router for token share user management."""
import logging
from datetime import datetime
from typing import List

from fastapi import APIRouter, Depends, HTTPException
from pydantic import BaseModel, EmailStr, Field, field_validator
from sqlalchemy.orm import Session

from app.api.deps import get_db, require_role
from app.core import security
from app.models import TokenShareUser, TokenDeployment, UserRole

logger = logging.getLogger(__name__)

router = APIRouter(prefix="/api/token-share-users", tags=["token-share-users"])


class TokenShareUserCreate(BaseModel):
    """Request model for creating a token share user."""
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
    is_active: bool | None = None


class TokenShareUserResponse(BaseModel):
    """Response model for token share user."""
    id: str
    token_deployment_id: str
    name: str
    email: str
    phone: str | None
    is_active: bool
    created_at_utc: str

    @field_validator('created_at_utc', mode='before')
    @classmethod
    def serialize_datetime(cls, v):
        if isinstance(v, datetime):
            return v.isoformat()
        return v

    class Config:
        from_attributes = True


@router.post("/", response_model=TokenShareUserResponse)
def create_token_share_user(
    body: TokenShareUserCreate,
    db: Session = Depends(get_db),
    _=Depends(require_role(UserRole.SUPER_ADMIN))
):
    """Create a new token share user."""
    try:
        # Verify token deployment exists
        token_deployment = db.query(TokenDeployment).filter(
            TokenDeployment.id == body.token_deployment_id
        ).first()
        
        if not token_deployment:
            raise HTTPException(status_code=404, detail="Token deployment not found")
        
        # Check for duplicate email within same token
        existing = db.query(TokenShareUser).filter(
            TokenShareUser.token_deployment_id == body.token_deployment_id,
            TokenShareUser.email == body.email
        ).first()
        
        if existing:
            raise HTTPException(
                status_code=400,
                detail=f"User with email {body.email} already exists for this token"
            )
        
        # Create user
        user = TokenShareUser(
            token_deployment_id=body.token_deployment_id,
            name=body.name,
            email=body.email,
            phone=body.phone,
            password_hash=security.hash_password(body.password),
            is_active=True
        )
        
        db.add(user)
        db.commit()
        db.refresh(user)
        
        logger.info(f"Created token share user {user.id} for token {token_deployment.token_name}")
        
        return user
    
    except HTTPException:
        raise
    except Exception as e:
        db.rollback()
        logger.error(f"Failed to create token share user: {e}", exc_info=True)
        raise HTTPException(status_code=500, detail=f"Failed to create user: {str(e)}")


@router.get("/token/{token_deployment_id}", response_model=List[TokenShareUserResponse])
def list_token_share_users(
    token_deployment_id: str,
    db: Session = Depends(get_db),
    _=Depends(require_role(UserRole.SUPER_ADMIN))
):
    """Get all share users for a specific token deployment."""
    try:
        users = db.query(TokenShareUser).filter(
            TokenShareUser.token_deployment_id == token_deployment_id
        ).order_by(TokenShareUser.name).all()
        
        return users
    
    except Exception as e:
        logger.error(f"Failed to list token share users: {e}", exc_info=True)
        raise HTTPException(status_code=500, detail=f"Failed to retrieve users: {str(e)}")


@router.put("/{user_id}", response_model=TokenShareUserResponse)
def update_token_share_user(
    user_id: str,
    body: TokenShareUserUpdate,
    db: Session = Depends(get_db),
    _=Depends(require_role(UserRole.SUPER_ADMIN))
):
    """Update a token share user."""
    try:
        user = db.query(TokenShareUser).filter(TokenShareUser.id == user_id).first()
        
        if not user:
            raise HTTPException(status_code=404, detail="User not found")
        
        # Check for duplicate email if email is being changed
        if body.email and body.email != user.email:
            existing = db.query(TokenShareUser).filter(
                TokenShareUser.token_deployment_id == user.token_deployment_id,
                TokenShareUser.email == body.email,
                TokenShareUser.id != user_id
            ).first()
            
            if existing:
                raise HTTPException(
                    status_code=400,
                    detail=f"User with email {body.email} already exists for this token"
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
        if body.is_active is not None:
            user.is_active = body.is_active
        
        db.add(user)
        db.commit()
        db.refresh(user)
        
        logger.info(f"Updated token share user {user.id}")
        
        return user
    
    except HTTPException:
        raise
    except Exception as e:
        db.rollback()
        logger.error(f"Failed to update token share user: {e}", exc_info=True)
        raise HTTPException(status_code=500, detail=f"Failed to update user: {str(e)}")


@router.delete("/{user_id}")
def delete_token_share_user(
    user_id: str,
    db: Session = Depends(get_db),
    _=Depends(require_role(UserRole.SUPER_ADMIN))
):
    """Delete a token share user."""
    try:
        user = db.query(TokenShareUser).filter(TokenShareUser.id == user_id).first()
        
        if not user:
            raise HTTPException(status_code=404, detail="User not found")
        
        # Note: If there are assigned shares, they will be cascade deleted due to FK constraint
        db.delete(user)
        db.commit()
        
        logger.info(f"Deleted token share user {user_id}")
        
        return {"message": "User deleted successfully"}
    
    except HTTPException:
        raise
    except Exception as e:
        db.rollback()
        logger.error(f"Failed to delete token share user: {e}", exc_info=True)
        raise HTTPException(status_code=500, detail=f"Failed to delete user: {str(e)}")

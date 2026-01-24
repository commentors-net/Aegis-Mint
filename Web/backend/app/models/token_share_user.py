"""Token Share User model - users specific to token shares (not system users)."""
import uuid
from sqlalchemy import Boolean, Column, DateTime, ForeignKey, String
from sqlalchemy.orm import relationship

from app.core.time import utcnow
from app.db.base import Base


class TokenShareUser(Base):
    """Users created specifically for assigning token shares.
    
    These users authenticate to access assigned shares.
    They are bound to specific tokens and can only access shares for their assigned token.
    """
    __tablename__ = "token_share_users"

    id = Column(String(36), primary_key=True, default=lambda: str(uuid.uuid4()))
    token_deployment_id = Column(String(36), ForeignKey("token_deployments.id", ondelete="CASCADE"), nullable=False, index=True)
    name = Column(String(255), nullable=False)
    email = Column(String(255), nullable=False)
    phone = Column(String(20), nullable=True)
    password_hash = Column(String(255), nullable=True)
    mfa_secret = Column(String(64), nullable=True)
    is_active = Column(Boolean, default=True, nullable=False)
    created_at_utc = Column(DateTime(timezone=True), default=utcnow, nullable=False)

    # Relationships
    token_deployment = relationship("TokenDeployment", back_populates="share_users")
    share_assignments = relationship("ShareAssignment", foreign_keys="ShareAssignment.user_id", back_populates="user", cascade="all, delete-orphan")

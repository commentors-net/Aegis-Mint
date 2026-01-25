"""Token share user model - represents users who can access token shares."""
from datetime import datetime
from sqlalchemy import Column, String, Boolean, DateTime, ForeignKey
from sqlalchemy.orm import relationship
from app.db.base import Base
import uuid


class TokenUser(Base):
    """
    Users who can be assigned to tokens and access shares.
    One user can be assigned to multiple tokens.
    """
    __tablename__ = "token_users"
    
    id = Column(String(36), primary_key=True, default=lambda: str(uuid.uuid4()))
    email = Column(String(255), nullable=False, unique=True, index=True)
    name = Column(String(255), nullable=False)
    phone = Column(String(20), nullable=True)
    password_hash = Column(String(255), nullable=False)
    mfa_secret = Column(String(255), nullable=True)
    mfa_enabled = Column(Boolean, default=False, nullable=False)
    created_at = Column(DateTime, default=datetime.utcnow, nullable=False)
    updated_at = Column(DateTime, default=datetime.utcnow, onupdate=datetime.utcnow, nullable=False)
    
    # Relationships
    token_assignments = relationship("TokenUserAssignment", back_populates="user", cascade="all, delete-orphan")
    share_assignments = relationship("ShareAssignment", back_populates="user", cascade="all, delete-orphan")
    login_challenges = relationship("TokenUserLoginChallenge", back_populates="token_user", cascade="all, delete-orphan")
    download_logs = relationship("ShareDownloadLog", back_populates="token_user", cascade="all, delete-orphan")


class TokenUserAssignment(Base):
    """
    Many-to-many relationship between users and tokens.
    A user can be assigned to multiple tokens, and a token can have multiple users.
    """
    __tablename__ = "token_user_assignments"
    
    id = Column(String(36), primary_key=True, default=lambda: str(uuid.uuid4()))
    user_id = Column(String(36), ForeignKey("token_users.id", ondelete="CASCADE"), nullable=False, index=True)
    token_deployment_id = Column(String(36), ForeignKey("token_deployments.id", ondelete="CASCADE"), nullable=False, index=True)
    created_at = Column(DateTime, default=datetime.utcnow, nullable=False)
    updated_at = Column(DateTime, default=datetime.utcnow, onupdate=datetime.utcnow, nullable=False)
    
    # Relationships
    user = relationship("TokenUser", back_populates="token_assignments")
    token = relationship("TokenDeployment")
    
    __table_args__ = (
        # Ensure a user can only be assigned to a token once
        {'mysql_charset': 'utf8mb4', 'mysql_collate': 'utf8mb4_unicode_ci'},
    )

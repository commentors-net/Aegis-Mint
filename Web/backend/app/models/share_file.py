"""Model for individual share files storage."""
import uuid

from sqlalchemy import Boolean, Column, DateTime, ForeignKey, Integer, String, Text
from sqlalchemy.orm import relationship

from app.core.time import utcnow
from app.db.base import Base


class ShareFile(Base):
    """Individual Shamir secret share file with encrypted content."""
    __tablename__ = "share_files"

    id = Column(String(36), primary_key=True, default=lambda: str(uuid.uuid4()))
    token_deployment_id = Column(String(36), ForeignKey("token_deployments.id", ondelete="CASCADE"), nullable=False)
    share_number = Column(Integer, nullable=False)
    file_name = Column(String(255), nullable=False)
    encrypted_content = Column(Text, nullable=False)
    encryption_key_id = Column(String(128), nullable=True)
    created_at_utc = Column(DateTime(timezone=True), default=utcnow, nullable=False)
    is_active = Column(Boolean, default=True, nullable=False)
    replaced_at_utc = Column(DateTime(timezone=True), nullable=True)

    # Relationships
    token_deployment = relationship("TokenDeployment", back_populates="share_files")
    assignments = relationship("ShareAssignment", back_populates="share_file", cascade="all, delete-orphan")

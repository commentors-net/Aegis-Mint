"""Model for share assignments to users."""
import uuid

from sqlalchemy import Boolean, Column, DateTime, ForeignKey, Integer, String, Text
from sqlalchemy.orm import relationship

from app.core.time import utcnow
from app.db.base import Base


class ShareAssignment(Base):
    """Assignment of a share file to a governance authority user."""
    __tablename__ = "share_assignments"

    id = Column(String(36), primary_key=True, default=lambda: str(uuid.uuid4()))
    share_file_id = Column(String(36), ForeignKey("share_files.id", ondelete="CASCADE"), nullable=False)
    user_id = Column(String(36), ForeignKey("token_share_users.id", ondelete="CASCADE"), nullable=False)
    assigned_by = Column(String(36), ForeignKey("users.id", ondelete="RESTRICT"), nullable=False)
    assigned_at_utc = Column(DateTime(timezone=True), default=utcnow, nullable=False)
    
    # Status flags
    is_active = Column(Boolean, default=True, nullable=False)
    download_allowed = Column(Boolean, default=True, nullable=False)
    
    # Download tracking
    download_count = Column(Integer, default=0, nullable=False)
    first_downloaded_at_utc = Column(DateTime(timezone=True), nullable=True)
    last_downloaded_at_utc = Column(DateTime(timezone=True), nullable=True)
    
    # Notes
    assignment_notes = Column(Text, nullable=True)

    # Relationships
    share_file = relationship("ShareFile", back_populates="assignments")
    user = relationship("TokenShareUser", foreign_keys=[user_id], back_populates="share_assignments")
    assigner = relationship("User", foreign_keys=[assigned_by])
    download_logs = relationship("ShareDownloadLog", back_populates="assignment", cascade="all, delete-orphan")

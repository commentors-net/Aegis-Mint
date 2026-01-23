"""Model for share download audit log."""
import uuid

from sqlalchemy import Boolean, Column, DateTime, ForeignKey, String, Text
from sqlalchemy.orm import relationship

from app.core.time import utcnow
from app.db.base import Base


class ShareDownloadLog(Base):
    """Audit trail of all share download attempts."""
    __tablename__ = "share_download_log"

    id = Column(String(36), primary_key=True, default=lambda: str(uuid.uuid4()))
    share_assignment_id = Column(String(36), ForeignKey("share_assignments.id", ondelete="CASCADE"), nullable=False)
    user_id = Column(String(36), ForeignKey("users.id", ondelete="CASCADE"), nullable=False)
    downloaded_at_utc = Column(DateTime(timezone=True), default=utcnow, nullable=False)
    ip_address = Column(String(45), nullable=True)  # IPv4/IPv6
    user_agent = Column(Text, nullable=True)
    success = Column(Boolean, default=True, nullable=False)
    failure_reason = Column(Text, nullable=True)

    # Relationships
    assignment = relationship("ShareAssignment", back_populates="download_logs")
    user = relationship("User", back_populates="download_logs")

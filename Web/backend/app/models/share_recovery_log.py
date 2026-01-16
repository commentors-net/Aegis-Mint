"""Model for tracking share recovery attempts."""
import uuid

from sqlalchemy import Boolean, Column, DateTime, ForeignKey, String, Text

from app.core.time import utcnow
from app.db.base import Base


class ShareRecoveryLog(Base):
    """Audit log for share recovery attempts."""
    __tablename__ = "share_recovery_logs"

    id = Column(String(36), primary_key=True, default=lambda: str(uuid.uuid4()))
    at_utc = Column(DateTime(timezone=True), default=utcnow, nullable=False)
    user_id = Column(String(36), ForeignKey("users.id"), nullable=False)
    success = Column(Boolean, nullable=False)
    num_shares = Column(String(10), nullable=False)  # e.g., "2", "3"
    token_address = Column(String(128), nullable=True)  # If available in shares
    error_message = Column(Text, nullable=True)
    ip_address = Column(String(45), nullable=True)  # IPv6 max length

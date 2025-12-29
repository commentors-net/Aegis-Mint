import uuid

from sqlalchemy import Column, DateTime, ForeignKey, String, Text

from app.core.time import utcnow
from app.db.base import Base


class AuditLog(Base):
    __tablename__ = "audit_logs"

    id = Column(String(36), primary_key=True, default=lambda: str(uuid.uuid4()))
    at_utc = Column(DateTime(timezone=True), default=utcnow, nullable=False)
    action = Column(String(64), nullable=False)
    actor_user_id = Column(String(36), ForeignKey("users.id"), nullable=True)
    desktop_app_id = Column(String(64), ForeignKey("desktops.desktop_app_id"), nullable=True)
    session_id = Column(String(36), ForeignKey("approval_sessions.id"), nullable=True)
    details = Column(Text, nullable=True)

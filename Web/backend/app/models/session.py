import enum
import uuid

from sqlalchemy import Column, DateTime, Enum, ForeignKey, Integer, String
from sqlalchemy.orm import relationship

from app.core.time import utcnow
from app.db.base import Base


class SessionStatus(str, enum.Enum):
    NONE = "None"
    PENDING = "Pending"
    UNLOCKED = "Unlocked"
    EXPIRED = "Expired"
    CANCELLED = "Cancelled"


class ApprovalSession(Base):
    __tablename__ = "approval_sessions"

    id = Column(String(36), primary_key=True, default=lambda: str(uuid.uuid4()))
    desktop_app_id = Column(String(64), ForeignKey("desktops.desktop_app_id"), nullable=False, index=True)
    status = Column(Enum(SessionStatus), default=SessionStatus.PENDING, nullable=False)
    required_approvals_snapshot = Column(Integer, nullable=False)
    created_at_utc = Column(DateTime(timezone=True), default=utcnow, nullable=False)
    unlocked_at_utc = Column(DateTime(timezone=True), nullable=True)
    unlocked_until_utc = Column(DateTime(timezone=True), nullable=True)

    desktop = relationship("Desktop", back_populates="sessions")
    approvals = relationship("Approval", back_populates="session", cascade="all, delete-orphan")

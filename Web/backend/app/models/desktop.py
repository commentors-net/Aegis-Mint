import enum
import uuid

from sqlalchemy import Column, DateTime, Enum, Integer, String
from sqlalchemy.orm import relationship

from app.core.config import get_settings
from app.core.time import utcnow
from app.db.base import Base


class DesktopStatus(str, enum.Enum):
    PENDING = "Pending"
    ACTIVE = "Active"
    DISABLED = "Disabled"


class Desktop(Base):
    __tablename__ = "desktops"

    desktop_app_id = Column(String(64), primary_key=True)
    name_label = Column(String(255), nullable=True)
    status = Column(Enum(DesktopStatus), default=DesktopStatus.PENDING, nullable=False)
    required_approvals_n = Column(Integer, default=lambda: get_settings().required_approvals_default, nullable=False)
    unlock_minutes = Column(Integer, default=lambda: get_settings().unlock_minutes_default, nullable=False)
    created_at_utc = Column(DateTime(timezone=True), default=utcnow, nullable=False)
    last_seen_at_utc = Column(DateTime(timezone=True), nullable=True)
    machine_name = Column(String(255), nullable=True)
    token_control_version = Column(String(64), nullable=True)
    os_user = Column(String(128), nullable=True)

    assignments = relationship("GovernanceAssignment", back_populates="desktop", cascade="all, delete-orphan")
    sessions = relationship("ApprovalSession", back_populates="desktop", cascade="all, delete-orphan")

import enum
import uuid

from sqlalchemy import Boolean, Column, DateTime, Enum, String
from sqlalchemy.orm import relationship

from app.core.time import utcnow
from app.db.base import Base


class UserRole(str, enum.Enum):
    SUPER_ADMIN = "SuperAdmin"
    GOVERNANCE_AUTHORITY = "GovernanceAuthority"


class User(Base):
    __tablename__ = "users"

    id = Column(String(36), primary_key=True, default=lambda: str(uuid.uuid4()))
    email = Column(String(255), unique=True, nullable=False, index=True)
    password_hash = Column(String(255), nullable=False)
    role = Column(Enum(UserRole), nullable=False)
    mfa_secret = Column(String(64), nullable=False)
    phone = Column(String(20), nullable=True)
    is_active = Column(Boolean, default=True)
    created_at_utc = Column(DateTime(timezone=True), default=utcnow, nullable=False)

    assignments = relationship("GovernanceAssignment", back_populates="user", cascade="all, delete-orphan")
    approvals = relationship("Approval", back_populates="approver")
    download_logs = relationship("ShareDownloadLog", back_populates="user", cascade="all, delete-orphan")

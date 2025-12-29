"""
Authentication audit log model
"""
import enum

from sqlalchemy import Column, DateTime, Enum, String, Boolean, Text
from app.core.time import utcnow
from app.db.base import Base


class AuthEventType(str, enum.Enum):
    """Types of authentication events"""
    REGISTRATION = "Registration"
    AUTH_SUCCESS = "AuthSuccess"
    AUTH_FAILURE = "AuthFailure"
    KEY_ROTATION = "KeyRotation"
    INVALID_SIGNATURE = "InvalidSignature"
    TIMESTAMP_INVALID = "TimestampInvalid"
    DESKTOP_NOT_FOUND = "DesktopNotFound"


class AuthenticationLog(Base):
    """Audit log for all authentication attempts"""
    __tablename__ = "authentication_logs"

    id = Column(String(64), primary_key=True)  # UUID
    desktop_app_id = Column(String(64), nullable=False, index=True)
    event_type = Column(Enum(AuthEventType), nullable=False)
    success = Column(Boolean, nullable=False, default=False)
    endpoint = Column(String(255), nullable=True)  # e.g., /api/desktop/unlock-status
    ip_address = Column(String(45), nullable=True)  # IPv6 max length
    user_agent = Column(String(512), nullable=True)
    error_message = Column(Text, nullable=True)
    timestamp_utc = Column(DateTime(timezone=True), default=utcnow, nullable=False, index=True)
    
    # Additional context
    machine_name = Column(String(255), nullable=True)
    os_user = Column(String(128), nullable=True)
    token_control_version = Column(String(64), nullable=True)

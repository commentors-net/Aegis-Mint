"""
System settings model for storing CA and other system-wide configuration
"""
from sqlalchemy import Column, String, Text, DateTime, Boolean
from app.core.time import utcnow
from app.db.base import Base


class SystemSetting(Base):
    """System-wide settings and secure data storage"""
    __tablename__ = "system_settings"
    __table_args__ = {'extend_existing': True}

    key = Column(String(255), primary_key=True)
    value = Column(Text, nullable=True)
    encrypted = Column(Boolean, default=False, nullable=False)  # Whether value is encrypted
    created_at = Column(DateTime(timezone=True), default=utcnow, nullable=False)
    updated_at = Column(DateTime(timezone=True), default=utcnow, onupdate=utcnow, nullable=False)
    description = Column(String(512), nullable=True)

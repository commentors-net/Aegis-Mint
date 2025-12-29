import uuid

from sqlalchemy import Column, DateTime, ForeignKey, String
from sqlalchemy.orm import relationship

from app.core.time import utcnow
from app.db.base import Base


class LoginChallenge(Base):
    __tablename__ = "login_challenges"

    id = Column(String(36), primary_key=True, default=lambda: str(uuid.uuid4()))
    user_id = Column(String(36), ForeignKey("users.id"), nullable=False, index=True)
    expires_at_utc = Column(DateTime(timezone=True), nullable=False)
    temp_mfa_secret = Column(String(64), nullable=True)
    created_at_utc = Column(DateTime(timezone=True), default=utcnow, nullable=False)

    user = relationship("User")

"""Token User Login Challenge model for MFA authentication."""
import uuid

from sqlalchemy import Column, DateTime, ForeignKey, String
from sqlalchemy.orm import relationship

from app.core.time import utcnow
from app.db.base import Base


class TokenUserLoginChallenge(Base):
    """Login challenge for token share users (external users)."""
    __tablename__ = "token_user_login_challenges"

    id = Column(String(36), primary_key=True, default=lambda: str(uuid.uuid4()))
    token_user_id = Column(String(36), ForeignKey("token_users.id"), nullable=False, index=True)
    expires_at_utc = Column(DateTime(timezone=True), nullable=False)
    temp_mfa_secret = Column(String(64), nullable=True)
    created_at_utc = Column(DateTime(timezone=True), default=utcnow, nullable=False)

    token_user = relationship("TokenUser", back_populates="login_challenges")

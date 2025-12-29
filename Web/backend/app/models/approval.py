import uuid

from sqlalchemy import Column, DateTime, ForeignKey, String, UniqueConstraint
from sqlalchemy.orm import relationship

from app.core.time import utcnow
from app.db.base import Base


class Approval(Base):
    __tablename__ = "approvals"
    __table_args__ = (UniqueConstraint("session_id", "approver_user_id", name="uq_approval_unique"),)

    id = Column(String(36), primary_key=True, default=lambda: str(uuid.uuid4()))
    session_id = Column(String(36), ForeignKey("approval_sessions.id"), nullable=False, index=True)
    approver_user_id = Column(String(36), ForeignKey("users.id"), nullable=False, index=True)
    approved_at_utc = Column(DateTime(timezone=True), default=utcnow, nullable=False)

    session = relationship("ApprovalSession", back_populates="approvals")
    approver = relationship("User", back_populates="approvals")

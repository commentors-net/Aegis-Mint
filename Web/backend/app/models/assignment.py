import uuid

from sqlalchemy import Column, ForeignKey, String
from sqlalchemy.orm import relationship

from app.db.base import Base


class GovernanceAssignment(Base):
    __tablename__ = "governance_assignments"

    id = Column(String(36), primary_key=True, default=lambda: str(uuid.uuid4()))
    user_id = Column(String(36), ForeignKey("users.id"), nullable=False, index=True)
    desktop_id = Column(String(36), ForeignKey("desktops.id", ondelete="CASCADE"), nullable=False, index=True)
    desktop_app_id = Column(String(64), nullable=False, index=True)  # Kept for convenience/queries

    user = relationship("User", back_populates="assignments")
    desktop = relationship("Desktop", back_populates="assignments")

"""Model for tracking share creation and retrieval operations from desktop apps."""
import enum
import uuid

from sqlalchemy import Boolean, Column, DateTime, Enum, ForeignKey, Integer, String, Text

from app.core.time import utcnow
from app.db.base import Base


class ShareOperationType(str, enum.Enum):
    """Type of share operation."""
    CREATION = "Creation"  # Shares created in Mint
    RETRIEVAL = "Retrieval"  # Shares retrieved in TokenControl


class ShareOperationLog(Base):
    """Log of share creation and retrieval operations from desktop applications."""
    __tablename__ = "share_operation_logs"
    __table_args__ = (
        {'mysql_engine': 'InnoDB'}
    )

    id = Column(String(36), primary_key=True, default=lambda: str(uuid.uuid4()))
    at_utc = Column(DateTime(timezone=True), default=utcnow, nullable=False, index=True)
    
    # Desktop identification
    desktop_app_id = Column(String(64), ForeignKey("desktops.desktop_app_id"), nullable=False, index=True)
    app_type = Column(String(50), nullable=False)  # "Mint" or "TokenControl"
    machine_name = Column(String(255), nullable=True)
    
    # Operation details
    operation_type = Column(Enum(ShareOperationType, values_callable=lambda obj: [e.value for e in obj]), nullable=False, index=True)
    success = Column(Boolean, nullable=False, default=False)
    
    # Share metadata
    total_shares = Column(Integer, nullable=True)  # Total number of shares
    threshold = Column(Integer, nullable=True)  # Minimum shares needed
    shares_used = Column(Integer, nullable=True)  # For retrieval: how many shares were provided
    
    # Context
    token_name = Column(String(255), nullable=True)  # For creation
    token_address = Column(String(128), nullable=True, index=True)  # Contract address
    network = Column(String(50), nullable=True)  # e.g., "sepolia", "mainnet"
    
    # Storage path (for creation only)
    shares_path = Column(String(512), nullable=True)
    
    # Status and error tracking
    operation_stage = Column(String(100), nullable=True)  # Current stage of operation
    error_message = Column(Text, nullable=True)
    
    # Additional metadata
    notes = Column(Text, nullable=True)

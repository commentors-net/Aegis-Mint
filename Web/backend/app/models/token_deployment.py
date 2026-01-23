"""Model for tracking token deployments for emergency recovery."""
import uuid

from sqlalchemy import Boolean, Column, DateTime, Integer, String, Text
from sqlalchemy.orm import relationship

from app.core.time import utcnow
from app.db.base import Base


class TokenDeployment(Base):
    """Record of token deployments with crucial information for emergency recovery."""
    __tablename__ = "token_deployments"

    id = Column(String(36), primary_key=True, default=lambda: str(uuid.uuid4()))
    created_at_utc = Column(DateTime(timezone=True), default=utcnow, nullable=False)
    
    # Token information
    token_name = Column(String(255), nullable=False)
    token_symbol = Column(String(50), nullable=False)
    token_decimals = Column(Integer, nullable=False)
    token_supply = Column(String(100), nullable=False)
    
    # Network and deployment details
    network = Column(String(50), nullable=False)
    contract_address = Column(String(128), nullable=False)
    treasury_address = Column(String(128), nullable=False)
    proxy_admin_address = Column(String(128), nullable=True)
    
    # Governance configuration
    gov_shares = Column(Integer, nullable=False)
    gov_threshold = Column(Integer, nullable=False)
    total_shares = Column(Integer, nullable=False)
    client_share_count = Column(Integer, nullable=False)
    safekeeping_share_count = Column(Integer, nullable=False)
    
    # Share storage location
    shares_path = Column(String(512), nullable=False)
    
    # Additional metadata
    encrypted_mnemonic = Column(Text, nullable=True)  # For additional backup reference
    encrypted_shares = Column(Text, nullable=True)  # Encrypted share files for emergency recovery (DEPRECATED - use share_files table)
    encryption_version = Column(Integer, default=1, nullable=False)
    
    # Share upload tracking (NEW)
    shares_uploaded = Column(Boolean, default=False, nullable=False)
    upload_completed_at_utc = Column(DateTime(timezone=True), nullable=True)
    share_files_count = Column(Integer, default=0, nullable=False)
    
    # Deployment source
    desktop_id = Column(String(128), nullable=True)  # Desktop/machine identifier
    deployment_notes = Column(Text, nullable=True)

    # Relationships
    share_files = relationship("ShareFile", back_populates="token_deployment", cascade="all, delete-orphan")

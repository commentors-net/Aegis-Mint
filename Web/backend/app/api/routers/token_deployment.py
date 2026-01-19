"""API endpoints for token deployment tracking."""
import logging
from typing import Optional

from fastapi import APIRouter, Depends, HTTPException
from pydantic import BaseModel, Field
from sqlalchemy.orm import Session

from app.api.deps import get_db
from app.models.token_deployment import TokenDeployment

logger = logging.getLogger(__name__)

router = APIRouter(prefix="/api/token-deployments", tags=["token-deployments"])


class TokenDeploymentCreate(BaseModel):
    """Request model for creating a token deployment record."""
    token_name: str = Field(..., min_length=1, max_length=255)
    token_symbol: str = Field(..., min_length=1, max_length=50)
    token_decimals: int = Field(..., ge=0, le=18)
    token_supply: str = Field(..., min_length=1, max_length=100)
    
    network: str = Field(..., min_length=1, max_length=50)
    contract_address: str = Field(..., min_length=1, max_length=128)
    treasury_address: str = Field(..., min_length=1, max_length=128)
    proxy_admin_address: Optional[str] = Field(None, max_length=128)
    
    gov_shares: int = Field(..., ge=1)
    gov_threshold: int = Field(..., ge=1)
    total_shares: int = Field(..., ge=2)
    client_share_count: int = Field(..., ge=0)
    safekeeping_share_count: int = Field(..., ge=1)
    
    shares_path: str = Field(..., min_length=1, max_length=512)
    
    encrypted_mnemonic: Optional[str] = None
    encryption_version: int = Field(default=1, ge=1)
    
    desktop_id: Optional[str] = Field(None, max_length=128)
    deployment_notes: Optional[str] = None


class TokenDeploymentResponse(BaseModel):
    """Response model for token deployment."""
    id: str
    created_at_utc: str
    token_name: str
    token_symbol: str
    token_decimals: int
    token_supply: str
    network: str
    contract_address: str
    treasury_address: str
    proxy_admin_address: Optional[str]
    gov_shares: int
    gov_threshold: int
    total_shares: int
    client_share_count: int
    safekeeping_share_count: int
    shares_path: str
    encryption_version: int
    desktop_id: Optional[str]
    deployment_notes: Optional[str]

    class Config:
        from_attributes = True


@router.post("/", response_model=TokenDeploymentResponse)
def create_token_deployment(
    deployment: TokenDeploymentCreate,
    db: Session = Depends(get_db)
):
    """
    Store token deployment information for emergency recovery verification.
    
    This endpoint is called by the desktop application after a successful token deployment
    to record all crucial information needed for potential emergency recovery scenarios.
    """
    try:
        # Create new deployment record
        db_deployment = TokenDeployment(
            token_name=deployment.token_name,
            token_symbol=deployment.token_symbol,
            token_decimals=deployment.token_decimals,
            token_supply=deployment.token_supply,
            network=deployment.network,
            contract_address=deployment.contract_address,
            treasury_address=deployment.treasury_address,
            proxy_admin_address=deployment.proxy_admin_address,
            gov_shares=deployment.gov_shares,
            gov_threshold=deployment.gov_threshold,
            total_shares=deployment.total_shares,
            client_share_count=deployment.client_share_count,
            safekeeping_share_count=deployment.safekeeping_share_count,
            shares_path=deployment.shares_path,
            encrypted_mnemonic=deployment.encrypted_mnemonic,
            encryption_version=deployment.encryption_version,
            desktop_id=deployment.desktop_id,
            deployment_notes=deployment.deployment_notes,
        )
        
        db.add(db_deployment)
        db.commit()
        db.refresh(db_deployment)
        
        logger.info(
            f"Token deployment recorded: {deployment.token_name} "
            f"on {deployment.network} at {deployment.contract_address}"
        )
        
        return db_deployment
    
    except Exception as e:
        db.rollback()
        logger.error(f"Failed to create token deployment record: {e}", exc_info=True)
        raise HTTPException(status_code=500, detail=f"Failed to store deployment: {str(e)}")


@router.get("/", response_model=list[TokenDeploymentResponse])
def list_token_deployments(
    network: Optional[str] = None,
    token_name: Optional[str] = None,
    limit: int = 100,
    db: Session = Depends(get_db)
):
    """
    List token deployments with optional filtering.
    
    Useful for verifying what deployments exist and their configuration
    during emergency recovery scenarios.
    """
    try:
        query = db.query(TokenDeployment)
        
        if network:
            query = query.filter(TokenDeployment.network == network)
        
        if token_name:
            query = query.filter(TokenDeployment.token_name.ilike(f"%{token_name}%"))
        
        deployments = query.order_by(TokenDeployment.created_at_utc.desc()).limit(limit).all()
        
        return deployments
    
    except Exception as e:
        logger.error(f"Failed to list token deployments: {e}", exc_info=True)
        raise HTTPException(status_code=500, detail=f"Failed to retrieve deployments: {str(e)}")


@router.get("/{deployment_id}", response_model=TokenDeploymentResponse)
def get_token_deployment(
    deployment_id: str,
    db: Session = Depends(get_db)
):
    """
    Get a specific token deployment by ID.
    
    Retrieve all stored information about a specific token deployment
    for emergency recovery verification.
    """
    try:
        deployment = db.query(TokenDeployment).filter(TokenDeployment.id == deployment_id).first()
        
        if not deployment:
            raise HTTPException(status_code=404, detail="Deployment not found")
        
        return deployment
    
    except HTTPException:
        raise
    except Exception as e:
        logger.error(f"Failed to get token deployment: {e}", exc_info=True)
        raise HTTPException(status_code=500, detail=f"Failed to retrieve deployment: {str(e)}")


@router.get("/by-contract/{contract_address}", response_model=TokenDeploymentResponse)
def get_token_deployment_by_address(
    contract_address: str,
    db: Session = Depends(get_db)
):
    """
    Get token deployment by contract address.
    
    Useful when you know the contract address and need to retrieve
    all deployment information for emergency recovery.
    """
    try:
        deployment = db.query(TokenDeployment).filter(
            TokenDeployment.contract_address == contract_address
        ).first()
        
        if not deployment:
            raise HTTPException(status_code=404, detail="Deployment not found for this contract address")
        
        return deployment
    
    except HTTPException:
        raise
    except Exception as e:
        logger.error(f"Failed to get token deployment by address: {e}", exc_info=True)
        raise HTTPException(status_code=500, detail=f"Failed to retrieve deployment: {str(e)}")

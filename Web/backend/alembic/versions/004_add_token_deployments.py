"""Add token_deployments table for emergency recovery

Revision ID: 004_add_token_deployments
Revises: 003_add_app_type
Create Date: 2026-01-16

"""
from alembic import op
import sqlalchemy as sa
from sqlalchemy.dialects import postgresql


# revision identifiers, used by Alembic.
revision = '004_add_token_deployments'
down_revision = '003_add_app_type'
branch_labels = None
depends_on = None


def upgrade():
    # Create token_deployments table
    op.create_table(
        'token_deployments',
        sa.Column('id', sa.String(length=36), nullable=False),
        sa.Column('created_at_utc', sa.DateTime(timezone=True), nullable=False),
        
        # Token information
        sa.Column('token_name', sa.String(length=255), nullable=False),
        sa.Column('token_symbol', sa.String(length=50), nullable=False),
        sa.Column('token_decimals', sa.Integer(), nullable=False),
        sa.Column('token_supply', sa.String(length=100), nullable=False),
        
        # Network and deployment details
        sa.Column('network', sa.String(length=50), nullable=False),
        sa.Column('contract_address', sa.String(length=128), nullable=False),
        sa.Column('treasury_address', sa.String(length=128), nullable=False),
        sa.Column('proxy_admin_address', sa.String(length=128), nullable=True),
        
        # Governance configuration
        sa.Column('gov_shares', sa.Integer(), nullable=False),
        sa.Column('gov_threshold', sa.Integer(), nullable=False),
        sa.Column('total_shares', sa.Integer(), nullable=False),
        sa.Column('client_share_count', sa.Integer(), nullable=False),
        sa.Column('safekeeping_share_count', sa.Integer(), nullable=False),
        
        # Share storage location
        sa.Column('shares_path', sa.String(length=512), nullable=False),
        
        # Additional metadata
        sa.Column('encrypted_mnemonic', sa.Text(), nullable=True),
        sa.Column('encryption_version', sa.Integer(), nullable=False),
        
        # Deployment source
        sa.Column('desktop_id', sa.String(length=128), nullable=True),
        sa.Column('deployment_notes', sa.Text(), nullable=True),
        
        sa.PrimaryKeyConstraint('id')
    )
    
    # Create indexes for common queries
    op.create_index('ix_token_deployments_network', 'token_deployments', ['network'])
    op.create_index('ix_token_deployments_contract_address', 'token_deployments', ['contract_address'], unique=True)
    op.create_index('ix_token_deployments_token_name', 'token_deployments', ['token_name'])
    op.create_index('ix_token_deployments_created_at_utc', 'token_deployments', ['created_at_utc'])


def downgrade():
    # Drop indexes
    op.drop_index('ix_token_deployments_created_at_utc', 'token_deployments')
    op.drop_index('ix_token_deployments_token_name', 'token_deployments')
    op.drop_index('ix_token_deployments_contract_address', 'token_deployments')
    op.drop_index('ix_token_deployments_network', 'token_deployments')
    
    # Drop table
    op.drop_table('token_deployments')

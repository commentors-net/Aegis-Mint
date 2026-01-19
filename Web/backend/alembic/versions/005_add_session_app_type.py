"""Add app_type to approval_sessions table

Revision ID: 005_add_session_app_type
Revises: 004_add_token_deployments
Create Date: 2026-01-16

"""
from alembic import op
import sqlalchemy as sa


# revision identifiers, used by Alembic.
revision = '005_add_session_app_type'
down_revision = '004_add_token_deployments'
branch_labels = None
depends_on = None


def upgrade():
    # Add app_type column to approval_sessions table
    # Default to 'TokenControl' for backward compatibility
    op.add_column('approval_sessions', sa.Column('app_type', sa.String(length=50), nullable=False, server_default='TokenControl'))
    
    # Create composite index for filtering by desktop_app_id and app_type
    op.create_index('ix_approval_sessions_desktop_app_type', 'approval_sessions', ['desktop_app_id', 'app_type'])


def downgrade():
    # Drop index
    op.drop_index('ix_approval_sessions_desktop_app_type', 'approval_sessions')
    
    # Remove app_type column
    op.drop_column('approval_sessions', 'app_type')

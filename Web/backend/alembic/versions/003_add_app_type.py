"""Add app_type to desktops table

Revision ID: add_app_type
Revises: 002_add_certificate_auth
Create Date: 2026-01-16

"""
from alembic import op
import sqlalchemy as sa


# revision identifiers, used by Alembic.
revision = '003_add_app_type'
down_revision = '002_add_certificate_auth'
branch_labels = None
depends_on = None


def upgrade():
    # Add app_type column to desktops table
    # Default to 'TokenControl' for backward compatibility
    op.add_column('desktops', sa.Column('app_type', sa.String(length=50), nullable=False, server_default='TokenControl'))
    
    # Create index for filtering by app_type
    op.create_index('ix_desktops_app_type', 'desktops', ['app_type'])


def downgrade():
    # Drop index
    op.drop_index('ix_desktops_app_type', 'desktops')
    
    # Remove app_type column
    op.drop_column('desktops', 'app_type')

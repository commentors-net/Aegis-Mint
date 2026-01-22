"""Add encrypted_shares column to token_deployments

Revision ID: 008_add_encrypted_shares
Revises: 007_add_download_links
Create Date: 2026-01-21

"""
from alembic import op
import sqlalchemy as sa


# revision identifiers, used by Alembic.
revision = '008_add_encrypted_shares'
down_revision = '007_add_download_links'
branch_labels = None
depends_on = None


def upgrade():
    op.add_column('token_deployments', sa.Column('encrypted_shares', sa.Text(), nullable=True))


def downgrade():
    op.drop_column('token_deployments', 'encrypted_shares')

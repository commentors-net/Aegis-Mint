"""Add download_links table

Revision ID: 007_add_download_links
Revises: 006_composite_desktop_pk
Create Date: 2026-01-20

"""
from alembic import op
import sqlalchemy as sa
from datetime import datetime


# revision identifiers, used by Alembic.
revision = '007_add_download_links'
down_revision = '006_composite_desktop_pk'
branch_labels = None
depends_on = None


def upgrade():
    op.create_table(
        'download_links',
        sa.Column('id', sa.Integer(), nullable=False),
        sa.Column('url', sa.String(length=500), nullable=False),
        sa.Column('filename', sa.String(length=255), nullable=False),
        sa.Column('created_at', sa.DateTime(), nullable=False, default=datetime.utcnow),
        sa.Column('created_by', sa.String(length=255), nullable=False),
        sa.PrimaryKeyConstraint('id')
    )
    op.create_index('ix_download_links_id', 'download_links', ['id'])
    op.create_index('ix_download_links_url', 'download_links', ['url'], unique=True)


def downgrade():
    op.drop_index('ix_download_links_url', 'download_links')
    op.drop_index('ix_download_links_id', 'download_links')
    op.drop_table('download_links')

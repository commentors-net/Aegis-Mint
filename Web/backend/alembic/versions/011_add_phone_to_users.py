"""add phone to users

Revision ID: 011_add_phone_to_users
Revises: 010_add_share_management_tables
Create Date: 2026-01-24 09:30:00.000000

"""
from alembic import op
import sqlalchemy as sa


# revision identifiers, used by Alembic.
revision = '011_add_phone_to_users'
down_revision = '010_add_share_management'
branch_labels = None
depends_on = None


def upgrade():
    # Add phone column to users table
    op.add_column('users', sa.Column('phone', sa.String(20), nullable=True))


def downgrade():
    # Remove phone column from users table
    op.drop_column('users', 'phone')

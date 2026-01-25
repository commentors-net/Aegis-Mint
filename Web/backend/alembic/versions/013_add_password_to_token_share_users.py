"""add password to token share users

Revision ID: 013_add_password
Revises: 012_add_token_share_users
Create Date: 2026-01-24

"""
from alembic import op
import sqlalchemy as sa


# revision identifiers, used by Alembic.
revision = '013_add_password'
down_revision = '012_add_token_share_users'
branch_labels = None
depends_on = None


def upgrade() -> None:
    # Add password_hash and mfa_secret columns to token_share_users table
    op.add_column('token_share_users', sa.Column('password_hash', sa.String(255), nullable=True))
    op.add_column('token_share_users', sa.Column('mfa_secret', sa.String(64), nullable=True))
    op.add_column('token_share_users', sa.Column('is_active', sa.Boolean(), nullable=False, server_default='1'))


def downgrade() -> None:
    op.drop_column('token_share_users', 'is_active')
    op.drop_column('token_share_users', 'mfa_secret')
    op.drop_column('token_share_users', 'password_hash')

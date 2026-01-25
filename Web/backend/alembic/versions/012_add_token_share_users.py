"""add token share users table

Revision ID: 012_add_token_share_users
Revises: 011_add_phone_to_users
Create Date: 2026-01-24 09:35:00.000000

"""
from alembic import op
import sqlalchemy as sa


# revision identifiers, used by Alembic.
revision = '012_add_token_share_users'
down_revision = '011_add_phone_to_users'
branch_labels = None
depends_on = None


def upgrade():
    # Create token_share_users table
    op.create_table(
        'token_share_users',
        sa.Column('id', sa.String(36), primary_key=True),
        sa.Column('token_deployment_id', sa.String(36), nullable=False),
        sa.Column('name', sa.String(255), nullable=False),
        sa.Column('email', sa.String(255), nullable=False),
        sa.Column('phone', sa.String(20), nullable=True),
        sa.Column('created_at_utc', sa.DateTime(timezone=True), nullable=False),
        sa.ForeignKeyConstraint(['token_deployment_id'], ['token_deployments.id'], ondelete='CASCADE'),
    )
    
    # Create index for faster lookups
    op.create_index('ix_token_share_users_token_id', 'token_share_users', ['token_deployment_id'])


def downgrade():
    op.drop_index('ix_token_share_users_token_id', table_name='token_share_users')
    op.drop_table('token_share_users')

"""add token_user_login_challenges table

Revision ID: 015_token_user_login_challenges
Revises: 014_update_fk_assignment
Create Date: 2026-01-25

"""
from alembic import op
import sqlalchemy as sa
from sqlalchemy.dialects import mysql


# revision identifiers, used by Alembic.
revision = '015_token_user_login_challenges'
down_revision = '014_update_fk_assignment'
branch_labels = None
depends_on = None


def upgrade() -> None:
    # Create token_user_login_challenges table
    op.create_table(
        'token_user_login_challenges',
        sa.Column('id', sa.String(36), nullable=False),
        sa.Column('token_user_id', sa.String(36), nullable=False),
        sa.Column('expires_at_utc', mysql.DATETIME(timezone=True), nullable=False),
        sa.Column('temp_mfa_secret', sa.String(64), nullable=True),
        sa.Column('created_at_utc', mysql.DATETIME(timezone=True), nullable=False),
        sa.PrimaryKeyConstraint('id'),
        sa.ForeignKeyConstraint(
            ['token_user_id'], 
            ['token_share_users.id'],
            name='token_user_login_challenges_ibfk_1',
            ondelete='CASCADE'
        ),
        mysql_charset='utf8mb4',
        mysql_collate='utf8mb4_0900_ai_ci'
    )
    
    # Create index on token_user_id
    op.create_index(
        'ix_token_user_login_challenges_token_user_id',
        'token_user_login_challenges',
        ['token_user_id']
    )


def downgrade() -> None:
    op.drop_index('ix_token_user_login_challenges_token_user_id', table_name='token_user_login_challenges')
    op.drop_table('token_user_login_challenges')

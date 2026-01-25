"""add token_user_id to share_download_log

Revision ID: 016_token_user_log
Revises: 015_token_user_login_challenges
Create Date: 2026-01-25

"""
from alembic import op
import sqlalchemy as sa


# revision identifiers, used by Alembic.
revision = '016_token_user_log'
down_revision = '015_token_user_login_challenges'
branch_labels = None
depends_on = None


def upgrade() -> None:
    # Make user_id nullable (for token users)
    op.alter_column('share_download_log', 'user_id',
                    existing_type=sa.String(36),
                    nullable=True)
    
    # Add token_user_id column
    op.add_column('share_download_log', 
                  sa.Column('token_user_id', sa.String(36), nullable=True))
    
    # Add foreign key constraint
    op.create_foreign_key(
        'share_download_log_ibfk_3',
        'share_download_log', 'token_share_users',
        ['token_user_id'], ['id'],
        ondelete='CASCADE'
    )
    
    # Create index on token_user_id
    op.create_index(
        'ix_share_download_log_token_user_id',
        'share_download_log',
        ['token_user_id']
    )


def downgrade() -> None:
    op.drop_index('ix_share_download_log_token_user_id', table_name='share_download_log')
    op.drop_constraint('share_download_log_ibfk_3', 'share_download_log', type_='foreignkey')
    op.drop_column('share_download_log', 'token_user_id')
    
    # Make user_id non-nullable again
    op.alter_column('share_download_log', 'user_id',
                    existing_type=sa.String(36),
                    nullable=False)

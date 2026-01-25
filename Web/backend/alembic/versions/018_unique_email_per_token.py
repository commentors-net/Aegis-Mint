"""Make token share user email unique per token (composite constraint)

Revision ID: 018_unique_email_per_token
Revises: 016_token_user_log
Create Date: 2026-01-25 17:30:00.000000

"""
from alembic import op
import sqlalchemy as sa


# revision identifiers, used by Alembic.
revision = '018_unique_email_per_token'
down_revision = '016_token_user_log'
branch_labels = None
depends_on = None


def upgrade():
    """Add composite unique constraint on (email, token_deployment_id) in token_share_users table."""
    # First, check for and remove any duplicate emails within same token
    connection = op.get_bind()
    connection.execute(sa.text("""
        DELETE t1 FROM token_share_users t1
        INNER JOIN token_share_users t2 
        WHERE t1.id > t2.id 
        AND t1.email = t2.email 
        AND t1.token_deployment_id = t2.token_deployment_id
    """))
    
    # Add composite unique constraint (email unique per token)
    op.create_unique_constraint(
        'uq_token_share_users_email_token',
        'token_share_users',
        ['email', 'token_deployment_id']
    )
    
    # Add index on email for faster lookups
    op.create_index(
        'ix_token_share_users_email',
        'token_share_users',
        ['email']
    )


def downgrade():
    """Remove composite unique constraint and email index."""
    op.drop_index('ix_token_share_users_email', 'token_share_users')
    op.drop_constraint('uq_token_share_users_email_token', 'token_share_users', type_='unique')

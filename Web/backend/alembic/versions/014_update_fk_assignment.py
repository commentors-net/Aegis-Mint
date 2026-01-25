"""Update share assignment foreign key to token_share_users

Revision ID: 014_update_fk_assignment
Revises: 013_add_password
Create Date: 2026-01-24 23:30:00.000000

"""
from alembic import op
import sqlalchemy as sa


# revision identifiers, used by Alembic.
revision = '014_update_fk_assignment'
down_revision = '013_add_password'
branch_labels = None
depends_on = None


def upgrade():
    """Update share_assignments.user_id foreign key to reference token_share_users."""
    # Drop the existing foreign key constraint
    op.drop_constraint('share_assignments_ibfk_2', 'share_assignments', type_='foreignkey')
    
    # Add new foreign key constraint pointing to token_share_users
    op.create_foreign_key(
        'share_assignments_ibfk_2',
        'share_assignments',
        'token_share_users',
        ['user_id'],
        ['id'],
        ondelete='CASCADE'
    )


def downgrade():
    """Revert share_assignments.user_id foreign key to reference users."""
    # Drop the new foreign key constraint
    op.drop_constraint('share_assignments_ibfk_2', 'share_assignments', type_='foreignkey')
    
    # Restore original foreign key constraint pointing to users
    op.create_foreign_key(
        'share_assignments_ibfk_2',
        'share_assignments',
        'users',
        ['user_id'],
        ['id'],
        ondelete='CASCADE'
    )

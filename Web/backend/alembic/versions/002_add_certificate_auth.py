"""add certificate authority support

Revision ID: 002_add_certificate_auth
Revises: 001_add_desktop_secret_key
Create Date: 2024-01-15 10:00:00.000000

"""
from alembic import op
import sqlalchemy as sa

# revision identifiers, used by Alembic.
revision = '002_add_certificate_auth'
down_revision = '001_add_desktop_secret_key'
branch_labels = None
depends_on = None


def upgrade() -> None:
    # Create system_settings table for CA and other system-wide config
    op.create_table('system_settings',
        sa.Column('key', sa.String(length=255), nullable=False),
        sa.Column('value', sa.Text(), nullable=True),
        sa.Column('encrypted', sa.Boolean(), nullable=False, server_default='0'),
        sa.Column('created_at', sa.DateTime(), nullable=False, server_default=sa.text('CURRENT_TIMESTAMP')),
        sa.Column('updated_at', sa.DateTime(), nullable=False, server_default=sa.text('CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP')),
        sa.Column('description', sa.String(length=512), nullable=True),
        sa.PrimaryKeyConstraint('key')
    )
    
    # Add certificate columns to desktops table
    op.add_column('desktops', sa.Column('certificate_pem', sa.Text(), nullable=True))
    op.add_column('desktops', sa.Column('certificate_issued_at', sa.DateTime(), nullable=True))
    op.add_column('desktops', sa.Column('certificate_expires_at', sa.DateTime(), nullable=True))
    op.add_column('desktops', sa.Column('csr_submitted', sa.Boolean(), nullable=False, server_default='0'))
    op.add_column('desktops', sa.Column('csr_pem', sa.Text(), nullable=True))


def downgrade() -> None:
    op.drop_column('desktops', 'csr_pem')
    op.drop_column('desktops', 'csr_submitted')
    op.drop_column('desktops', 'certificate_expires_at')
    op.drop_column('desktops', 'certificate_issued_at')
    op.drop_column('desktops', 'certificate_pem')
    op.drop_table('system_settings')

"""Add secret_key and auth logs

Revision ID: add_desktop_secret_key
Revises: 
Create Date: 2025-12-28

"""
from alembic import op
import sqlalchemy as sa


# revision identifiers, used by Alembic.
revision = '001_add_desktop_secret_key'
down_revision = None
branch_labels = None
depends_on = None


def upgrade():
    # Add secret_key column to desktops table
    op.add_column('desktops', sa.Column('secret_key', sa.String(length=64), nullable=True))
    
    # Add secret_key_rotated_at column to desktops table
    op.add_column('desktops', sa.Column('secret_key_rotated_at', sa.DateTime(), nullable=True))
    
    # Create authentication_logs table
    op.create_table(
        'authentication_logs',
        sa.Column('id', sa.String(length=64), nullable=False),
        sa.Column('desktop_app_id', sa.String(length=64), nullable=False),
        sa.Column('event_type', sa.String(length=50), nullable=False),
        sa.Column('success', sa.Boolean(), nullable=False, server_default='0'),
        sa.Column('endpoint', sa.String(length=255), nullable=True),
        sa.Column('ip_address', sa.String(length=45), nullable=True),
        sa.Column('user_agent', sa.String(length=512), nullable=True),
        sa.Column('error_message', sa.Text(), nullable=True),
        sa.Column('timestamp_utc', sa.DateTime(), nullable=False),
        sa.Column('machine_name', sa.String(length=255), nullable=True),
        sa.Column('os_user', sa.String(length=128), nullable=True),
        sa.Column('token_control_version', sa.String(length=64), nullable=True),
        sa.PrimaryKeyConstraint('id')
    )
    
    # Create indexes for authentication_logs
    op.create_index('ix_authentication_logs_desktop_app_id', 'authentication_logs', ['desktop_app_id'])
    op.create_index('ix_authentication_logs_timestamp_utc', 'authentication_logs', ['timestamp_utc'])


def downgrade():
    # Drop indexes
    op.drop_index('ix_authentication_logs_timestamp_utc', 'authentication_logs')
    op.drop_index('ix_authentication_logs_desktop_app_id', 'authentication_logs')
    
    # Drop authentication_logs table
    op.drop_table('authentication_logs')
    
    # Remove columns from desktops table
    op.drop_column('desktops', 'secret_key_rotated_at')
    op.drop_column('desktops', 'secret_key')

"""add share operation logs table

Revision ID: 009_share_operation_logs
Revises: 008_add_encrypted_shares
Create Date: 2026-01-22

"""
from alembic import op
import sqlalchemy as sa


# revision identifiers, used by Alembic.
revision = '009_share_operation_logs'
down_revision = '008_add_encrypted_shares'
branch_labels = None
depends_on = None


def upgrade():
    op.create_table(
        'share_operation_logs',
        sa.Column('id', sa.String(36), nullable=False),
        sa.Column('at_utc', sa.DateTime(timezone=True), nullable=False),
        sa.Column('desktop_app_id', sa.String(64), nullable=False),
        sa.Column('app_type', sa.String(50), nullable=False),
        sa.Column('machine_name', sa.String(255), nullable=True),
        sa.Column('operation_type', sa.Enum('Creation', 'Retrieval', name='shareoperationtype'), nullable=False),
        sa.Column('success', sa.Boolean(), nullable=False, default=False),
        sa.Column('total_shares', sa.Integer(), nullable=True),
        sa.Column('threshold', sa.Integer(), nullable=True),
        sa.Column('shares_used', sa.Integer(), nullable=True),
        sa.Column('token_name', sa.String(255), nullable=True),
        sa.Column('token_address', sa.String(128), nullable=True),
        sa.Column('network', sa.String(50), nullable=True),
        sa.Column('shares_path', sa.String(512), nullable=True),
        sa.Column('operation_stage', sa.String(100), nullable=True),
        sa.Column('error_message', sa.Text(), nullable=True),
        sa.Column('notes', sa.Text(), nullable=True),
        sa.PrimaryKeyConstraint('id'),
        sa.ForeignKeyConstraint(['desktop_app_id'], ['desktops.desktop_app_id'], ),
        mysql_engine='InnoDB'
    )
    
    # Create indexes for frequently queried columns
    op.create_index('ix_share_operation_logs_at_utc', 'share_operation_logs', ['at_utc'])
    op.create_index('ix_share_operation_logs_desktop_app_id', 'share_operation_logs', ['desktop_app_id'])
    op.create_index('ix_share_operation_logs_operation_type', 'share_operation_logs', ['operation_type'])
    op.create_index('ix_share_operation_logs_token_address', 'share_operation_logs', ['token_address'])


def downgrade():
    op.drop_index('ix_share_operation_logs_token_address', table_name='share_operation_logs')
    op.drop_index('ix_share_operation_logs_operation_type', table_name='share_operation_logs')
    op.drop_index('ix_share_operation_logs_desktop_app_id', table_name='share_operation_logs')
    op.drop_index('ix_share_operation_logs_at_utc', table_name='share_operation_logs')
    op.drop_table('share_operation_logs')
    op.execute("DROP TYPE IF EXISTS shareoperationtype")

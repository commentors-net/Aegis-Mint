"""Add share management tables (share_files, share_assignments, share_download_log)

Revision ID: 010_add_share_management
Revises: 009_share_operation_logs
Create Date: 2026-01-24

This migration creates the infrastructure for managing individual Shamir secret shares,
including storage, assignment to users, and download tracking.
"""
from alembic import op
import sqlalchemy as sa
from sqlalchemy.dialects import postgresql

# revision identifiers, used by Alembic.
revision = '010_add_share_management'
down_revision = '009_share_operation_logs'
branch_labels = None
depends_on = None


def upgrade():
    # 1. Create share_files table
    op.create_table(
        'share_files',
        sa.Column('id', sa.String(36), primary_key=True),
        sa.Column('token_deployment_id', sa.String(36), nullable=False),
        sa.Column('share_number', sa.Integer, nullable=False),
        sa.Column('file_name', sa.String(255), nullable=False),
        sa.Column('encrypted_content', sa.Text, nullable=False),
        sa.Column('encryption_key_id', sa.String(128), nullable=True),
        sa.Column('created_at_utc', sa.DateTime(timezone=True), server_default=sa.text('NOW()'), nullable=False),
        sa.ForeignKeyConstraint(['token_deployment_id'], ['token_deployments.id'], ondelete='CASCADE'),
    )
    
    # Create indexes for share_files
    op.create_index('ix_share_files_token_deployment', 'share_files', ['token_deployment_id'])
    op.create_index('ix_share_files_share_number', 'share_files', ['share_number'])
    
    # Create unique constraint for token_deployment_id + share_number
    op.create_unique_constraint('uq_share_files_token_share', 'share_files', ['token_deployment_id', 'share_number'])
    
    # 2. Create share_assignments table
    op.create_table(
        'share_assignments',
        sa.Column('id', sa.String(36), primary_key=True),
        sa.Column('share_file_id', sa.String(36), nullable=False),
        sa.Column('user_id', sa.String(36), nullable=False),
        sa.Column('assigned_by', sa.String(36), nullable=False),
        sa.Column('assigned_at_utc', sa.DateTime(timezone=True), server_default=sa.text('NOW()'), nullable=False),
        sa.Column('is_active', sa.Boolean, server_default=sa.text('TRUE'), nullable=False),
        sa.Column('download_allowed', sa.Boolean, server_default=sa.text('TRUE'), nullable=False),
        sa.Column('download_count', sa.Integer, server_default=sa.text('0'), nullable=False),
        sa.Column('first_downloaded_at_utc', sa.DateTime(timezone=True), nullable=True),
        sa.Column('last_downloaded_at_utc', sa.DateTime(timezone=True), nullable=True),
        sa.Column('assignment_notes', sa.Text, nullable=True),
        sa.ForeignKeyConstraint(['share_file_id'], ['share_files.id'], ondelete='CASCADE'),
        sa.ForeignKeyConstraint(['user_id'], ['users.id'], ondelete='CASCADE'),
        sa.ForeignKeyConstraint(['assigned_by'], ['users.id'], ondelete='RESTRICT'),
    )
    
    # Create indexes for share_assignments
    op.create_index('ix_share_assignments_user', 'share_assignments', ['user_id'])
    op.create_index('ix_share_assignments_share_file', 'share_assignments', ['share_file_id'])
    op.create_index('ix_share_assignments_active', 'share_assignments', ['is_active', 'download_allowed'])
    op.create_index('ix_share_assignments_assigned_at', 'share_assignments', ['assigned_at_utc'])
    
    # Create unique constraint for share_file_id + user_id (one share per user only)
    op.create_unique_constraint('uq_share_assignments_share_user', 'share_assignments', ['share_file_id', 'user_id'])
    
    # 3. Create share_download_log table
    op.create_table(
        'share_download_log',
        sa.Column('id', sa.String(36), primary_key=True),
        sa.Column('share_assignment_id', sa.String(36), nullable=False),
        sa.Column('user_id', sa.String(36), nullable=False),
        sa.Column('downloaded_at_utc', sa.DateTime(timezone=True), server_default=sa.text('NOW()'), nullable=False),
        sa.Column('ip_address', sa.String(45), nullable=True),  # IPv4/IPv6
        sa.Column('user_agent', sa.Text, nullable=True),
        sa.Column('success', sa.Boolean, server_default=sa.text('TRUE'), nullable=False),
        sa.Column('failure_reason', sa.Text, nullable=True),
        sa.ForeignKeyConstraint(['share_assignment_id'], ['share_assignments.id'], ondelete='CASCADE'),
        sa.ForeignKeyConstraint(['user_id'], ['users.id'], ondelete='CASCADE'),
    )
    
    # Create indexes for share_download_log
    op.create_index('ix_share_download_log_user', 'share_download_log', ['user_id'])
    op.create_index('ix_share_download_log_assignment', 'share_download_log', ['share_assignment_id'])
    op.create_index('ix_share_download_log_timestamp', 'share_download_log', ['downloaded_at_utc'])
    op.create_index('ix_share_download_log_success', 'share_download_log', ['success'])
    
    # 4. Add new columns to token_deployments for tracking share upload status
    op.add_column('token_deployments', sa.Column('shares_uploaded', sa.Boolean, server_default=sa.text('FALSE'), nullable=False))
    op.add_column('token_deployments', sa.Column('upload_completed_at_utc', sa.DateTime(timezone=True), nullable=True))
    op.add_column('token_deployments', sa.Column('share_files_count', sa.Integer, server_default=sa.text('0'), nullable=False))
    
    # Create index for shares_uploaded status
    op.create_index('ix_token_deployments_shares_uploaded', 'token_deployments', ['shares_uploaded'])


def downgrade():
    # Remove indexes and columns from token_deployments
    op.drop_index('ix_token_deployments_shares_uploaded', 'token_deployments')
    op.drop_column('token_deployments', 'share_files_count')
    op.drop_column('token_deployments', 'upload_completed_at_utc')
    op.drop_column('token_deployments', 'shares_uploaded')
    
    # Drop share_download_log table
    op.drop_index('ix_share_download_log_success', 'share_download_log')
    op.drop_index('ix_share_download_log_timestamp', 'share_download_log')
    op.drop_index('ix_share_download_log_assignment', 'share_download_log')
    op.drop_index('ix_share_download_log_user', 'share_download_log')
    op.drop_table('share_download_log')
    
    # Drop share_assignments table
    op.drop_constraint('uq_share_assignments_share_user', 'share_assignments')
    op.drop_index('ix_share_assignments_assigned_at', 'share_assignments')
    op.drop_index('ix_share_assignments_active', 'share_assignments')
    op.drop_index('ix_share_assignments_share_file', 'share_assignments')
    op.drop_index('ix_share_assignments_user', 'share_assignments')
    op.drop_table('share_assignments')
    
    # Drop share_files table
    op.drop_constraint('uq_share_files_token_share', 'share_files')
    op.drop_index('ix_share_files_share_number', 'share_files')
    op.drop_index('ix_share_files_token_deployment', 'share_files')
    op.drop_table('share_files')

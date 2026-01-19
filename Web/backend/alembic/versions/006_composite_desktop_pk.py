"""Make desktop primary key composite (desktop_app_id, app_type)

Revision ID: 006_composite_desktop_pk
Revises: 005_add_session_app_type
Create Date: 2026-01-16

"""
from alembic import op
import sqlalchemy as sa


# revision identifiers, used by Alembic.
revision = '006_composite_desktop_pk'
down_revision = '005_add_session_app_type'
branch_labels = None
depends_on = None


def upgrade():
    # WARNING: This migration drops and recreates the desktops table
    # All desktop registrations will be lost
    # This is acceptable for development, but for production you need data migration
    
    # Step 1: Drop FK constraint from audit_logs to desktops (if it exists)
    try:
        op.execute("ALTER TABLE audit_logs DROP FOREIGN KEY audit_logs_ibfk_2")
    except:
        pass  # FK might not exist if previous migration failed partway
    
    # Step 2: Drop the old desktops table (if it exists)
    try:
        op.drop_table('desktops')
    except:
        pass  # Table might already be dropped from previous failed migration attempt
    
    # Step 3: Recreate desktops table with new schema (composite PK)
    op.create_table(
        'desktops',
        sa.Column('id', sa.String(length=36), nullable=False),
        sa.Column('desktop_app_id', sa.String(length=64), nullable=False),
        sa.Column('app_type', sa.String(length=50), nullable=False),
        sa.Column('name_label', sa.String(length=255), nullable=True),
        sa.Column('status', sa.Enum('PENDING', 'ACTIVE', 'DISABLED', name='desktopstatus'), nullable=False),
        sa.Column('required_approvals_n', sa.Integer(), nullable=False),
        sa.Column('unlock_minutes', sa.Integer(), nullable=False),
        sa.Column('secret_key', sa.String(length=64), nullable=True),
        sa.Column('secret_key_rotated_at', sa.DateTime(timezone=True), nullable=True),
        sa.Column('certificate_pem', sa.Text(), nullable=True),
        sa.Column('certificate_issued_at', sa.DateTime(timezone=True), nullable=True),
        sa.Column('certificate_expires_at', sa.DateTime(timezone=True), nullable=True),
        sa.Column('csr_submitted', sa.Integer(), nullable=False),
        sa.Column('csr_pem', sa.Text(), nullable=True),
        sa.Column('created_at_utc', sa.DateTime(timezone=True), nullable=False),
        sa.Column('last_seen_at_utc', sa.DateTime(timezone=True), nullable=True),
        sa.Column('machine_name', sa.String(length=255), nullable=True),
        sa.Column('token_control_version', sa.String(length=64), nullable=True),
        sa.Column('os_user', sa.String(length=128), nullable=True),
        sa.PrimaryKeyConstraint('desktop_app_id', 'app_type'),
        sa.UniqueConstraint('id'),
        mysql_engine='InnoDB'
    )
    
    # Step 4: Add desktop_id columns to related tables if not exists
    with op.batch_alter_table('approval_sessions', schema=None) as batch_op:
        try:
            batch_op.add_column(sa.Column('desktop_id', sa.String(length=36), nullable=True))
        except:
            pass  # Column might already exist
    
    with op.batch_alter_table('governance_assignments', schema=None) as batch_op:
        try:
            batch_op.add_column(sa.Column('desktop_id', sa.String(length=36), nullable=True))
        except:
            pass  # Column might already exist
            
    with op.batch_alter_table('audit_logs', schema=None) as batch_op:
        try:
            batch_op.add_column(sa.Column('desktop_id', sa.String(length=36), nullable=True))
        except:
            pass  # Column might already exist
        try:
            batch_op.add_column(sa.Column('app_type', sa.String(length=50), nullable=True))
        except:
            pass  # Column might already exist
    
    # Step 5: Since we dropped desktops, we need to clean existing data
    op.execute("DELETE FROM approval_sessions")
    op.execute("DELETE FROM governance_assignments")
    op.execute("DELETE FROM audit_logs WHERE desktop_app_id IS NOT NULL")
    
    # Step 6: Create foreign key constraints on desktop_id
    with op.batch_alter_table('approval_sessions', schema=None) as batch_op:
        batch_op.create_foreign_key(
            'fk_approval_sessions_desktop_id',
            'desktops',
            ['desktop_id'], ['id'],
            ondelete='CASCADE'
        )
        batch_op.create_index('ix_approval_sessions_desktop_id', ['desktop_id'])
    
    with op.batch_alter_table('governance_assignments', schema=None) as batch_op:
        batch_op.create_foreign_key(
            'fk_governance_assignments_desktop_id',
            'desktops',
            ['desktop_id'], ['id'],
            ondelete='CASCADE'
        )
        batch_op.create_index('ix_governance_assignments_desktop_id', ['desktop_id'])
    
    # Recreate FK to desktops from audit_logs (still using desktop_app_id + app_type)
    # Note: We can't create FK to composite PK easily, so we'll FK to id instead
    with op.batch_alter_table('audit_logs', schema=None) as batch_op:
        batch_op.create_foreign_key(
            'fk_audit_logs_desktop_id',
            'desktops',
            ['desktop_id'], ['id'],
            ondelete='CASCADE'
        )


def downgrade():
    # Downgrade not fully supported - would need to reverse the table drop
    # For development, just drop and recreate from scratch if needed
    
    # Drop new foreign keys
    with op.batch_alter_table('governance_assignments', schema=None) as batch_op:
        batch_op.drop_index('ix_governance_assignments_desktop_id')
        batch_op.drop_constraint('fk_governance_assignments_desktop_id', type_='foreignkey')
        batch_op.drop_column('desktop_id')
    
    with op.batch_alter_table('approval_sessions', schema=None) as batch_op:
        batch_op.drop_index('ix_approval_sessions_desktop_id')
        batch_op.drop_constraint('fk_approval_sessions_desktop_id', type_='foreignkey')
        batch_op.drop_column('desktop_id')
    
    # Drop new desktops table
    op.drop_table('desktops')
    
    # Recreate old desktops table (simplified - you'd need to restore data)
    op.create_table(
        'desktops',
        sa.Column('desktop_app_id', sa.String(length=64), nullable=False),
        sa.Column('name_label', sa.String(length=255), nullable=True),
        sa.Column('status', sa.Enum('PENDING', 'ACTIVE', 'DISABLED', name='desktopstatus'), nullable=False),
        sa.Column('required_approvals_n', sa.Integer(), nullable=False),
        sa.Column('unlock_minutes', sa.Integer(), nullable=False),
        sa.Column('secret_key', sa.String(length=64), nullable=True),
        sa.Column('secret_key_rotated_at', sa.DateTime(timezone=True), nullable=True),
        sa.Column('certificate_pem', sa.String(), nullable=True),
        sa.Column('certificate_issued_at', sa.DateTime(timezone=True), nullable=True),
        sa.Column('certificate_expires_at', sa.DateTime(timezone=True), nullable=True),
        sa.Column('csr_submitted', sa.Integer(), nullable=False),
        sa.Column('created_at', sa.DateTime(timezone=True), nullable=False),
        sa.Column('updated_at', sa.DateTime(timezone=True), nullable=False),
        sa.PrimaryKeyConstraint('desktop_app_id'),
        mysql_engine='InnoDB'
    )

    
    # Recreate old foreign keys
    op.create_foreign_key(
        'approval_sessions_ibfk_1',
        'approval_sessions', 'desktops',
        ['desktop_app_id'], ['desktop_app_id']
    )
    
    op.create_foreign_key(
        'governance_assignments_ibfk_2',
        'governance_assignments', 'desktops',
        ['desktop_app_id'], ['desktop_app_id']
    )

"""Restructure users table - separate users from token assignments

Revision ID: 019_restructure_users_table
Revises: 018_unique_email_per_token
Create Date: 2026-01-25 23:05:00.000000

"""
from alembic import op
import sqlalchemy as sa
from sqlalchemy.dialects import mysql

# revision identifiers, used by Alembic.
revision = '019_restructure_users_table'
down_revision = '018_unique_email_per_token'
branch_labels = None
depends_on = None


def upgrade():
    """
    Restructure database to properly handle users assigned to multiple tokens.
    Old design: token_share_users table with duplicated user data per token
    New design: users table + token_user_assignments many-to-many table
    """
    
    # Create new users table
    op.create_table(
        'token_users',
        sa.Column('id', sa.String(length=36), nullable=False),
        sa.Column('email', sa.String(length=255), nullable=False),
        sa.Column('name', sa.String(length=255), nullable=False),
        sa.Column('phone', sa.String(length=20), nullable=True),
        sa.Column('password_hash', sa.String(length=255), nullable=False),
        sa.Column('mfa_secret', sa.String(length=255), nullable=True),
        sa.Column('mfa_enabled', sa.Boolean(), nullable=False, server_default='0'),
        sa.Column('created_at', sa.DateTime(), nullable=False, server_default=sa.text('CURRENT_TIMESTAMP')),
        sa.Column('updated_at', sa.DateTime(), nullable=False, server_default=sa.text('CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP')),
        sa.PrimaryKeyConstraint('id'),
        sa.UniqueConstraint('email', name='uq_users_email')
    )
    op.create_index('idx_users_email', 'token_users', ['email'])
    
    # Create token_user_assignments table (many-to-many)
    op.create_table(
        'token_user_assignments',
        sa.Column('id', sa.String(length=36), nullable=False),
        sa.Column('user_id', sa.String(length=36), nullable=False),
        sa.Column('token_deployment_id', sa.String(length=36), nullable=False),
        sa.Column('created_at', sa.DateTime(), nullable=False, server_default=sa.text('CURRENT_TIMESTAMP')),
        sa.Column('updated_at', sa.DateTime(), nullable=False, server_default=sa.text('CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP')),
        sa.ForeignKeyConstraint(['user_id'], ['token_users.id'], ondelete='CASCADE'),
        sa.ForeignKeyConstraint(['token_deployment_id'], ['token_deployments.id'], ondelete='CASCADE'),
        sa.PrimaryKeyConstraint('id'),
        sa.UniqueConstraint('user_id', 'token_deployment_id', name='uq_user_token')
    )
    op.create_index('idx_token_user_assignments_user_id', 'token_user_assignments', ['user_id'])
    op.create_index('idx_token_user_assignments_token_id', 'token_user_assignments', ['token_deployment_id'])
    
    # Migrate data from token_share_users to new structure
    # Use raw SQL for complex data migration
    connection = op.get_bind()
    
    # Step 1: Insert unique users (consolidate duplicates by email)
    connection.execute(sa.text("""
        INSERT INTO token_users (id, email, name, phone, password_hash, mfa_secret, mfa_enabled, created_at, updated_at)
        SELECT 
            MIN(id) as id,
            email,
            MIN(name) as name,
            MIN(phone) as phone,
            MIN(password_hash) as password_hash,
            MIN(mfa_secret) as mfa_secret,
            CASE WHEN MIN(mfa_secret) IS NOT NULL THEN 1 ELSE 0 END as mfa_enabled,
            MIN(created_at_utc) as created_at,
            MIN(created_at_utc) as updated_at
        FROM token_share_users
        GROUP BY email
    """))
    
    # Step 2: Create token assignments for all user-token pairs
    connection.execute(sa.text("""
        INSERT INTO token_user_assignments (id, user_id, token_deployment_id, created_at, updated_at)
        SELECT 
            UUID() as id,
            (SELECT MIN(id) FROM token_share_users tsu2 WHERE tsu2.email = tsu.email) as user_id,
            tsu.token_deployment_id,
            tsu.created_at_utc,
            tsu.created_at_utc
        FROM token_share_users tsu
    """))
    
    # Step 3: Update share_assignments to reference users instead of token_share_users
    # First add new user_id column
    op.add_column('share_assignments', sa.Column('user_id', sa.String(length=36), nullable=True))
    
    # Copy token_share_user_id to user_id (they now reference the same IDs in users table)
    connection.execute(sa.text("""
        UPDATE share_assignments sa
        INNER JOIN token_share_users tsu ON sa.token_share_user_id = tsu.id
        SET sa.user_id = (SELECT MIN(id) FROM token_share_users WHERE email = tsu.email)
    """))
    
    # Make user_id not nullable after data migration
    op.alter_column('share_assignments', 'user_id', nullable=False)
    
    # Add foreign key constraint
    op.create_foreign_key('fk_share_assignments_user', 'share_assignments', 'token_users', ['user_id'], ['id'], ondelete='CASCADE')
    op.create_index('idx_share_assignments_user_id', 'share_assignments', ['user_id'])
    
    # Drop old token_share_user_id column and its constraints
    op.drop_constraint('fk_share_assignments_token_share_user', 'share_assignments', type_='foreignkey')
    op.drop_index('idx_share_assignments_token_share_user_id', 'share_assignments')
    op.drop_column('share_assignments', 'token_share_user_id')
    
    # Step 4: Update token_user_login_challenges to reference token_users
    # Drop old FK constraint
    op.drop_constraint('token_user_login_challenges_ibfk_1', 'token_user_login_challenges', type_='foreignkey')
    
    # Update token_user_id to reference new consolidated user IDs
    connection.execute(sa.text("""
        UPDATE token_user_login_challenges lc
        INNER JOIN token_share_users tsu ON lc.token_user_id = tsu.id
        SET lc.token_user_id = (SELECT MIN(id) FROM token_share_users WHERE email = tsu.email)
    """))
    
    # Add new FK constraint pointing to token_users
    op.create_foreign_key('fk_login_challenges_user', 'token_user_login_challenges', 'token_users', ['token_user_id'], ['id'], ondelete='CASCADE')
    
    # Finally, drop the old token_share_users table
    op.drop_index('idx_token_share_users_email', 'token_share_users')
    op.drop_table('token_share_users')


def downgrade():
    """
    Reverse the restructuring - recreate token_share_users table.
    Note: This will lose the many-to-many relationships (users assigned to multiple tokens
    will be duplicated back to separate records).
    """
    
    # Recreate token_share_users table
    op.create_table(
        'token_share_users',
        sa.Column('id', sa.String(length=36), nullable=False),
        sa.Column('token_deployment_id', sa.String(length=36), nullable=False),
        sa.Column('email', sa.String(length=255), nullable=False),
        sa.Column('name', sa.String(length=255), nullable=False),
        sa.Column('phone', sa.String(length=20), nullable=True),
        sa.Column('password_hash', sa.String(length=255), nullable=False),
        sa.Column('mfa_secret', sa.String(length=255), nullable=True),
        sa.Column('mfa_enabled', sa.Boolean(), nullable=False, server_default='0'),
        sa.Column('created_at', sa.DateTime(), nullable=False, server_default=sa.text('CURRENT_TIMESTAMP')),
        sa.Column('updated_at', sa.DateTime(), nullable=False, server_default=sa.text('CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP')),
        sa.ForeignKeyConstraint(['token_deployment_id'], ['token_deployments.id'], ondelete='CASCADE'),
        sa.PrimaryKeyConstraint('id'),
        sa.UniqueConstraint('email', 'token_deployment_id', name='uq_email_per_token')
    )
    op.create_index('idx_token_share_users_email', 'token_share_users', ['email'])
    
    # Migrate data back from users + token_user_assignments to token_share_users
    connection = op.get_bind()
    
    connection.execute(sa.text("""
        INSERT INTO token_share_users (id, token_deployment_id, email, name, phone, password_hash, mfa_secret, mfa_enabled, created_at, updated_at)
        SELECT 
            tua.id,
            tua.token_deployment_id,
            u.email,
            u.name,
            u.phone,
            u.password_hash,
            u.mfa_secret,
            u.mfa_enabled,
            tua.created_at,
            tua.updated_at
        FROM token_user_assignments tua
        INNER JOIN users u ON tua.user_id = u.id
    """))
    
    # Restore share_assignments.token_share_user_id column
    op.add_column('share_assignments', sa.Column('token_share_user_id', sa.String(length=36), nullable=True))
    
    # Copy user_id back to token_share_user_id
    connection.execute(sa.text("""
        UPDATE share_assignments sa
        INNER JOIN token_user_assignments tua ON sa.user_id = tua.user_id
        SET sa.token_share_user_id = tua.id
    """))
    
    op.alter_column('share_assignments', 'token_share_user_id', nullable=False)
    op.create_foreign_key('fk_share_assignments_token_share_user', 'share_assignments', 'token_share_users', ['token_share_user_id'], ['id'], ondelete='CASCADE')
    op.create_index('idx_share_assignments_token_share_user_id', 'share_assignments', ['token_share_user_id'])
    
    # Drop new user_id column
    op.drop_constraint('fk_share_assignments_user', 'share_assignments', type_='foreignkey')
    op.drop_index('idx_share_assignments_user_id', 'share_assignments')
    op.drop_column('share_assignments', 'user_id')
    
    # Drop new tables
    op.drop_index('idx_token_user_assignments_token_id', 'token_user_assignments')
    op.drop_index('idx_token_user_assignments_user_id', 'token_user_assignments')
    op.drop_table('token_user_assignments')
    
    op.drop_index('idx_users_email', 'token_users')
    op.drop_table('token_users')

"""add soft delete fields to share_files

Revision ID: 020_add_share_file_soft_delete
Revises: 019_restructure_users_table
Create Date: 2026-02-07
"""
from alembic import op
import sqlalchemy as sa


# revision identifiers, used by Alembic.
revision = "020_add_share_file_soft_delete"
down_revision = "019_restructure_users_table"
branch_labels = None
depends_on = None


def upgrade() -> None:
    op.drop_constraint("uq_share_files_token_share", "share_files", type_="unique")
    op.add_column(
        "share_files",
        sa.Column("is_active", sa.Boolean(), server_default=sa.text("TRUE"), nullable=False),
    )
    op.add_column(
        "share_files",
        sa.Column("replaced_at_utc", sa.DateTime(timezone=True), nullable=True),
    )
    op.create_index("ix_share_files_active", "share_files", ["is_active"])


def downgrade() -> None:
    op.drop_index("ix_share_files_active", table_name="share_files")
    op.drop_column("share_files", "replaced_at_utc")
    op.drop_column("share_files", "is_active")
    op.create_unique_constraint("uq_share_files_token_share", "share_files", ["token_deployment_id", "share_number"])

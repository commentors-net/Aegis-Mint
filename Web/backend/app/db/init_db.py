from sqlalchemy.orm import Session

from app.core import security
from app.models import Desktop, DesktopStatus, GovernanceAssignment, User, UserRole


def seed_data(db: Session):
    """Create sample users/desktops/assignment for quick testing."""
    # Only seed if there are no super admins; otherwise, respect existing data.
    if db.query(User).filter(User.role == UserRole.SUPER_ADMIN).first():
        return

    admin_email = "admin@example.com"
    gov_email = "gov@example.com"
    desktop_id = "52a3f9d2-1111-2222-3333-acde1234abcd"
    admin_secret = None
    gov_secret = None

    admin = User(
        email=admin_email,
        role=UserRole.SUPER_ADMIN,
        password_hash=security.hash_password("ChangeMe123!"),
        mfa_secret=admin_secret,
        is_active=True,
    )
    db.add(admin)
    db.commit()
    db.refresh(admin)

    gov = User(
        email=gov_email,
        role=UserRole.GOVERNANCE_AUTHORITY,
        password_hash=security.hash_password("GovPass123!"),
        mfa_secret=gov_secret,
        is_active=True,
    )
    db.add(gov)
    db.commit()
    db.refresh(gov)

    desktop = Desktop(
        desktop_app_id=desktop_id,
        name_label="Office-PC1",
        status=DesktopStatus.ACTIVE,
        required_approvals_n=2,
        unlock_minutes=15,
    )
    db.add(desktop)
    db.commit()
    db.refresh(desktop)

    db.add(GovernanceAssignment(user_id=gov.id, desktop_app_id=desktop.desktop_app_id))
    db.commit()

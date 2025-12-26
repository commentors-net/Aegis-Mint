import pytest
from fastapi import HTTPException
from sqlalchemy import create_engine
from sqlalchemy.orm import sessionmaker

from app.core import security
from app.db.base import Base
from app.models import Desktop, DesktopStatus, User, UserRole
from app.services import approval_service


@pytest.fixture()
def db():
    engine = create_engine("sqlite:///:memory:", future=True)
    TestingSessionLocal = sessionmaker(bind=engine, autoflush=False, autocommit=False, expire_on_commit=False, future=True)
    Base.metadata.create_all(bind=engine)
    session = TestingSessionLocal()
    try:
        yield session
    finally:
        session.close()


def seed(db):
    desktop = Desktop(
        desktop_app_id="test-desktop",
        status=DesktopStatus.ACTIVE,
        required_approvals_n=2,
        unlock_minutes=15,
    )
    user = User(
        email="gov@example.com",
        password_hash=security.hash_password("Secret123!"),
        role=UserRole.GOVERNANCE_AUTHORITY,
        mfa_secret="BASE32SECRET3232",
    )
    db.add_all([desktop, user])
    db.commit()
    db.refresh(desktop)
    db.refresh(user)
    return desktop, user


def test_unique_approval(db):
    desktop, user = seed(db)
    approval_service.add_approval(db, desktop, user)
    with pytest.raises(HTTPException) as exc:
        approval_service.add_approval(db, desktop, user)
    assert exc.value.status_code == 409


def test_unlock_after_required_approvals(db):
    desktop, user = seed(db)
    other = User(
        email="gov2@example.com",
        password_hash=security.hash_password("Secret123!"),
        role=UserRole.GOVERNANCE_AUTHORITY,
        mfa_secret="BASE32SECRET3233",
    )
    db.add(other)
    db.commit()
    db.refresh(other)

    session = approval_service.add_approval(db, desktop, user)
    assert session.unlocked_until_utc is None
    session = approval_service.add_approval(db, desktop, other)
    assert session.status.value == "Unlocked"
    assert session.unlocked_until_utc is not None

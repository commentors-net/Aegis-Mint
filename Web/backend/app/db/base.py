from sqlalchemy.orm import declarative_base

Base = declarative_base()

# Import models so Alembic can discover metadata
from app.models import (  # noqa: E402,F401
    assignment,
    audit,
    approval,
    desktop,
    login_challenge,
    session,
    setting,
    user,
)

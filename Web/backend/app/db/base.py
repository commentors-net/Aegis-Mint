from sqlalchemy.orm import declarative_base

Base = declarative_base()

# Import models so Alembic can discover metadata
from app.models import user, desktop, session, approval, assignment, audit, setting  # noqa: E402,F401

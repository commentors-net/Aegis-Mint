import json
from typing import Any

from sqlalchemy.orm import Session

from app.core.time import utcnow
from app.models import AuditLog


def _normalize_details(details: Any) -> str | None:
    if details is None:
        return None
    if isinstance(details, str):
        return json.dumps({"message": details})
    try:
        return json.dumps(details, default=str)
    except TypeError:
        return json.dumps({"repr": repr(details)})


def log_audit(
    db: Session,
    action: str,
    actor_user_id: str | None = None,
    desktop_app_id: str | None = None,
    session_id: str | None = None,
    details: Any = None,
):
    entry = AuditLog(
        at_utc=utcnow(),
        action=action,
        actor_user_id=actor_user_id,
        desktop_app_id=desktop_app_id,
        session_id=session_id,
        details=_normalize_details(details),
    )
    db.add(entry)
    db.commit()

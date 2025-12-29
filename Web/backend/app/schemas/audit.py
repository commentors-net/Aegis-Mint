from datetime import datetime
from typing import Optional

from pydantic import BaseModel


class AuditLogEntry(BaseModel):
    id: str
    at_utc: datetime
    action: str
    actor_user_id: Optional[str] = None
    desktop_app_id: Optional[str] = None
    session_id: Optional[str] = None
    details: Optional[str] = None

    model_config = {"from_attributes": True}


class AuditPage(BaseModel):
    items: list[AuditLogEntry]
    total: int
    page: int
    pageSize: int

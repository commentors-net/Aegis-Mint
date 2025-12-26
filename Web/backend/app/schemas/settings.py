from typing import Optional

from pydantic import BaseModel, Field


class SystemSettings(BaseModel):
    requiredApprovalsDefault: Optional[int] = Field(None, alias="required_approvals_default")
    unlockMinutesDefault: Optional[int] = Field(None, alias="unlock_minutes_default")

    model_config = {"populate_by_name": True}

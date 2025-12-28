from functools import lru_cache
from typing import List

from pydantic_settings import BaseSettings, SettingsConfigDict


def _split_csv(value: str | List[str]) -> List[str]:
    if isinstance(value, list):
        return value
    return [v.strip() for v in str(value).split(",") if v and v.strip()]


class Settings(BaseSettings):
    model_config = SettingsConfigDict(
        env_file=".env",
        env_prefix="TOKENCONTROL_",
        extra="ignore",
    )

    app_name: str = "AegisMint Governance API"
    # values must come from environment/.env to avoid hardcoding secrets
    database_url: str = ""
    db_echo: bool = False

    jwt_secret: str = ""
    jwt_issuer: str = ""
    access_token_exp_minutes: int = 15
    refresh_token_exp_minutes: int = 24 * 60

    auth_challenge_minutes: int = 5
    totp_issuer: str = "AegisMint"

    unlock_minutes_default: int = 15
    required_approvals_default: int = 2

    cors_origins_raw: str = "http://localhost:5173"
    enable_docs: bool = True

    @property
    def cors_origins(self) -> List[str]:
        parsed = _split_csv(self.cors_origins_raw)
        return parsed or ["http://localhost:5173"]


@lru_cache(maxsize=1)
def get_settings() -> Settings:
    return Settings()

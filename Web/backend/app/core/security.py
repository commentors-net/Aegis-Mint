from datetime import datetime, timedelta, timezone
from typing import Any, Dict

import jwt
from passlib.context import CryptContext

from .config import get_settings
from .time import utcnow


# Use pbkdf2_sha256 as primary to avoid bcrypt backend issues; keep bcrypt variants for legacy verification.
pwd_context = CryptContext(
    schemes=["pbkdf2_sha256", "bcrypt_sha256", "bcrypt"],
    deprecated="auto",
    bcrypt__truncate_error=False,
)
ALGORITHM = "HS256"


def hash_password(password: str) -> str:
    return pwd_context.hash(password)


def verify_password(password: str, password_hash: str) -> bool:
    return pwd_context.verify(password, password_hash)


def create_token(payload: Dict[str, Any], expires_minutes: int) -> str:
    settings = get_settings()
    now = utcnow()
    expire = now + timedelta(minutes=expires_minutes)
    claims = {
        **payload,
        "iss": settings.jwt_issuer,
        "iat": now,
        "exp": expire,
    }
    return jwt.encode(claims, settings.jwt_secret, algorithm=ALGORITHM)


def decode_token(token: str) -> Dict[str, Any]:
    settings = get_settings()
    return jwt.decode(
        token,
        settings.jwt_secret,
        issuer=settings.jwt_issuer,
        algorithms=[ALGORITHM],
        options={"require": ["iss", "iat", "exp"]},
    )


def create_access_token(user_id: str, role: str) -> str:
    settings = get_settings()
    return create_token({"sub": user_id, "role": role, "type": "access"}, settings.access_token_exp_minutes)


def create_refresh_token(user_id: str, role: str) -> str:
    settings = get_settings()
    return create_token({"sub": user_id, "role": role, "type": "refresh"}, settings.refresh_token_exp_minutes)


def verify_token(token: str) -> Dict[str, Any]:
    """Verify and decode a JWT token."""
    return decode_token(token)

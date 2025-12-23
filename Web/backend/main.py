from datetime import datetime, timedelta, timezone
import os
import secrets
from typing import Optional

import jwt
import pyotp
from dotenv import load_dotenv
from fastapi import Depends, FastAPI, Header, HTTPException, Request
from fastapi.responses import JSONResponse
from pydantic import BaseModel, Field

# Load .env if present for local/dev
load_dotenv()


def get_settings():
    """Lazy settings loader."""
    return {
        "jwt_secret": os.getenv("TOKENCONTROL_JWT_SECRET", "change-me"),
        "jwt_issuer": os.getenv("TOKENCONTROL_JWT_ISSUER", "aegismint-gov"),
        "user_name": os.getenv("TOKENCONTROL_USER", "operator"),
        "user_password": os.getenv("TOKENCONTROL_PASSWORD", "StrongPassword!"),
        "totp_secret": os.getenv("TOKENCONTROL_TOTP_SECRET", "base32secret3232"),
        "unlock_window_minutes": int(os.getenv("TOKENCONTROL_UNLOCK_MINUTES", "15")),
    }


class LoginRequest(BaseModel):
    username: str
    password: str
    totp: str = Field(..., description="6-digit TOTP code")


class TokenResponse(BaseModel):
    access_token: str
    token_type: str = "bearer"
    expires_at: datetime


class UnlockRequest(BaseModel):
    reason: Optional[str] = Field(None, description="Why unlock is requested (ticket, justification)")


class UnlockResponse(BaseModel):
    unlocked_until: datetime
    window_minutes: int


app = FastAPI(
    title="Aegis Token Control Governance",
    version="0.1.0",
    docs_url=None,
    redoc_url=None,
)


def no_store(response: JSONResponse):
    response.headers["Cache-Control"] = "no-store, no-cache, must-revalidate"
    response.headers["Pragma"] = "no-cache"
    return response


def issue_jwt(username: str) -> TokenResponse:
    settings = get_settings()
    expiry = datetime.now(tz=timezone.utc) + timedelta(minutes=settings["unlock_window_minutes"])
    payload = {
        "sub": username,
        "iss": settings["jwt_issuer"],
        "exp": expiry,
        "iat": datetime.now(tz=timezone.utc),
        "scope": "unlock",
    }
    token = jwt.encode(payload, settings["jwt_secret"], algorithm="HS256")
    return TokenResponse(access_token=token, expires_at=expiry)


def validate_totp(code: str) -> bool:
    settings = get_settings()
    totp = pyotp.TOTP(settings["totp_secret"])
    return totp.verify(code, valid_window=1)


def authenticate(request: LoginRequest):
    settings = get_settings()
    if not secrets.compare_digest(request.username, settings["user_name"]):
        raise HTTPException(status_code=401, detail="Invalid credentials")
    if not secrets.compare_digest(request.password, settings["user_password"]):
        raise HTTPException(status_code=401, detail="Invalid credentials")
    if not validate_totp(request.totp):
        raise HTTPException(status_code=401, detail="Invalid TOTP")


def require_token(authorization: str = Header("")) -> dict:
    if not authorization.lower().startswith("bearer "):
        raise HTTPException(status_code=401, detail="Missing bearer token")
    token = authorization.split(" ", 1)[1].strip()
    settings = get_settings()
    try:
        payload = jwt.decode(
            token,
            settings["jwt_secret"],
            algorithms=["HS256"],
            issuer=settings["jwt_issuer"],
            options={"require": ["exp", "iat", "iss", "sub"]},
        )
        if payload.get("scope") != "unlock":
            raise HTTPException(status_code=403, detail="Invalid scope")
        return payload
    except jwt.ExpiredSignatureError:
        raise HTTPException(status_code=401, detail="Token expired")
    except jwt.InvalidTokenError:
        raise HTTPException(status_code=401, detail="Invalid token")


@app.middleware("http")
async def add_no_cache_headers(request: Request, call_next):
    response = await call_next(request)
    if isinstance(response, JSONResponse):
        no_store(response)
    return response


@app.post("/auth/login", response_model=TokenResponse)
def login(body: LoginRequest):
    authenticate(body)
    token = issue_jwt(body.username)
    return no_store(JSONResponse(token.model_dump()))


@app.post("/unlock", response_model=UnlockResponse)
def unlock(body: UnlockRequest, claims: dict = Depends(require_token)):
    settings = get_settings()
    window_minutes = settings["unlock_window_minutes"]
    unlocked_until = datetime.now(tz=timezone.utc) + timedelta(minutes=window_minutes)
    # TODO: wire this to the actual box unlock controller (RPC or message bus).
    # For now we just acknowledge and return window metadata.
    response = UnlockResponse(unlocked_until=unlocked_until, window_minutes=window_minutes)
    return no_store(JSONResponse(response.model_dump()))

"""Authentication API endpoints - proxy to main backend."""
import logging
from typing import Dict, Any

import httpx
from fastapi import APIRouter, HTTPException, status
from pydantic import BaseModel, EmailStr, Field

from app.core.config import settings

logger = logging.getLogger(__name__)
router = APIRouter()


class LoginRequest(BaseModel):
    """Login request model."""
    email: EmailStr
    password: str = Field(..., min_length=8)


class LoginResponse(BaseModel):
    """Login response model."""
    challenge_id: str
    mfa_secret_base32: str | None = None
    otpauth_url: str | None = None
    mfa_qr_base64: str | None = None


class VerifyOtpRequest(BaseModel):
    """OTP verification request."""
    challenge_id: str
    otp: str = Field(..., min_length=6, max_length=6)


class VerifyOtpResponse(BaseModel):
    """OTP verification response."""
    access_token: str
    refresh_token: str
    expires_at: str
    user_id: str
    user_email: str
    user_name: str
    token_deployment_id: str


class RefreshTokenRequest(BaseModel):
    """Refresh token request."""
    refresh_token: str


@router.post("/login", response_model=LoginResponse)
async def login(body: LoginRequest):
    """
    Forward login request to main backend.
    Returns challenge_id and MFA setup info if needed.
    """
    logger.info(f"[ClientWeb -> Web] Login request for user: {body.email}")
    try:
        backend_url = f"{settings.backend_api_url}/api/token-user-auth/login"
        payload = {"email": body.email, "password": body.password}
        logger.debug(f"[ClientWeb -> Web] POST {backend_url}")
        logger.debug(f"[ClientWeb -> Web] Payload (password hidden): {{email: {body.email}}}")
        
        async with httpx.AsyncClient(timeout=30.0) as client:
            response = await client.post(backend_url, json=payload)
            
            logger.info(f"[Web -> ClientWeb] Status: {response.status_code}")
            logger.debug(f"[Web -> ClientWeb] Response headers: {dict(response.headers)}")
            
            if response.status_code == 401:
                logger.warning(f"[ClientWeb] Authentication failed for: {body.email}")
                raise HTTPException(
                    status_code=status.HTTP_401_UNAUTHORIZED,
                    detail="Invalid email or password"
                )
            
            if response.status_code != 200:
                logger.error(f"[Web -> ClientWeb] Backend error: {response.status_code}")
                logger.error(f"[Web -> ClientWeb] Error details: {response.text}")
                raise HTTPException(
                    status_code=status.HTTP_502_BAD_GATEWAY,
                    detail="Authentication service unavailable"
                )
            
            result = response.json()
            logger.info(f"[ClientWeb] Login successful for {body.email}, challenge_id: {result.get('challenge_id')}")
            logger.debug(f"[ClientWeb] MFA setup required: {result.get('mfa_secret_base32') is not None}")
            return result
    
    except httpx.RequestError as e:
        logger.error(f"Failed to connect to backend: {e}")
        raise HTTPException(
            status_code=status.HTTP_503_SERVICE_UNAVAILABLE,
            detail="Authentication service unavailable"
        )


@router.post("/verify-otp", response_model=VerifyOtpResponse)
async def verify_otp(body: VerifyOtpRequest):
    """
    Forward OTP verification to main backend.
    Returns access/refresh tokens on success.
    """
    logger.info(f"[ClientWeb -> Web] Verify OTP for challenge: {body.challenge_id}")
    try:
        backend_url = f"{settings.backend_api_url}/api/token-user-auth/verify-otp"
        payload = {"challenge_id": body.challenge_id, "otp": body.otp}
        logger.debug(f"[ClientWeb -> Web] POST {backend_url}")
        logger.debug(f"[ClientWeb -> Web] Payload: {{challenge_id: {body.challenge_id}, otp: ******}}")
        
        async with httpx.AsyncClient(timeout=30.0) as client:
            response = await client.post(backend_url, json=payload)
            
            logger.info(f"[Web -> ClientWeb] Status: {response.status_code}")
            
            if response.status_code == 401:
                logger.warning(f"[ClientWeb] OTP verification failed for challenge: {body.challenge_id}")
                raise HTTPException(
                    status_code=status.HTTP_401_UNAUTHORIZED,
                    detail="Invalid or expired OTP code"
                )
            
            if response.status_code != 200:
                logger.error(f"[Web -> ClientWeb] Backend error: {response.status_code}")
                logger.error(f"[Web -> ClientWeb] Error details: {response.text}")
                raise HTTPException(
                    status_code=status.HTTP_502_BAD_GATEWAY,
                    detail="Authentication service unavailable"
                )
            
            result = response.json()
            logger.info(f"[ClientWeb] OTP verified, user: {result.get('user_email')}")
            logger.debug(f"[ClientWeb] Token issued for user_id: {result.get('user_id')}")
            return result
    
    except httpx.RequestError as e:
        logger.error(f"Failed to connect to backend: {e}")
        raise HTTPException(
            status_code=status.HTTP_503_SERVICE_UNAVAILABLE,
            detail="Authentication service unavailable"
        )


@router.post("/refresh", response_model=VerifyOtpResponse)
async def refresh_token(body: RefreshTokenRequest):
    """
    Forward token refresh request to main backend.
    Returns new access/refresh tokens.
    """
    logger.info("[ClientWeb -> Web] Token refresh request")
    try:
        backend_url = f"{settings.backend_api_url}/api/token-user-auth/refresh"
        logger.debug(f"[ClientWeb -> Web] POST {backend_url}")
        
        async with httpx.AsyncClient(timeout=30.0) as client:
            response = await client.post(
                backend_url,
                json={"refresh_token": body.refresh_token}
            )
            
            logger.info(f"[Web -> ClientWeb] Status: {response.status_code}")
            
            if response.status_code == 401:
                logger.warning("[ClientWeb] Token refresh failed - invalid or expired")
                raise HTTPException(
                    status_code=status.HTTP_401_UNAUTHORIZED,
                    detail="Invalid or expired refresh token"
                )
            
            if response.status_code != 200:
                logger.error(f"[Web -> ClientWeb] Backend error: {response.status_code}")
                logger.error(f"[Web -> ClientWeb] Error details: {response.text}")
                raise HTTPException(
                    status_code=status.HTTP_502_BAD_GATEWAY,
                    detail="Authentication service unavailable"
                )
            
            result = response.json()
            logger.info(f"[ClientWeb] Token refreshed for user: {result.get('user_email')}")
            return result
    
    except httpx.RequestError as e:
        logger.error(f"Failed to connect to backend: {e}")
        raise HTTPException(
            status_code=status.HTTP_503_SERVICE_UNAVAILABLE,
            detail="Authentication service unavailable"
        )

"""Shares API endpoints - proxy to main backend with authentication."""
import logging
from typing import List

import httpx
from fastapi import APIRouter, HTTPException, Header, status
from fastapi.responses import Response
from pydantic import BaseModel

from app.core.config import settings

logger = logging.getLogger(__name__)
router = APIRouter()


class ShareItem(BaseModel):
    """Share item model."""
    assignment_id: str
    share_file_id: str
    share_number: int
    token_name: str
    token_symbol: str
    token_address: str | None
    download_allowed: bool
    download_count: int
    first_downloaded_at_utc: str | None
    last_downloaded_at_utc: str | None


@router.get("/my-shares", response_model=List[ShareItem])
async def get_my_shares(authorization: str = Header(None)):
    """
    Get all shares assigned to the current user.
    Forwards request to main backend with auth token.
    """
    logger.info("[ClientWeb -> Web] Get my shares request")
    
    if not authorization:
        logger.warning("[ClientWeb] Missing authorization header")
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="Missing authorization header"
        )
    
    try:
        backend_url = f"{settings.backend_api_url}/api/my-shares"
        logger.debug(f"[ClientWeb -> Web] GET {backend_url}")
        logger.debug(f"[ClientWeb -> Web] Auth header present: {authorization[:20]}...")
        
        async with httpx.AsyncClient(timeout=30.0) as client:
            response = await client.get(
                backend_url,
                headers={"Authorization": authorization}
            )
            
            logger.info(f"[Web -> ClientWeb] Status: {response.status_code}")
            
            if response.status_code == 401:
                logger.warning("[ClientWeb] Token validation failed")
                raise HTTPException(
                    status_code=status.HTTP_401_UNAUTHORIZED,
                    detail="Invalid or expired token"
                )
            
            if response.status_code != 200:
                logger.error(f"[Web -> ClientWeb] Backend error: {response.status_code}")
                logger.error(f"[Web -> ClientWeb] Error details: {response.text}")
                raise HTTPException(
                    status_code=status.HTTP_502_BAD_GATEWAY,
                    detail="Service unavailable"
                )
            
            result = response.json()
            logger.info(f"[ClientWeb] Retrieved {len(result)} shares")
            return result
    
    except httpx.RequestError as e:
        logger.error(f"Failed to connect to backend: {e}")
        raise HTTPException(
            status_code=status.HTTP_503_SERVICE_UNAVAILABLE,
            detail="Service unavailable"
        )


@router.get("/download/{assignment_id}")
async def download_share(assignment_id: str, authorization: str = Header(None)):
    """
    Download share file.
    Forwards request to main backend with auth token.
    """
    logger.info(f"[ClientWeb -> Web] Download share request for assignment: {assignment_id}")
    
    if not authorization:
        logger.warning("[ClientWeb] Missing authorization header")
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="Missing authorization header"
        )
    
    try:
        backend_url = f"{settings.backend_api_url}/api/my-shares/download/{assignment_id}"
        logger.debug(f"[ClientWeb -> Web] GET {backend_url}")
        
        async with httpx.AsyncClient(timeout=60.0) as client:
            response = await client.get(
                backend_url,
                headers={"Authorization": authorization}
            )
            
            logger.info(f"[Web -> ClientWeb] Status: {response.status_code}")
            
            if response.status_code == 401:
                logger.warning("[ClientWeb] Token validation failed")
                raise HTTPException(
                    status_code=status.HTTP_401_UNAUTHORIZED,
                    detail="Invalid or expired token"
                )
            
            if response.status_code == 403:
                logger.warning(f"[ClientWeb] Download not allowed for assignment: {assignment_id}")
                raise HTTPException(
                    status_code=status.HTTP_403_FORBIDDEN,
                    detail="Download not allowed or already downloaded"
                )
            
            if response.status_code == 404:
                logger.warning(f"[ClientWeb] Share not found: {assignment_id}")
                raise HTTPException(
                    status_code=status.HTTP_404_NOT_FOUND,
                    detail="Share not found"
                )
            
            if response.status_code != 200:
                logger.error(f"[Web -> ClientWeb] Backend error: {response.status_code}")
                logger.error(f"[Web -> ClientWeb] Error details: {response.text}")
                raise HTTPException(
                    status_code=status.HTTP_502_BAD_GATEWAY,
                    detail="Service unavailable"
                )
            
            logger.info(f"[ClientWeb] Share download successful, size: {len(response.content)} bytes")
            # Forward the file download response
            return Response(
                content=response.content,
                media_type=response.headers.get("content-type", "application/json"),
                headers={
                    "Content-Disposition": response.headers.get("content-disposition", "attachment")
                }
            )
    
    except httpx.RequestError as e:
        logger.error(f"Failed to connect to backend: {e}")
        raise HTTPException(
            status_code=status.HTTP_503_SERVICE_UNAVAILABLE,
            detail="Service unavailable"
        )


@router.get("/history")
async def get_download_history(authorization: str = Header(None)):
    """
    Get download history for current user.
    Forwards request to main backend with auth token.
    """
    logger.info("[ClientWeb -> Web] Get download history request")
    
    if not authorization:
        logger.warning("[ClientWeb] Missing authorization header")
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="Missing authorization header"
        )
    
    try:
        backend_url = f"{settings.backend_api_url}/api/my-shares/history"
        logger.debug(f"[ClientWeb -> Web] GET {backend_url}")
        
        async with httpx.AsyncClient(timeout=30.0) as client:
            response = await client.get(
                backend_url,
                headers={"Authorization": authorization}
            )
            
            logger.info(f"[Web -> ClientWeb] Status: {response.status_code}")
            
            if response.status_code == 401:
                logger.warning("[ClientWeb] Token validation failed")
                raise HTTPException(
                    status_code=status.HTTP_401_UNAUTHORIZED,
                    detail="Invalid or expired token"
                )
            
            if response.status_code != 200:
                logger.error(f"[Web -> ClientWeb] Backend error: {response.status_code}")
                logger.error(f"[Web -> ClientWeb] Error details: {response.text}")
                raise HTTPException(
                    status_code=status.HTTP_502_BAD_GATEWAY,
                    detail="Service unavailable"
                )
            
            result = response.json()
            logger.info(f"[ClientWeb] Retrieved {len(result)} history entries")
            return result
    
    except httpx.RequestError as e:
        logger.error(f"Failed to connect to backend: {e}")
        raise HTTPException(
            status_code=status.HTTP_503_SERVICE_UNAVAILABLE,
            detail="Service unavailable"
        )

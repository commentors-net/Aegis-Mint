"""
HMAC Signature Authentication for Desktop Applications
"""
import hashlib
import hmac
import base64
import time
from typing import Optional
from fastapi import HTTPException, status


def verify_desktop_signature(
    desktop_app_id: str,
    timestamp: str,
    signature: str,
    secret_key: str,
    request_body: str = "",
    max_time_drift: int = 300  # 5 minutes
) -> tuple[bool, Optional[str]]:
    """
    Verify HMAC-SHA256 signature for desktop API requests.
    
    Args:
        desktop_app_id: Desktop application ID
        timestamp: Unix timestamp from request header
        signature: Base64-encoded HMAC signature from request header
        secret_key: Base64-encoded secret key for this desktop
        request_body: JSON request body (empty string if no body)
        max_time_drift: Maximum allowed time difference in seconds
        
    Returns:
        Tuple of (is_valid, error_reason)
    """
    try:
        # Verify timestamp to prevent replay attacks
        request_time = int(timestamp)
        current_time = int(time.time())
        time_diff = abs(current_time - request_time)
        
        if time_diff > max_time_drift:
            return False, f"Timestamp out of range (diff: {time_diff}s, max: {max_time_drift}s)"
        
        # Construct message: {desktopAppId}:{timestamp}:{requestBody}
        message = f"{desktop_app_id}:{timestamp}:{request_body}"
        
        # Decode secret key
        key_bytes = base64.b64decode(secret_key)
        message_bytes = message.encode('utf-8')
        
        # Compute HMAC-SHA256
        expected_hmac = hmac.new(key_bytes, message_bytes, hashlib.sha256).digest()
        expected_signature = base64.b64encode(expected_hmac).decode('utf-8')
        
        # Compare signatures (constant-time comparison)
        provided_signature_bytes = base64.b64decode(signature)
        expected_signature_bytes = base64.b64decode(expected_signature)
        
        is_valid = hmac.compare_digest(provided_signature_bytes, expected_signature_bytes)
        if not is_valid:
            return False, "Signature mismatch"
        
        return True, None
        
    except Exception as e:
        return False, f"Validation error: {str(e)}"


def generate_secret_key() -> str:
    """
    Generate a new random secret key for HMAC signing.
    
    Returns:
        Base64-encoded 32-byte random key
    """
    import secrets
    key_bytes = secrets.token_bytes(32)
    return base64.b64encode(key_bytes).decode('utf-8')


def validate_desktop_auth_headers(desktop_app_id: str, timestamp: str, signature: str, secret_key: str, body: str = ""):
    """
    Validate desktop authentication headers and raise HTTPException if invalid.
    
    Args:
        desktop_app_id: Desktop application ID from header
        timestamp: Unix timestamp from header
        signature: HMAC signature from header
        secret_key: Secret key for this desktop
        body: Request body JSON string
        
    Raises:
        HTTPException: If authentication fails
    """
    if not desktop_app_id:
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="Missing X-Desktop-Id header"
        )
    
    if not timestamp:
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="Missing X-Desktop-Timestamp header"
        )
    
    if not signature:
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="Missing X-Desktop-Signature header"
        )
    
    is_valid, error_reason = verify_desktop_signature(desktop_app_id, timestamp, signature, secret_key, body)
    if not is_valid:
        # Log for debugging
        print(f"[HMAC Auth Failed] Desktop: {desktop_app_id}, Reason: {error_reason}")
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail=f"Authentication failed: {error_reason}"
        )

"""Service for recovering mnemonic from encrypted Shamir shares."""
import base64
import hashlib
import json
import re
from typing import List, Dict, Any

from Crypto.Cipher import AES
from Crypto.Util.Padding import unpad
from fastapi import HTTPException, status


APP_SALT = "AegisMint-Recovery-v1-2026"


def derive_encryption_key(salt: str) -> bytes:
    """Derive 256-bit encryption key from salt using PBKDF2."""
    return hashlib.pbkdf2_hmac('sha256', salt.encode('utf-8'), b'', 100000, dklen=32)


def decrypt_mnemonic(encrypted_hex: str, iv_hex: str, key: bytes) -> str:
    """Decrypt AES-256-CBC encrypted mnemonic."""
    try:
        encrypted_bytes = bytes.fromhex(encrypted_hex)
        iv = bytes.fromhex(iv_hex)
        
        cipher = AES.new(key, AES.MODE_CBC, iv)
        decrypted = unpad(cipher.decrypt(encrypted_bytes), AES.block_size)
        return decrypted.decode('utf-8')
    except Exception as e:
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail=f"Decryption failed: {str(e)}"
        )


def _gcd(a: int, b: int) -> int:
    """Calculate Greatest Common Divisor."""
    while b:
        a, b = b, a % b
    return a


def _extended_gcd(a: int, b: int) -> tuple:
    """Extended Euclidean Algorithm."""
    if a == 0:
        return b, 0, 1
    gcd, x1, y1 = _extended_gcd(b % a, a)
    x = y1 - (b // a) * x1
    y = x1
    return gcd, x, y


def _mod_inverse(a: int, m: int) -> int:
    """Calculate modular multiplicative inverse."""
    gcd, x, _ = _extended_gcd(a, m)
    if gcd != 1:
        raise ValueError("Modular inverse does not exist")
    return (x % m + m) % m


def _lagrange_interpolate(shares: List[tuple], prime: int) -> int:
    """
    Lagrange interpolation to recover secret from shares.
    
    Args:
        shares: List of (x, y) tuples
        prime: Prime modulus
        
    Returns:
        The secret (y-intercept at x=0)
    """
    secret = 0
    
    for i, (xi, yi) in enumerate(shares):
        numerator = 1
        denominator = 1
        
        for j, (xj, _) in enumerate(shares):
            if i != j:
                numerator = (numerator * (0 - xj)) % prime
                denominator = (denominator * (xi - xj)) % prime
        
        # Calculate lagrange basis polynomial
        lagrange_basis = (numerator * _mod_inverse(denominator, prime)) % prime
        secret = (secret + yi * lagrange_basis) % prime
    
    return secret


def _parse_share(share_string: str) -> tuple:
    """
    Parse share string format from C# Shamir implementation.
    Expected format: "index-hexvalue"
    
    Returns:
        Tuple of (index, hex_value)
    """
    parts = share_string.split('-', 1)
    if len(parts) != 2:
        raise ValueError(f"Invalid share format: {share_string}")
    
    try:
        index = int(parts[0])
        hex_value = parts[1]
        return index, hex_value
    except ValueError as e:
        raise ValueError(f"Failed to parse share: {str(e)}")


def _recover_hex_from_shares(share_strings: List[str]) -> str:
    """
    Recover hex string from Shamir shares.
    
    Args:
        share_strings: List of share strings in format "index-hexvalue"
        
    Returns:
        Recovered hex string
    """
    # Parse all shares
    parsed_shares = [_parse_share(s) for s in share_strings]
    
    # Get hex length from first share
    hex_length = len(parsed_shares[0][1])
    
    # Verify all shares have same length
    for idx, hex_val in parsed_shares:
        if len(hex_val) != hex_length:
            raise ValueError("All shares must have the same length")
    
    # Use a large prime (Mersenne prime 2^127 - 1)
    prime = 2**127 - 1
    
    # Reconstruct each byte
    result_bytes = []
    
    # Process two hex chars (one byte) at a time
    for pos in range(0, hex_length, 2):
        # Extract byte value from each share at this position
        shares_for_byte = []
        for idx, hex_val in parsed_shares:
            byte_hex = hex_val[pos:pos+2]
            byte_value = int(byte_hex, 16)
            shares_for_byte.append((idx, byte_value))
        
        # Recover byte using Lagrange interpolation
        recovered_byte = _lagrange_interpolate(shares_for_byte, prime)
        result_bytes.append(recovered_byte & 0xFF)
    
    # Convert bytes to hex string
    return ''.join(f'{b:02x}' for b in result_bytes)


def reconstruct_from_shares(share_files: List[Dict[str, Any]]) -> Dict[str, str]:
    """
    Reconstruct mnemonic from encrypted Shamir shares.
    
    Args:
        share_files: List of parsed share JSON objects
        
    Returns:
        Dict with 'mnemonic' and optional 'token_address'
        
    Raises:
        HTTPException: If share validation or reconstruction fails
    """
    if len(share_files) < 2:
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail="At least 2 shares are required"
        )
    
    if len(share_files) > 3:
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail="Maximum 3 shares allowed"
        )
    
    # Validate all shares
    encryption_version = None
    encrypted_mnemonic = None
    iv = None
    shares = []
    token_address = None
    
    for idx, share_data in enumerate(share_files):
        # Validate required fields
        if 'encryptionVersion' not in share_data:
            raise HTTPException(
                status_code=status.HTTP_400_BAD_REQUEST,
                detail=f"Share {idx + 1}: Missing encryptionVersion"
            )
        
        if share_data['encryptionVersion'] != 1:
            raise HTTPException(
                status_code=status.HTTP_400_BAD_REQUEST,
                detail=f"Share {idx + 1}: Unsupported encryption version {share_data['encryptionVersion']}"
            )
        
        if 'share' not in share_data:
            raise HTTPException(
                status_code=status.HTTP_400_BAD_REQUEST,
                detail=f"Share {idx + 1}: Missing share data"
            )
        
        if 'encryptedMnemonic' not in share_data:
            raise HTTPException(
                status_code=status.HTTP_400_BAD_REQUEST,
                detail=f"Share {idx + 1}: Missing encryptedMnemonic"
            )
        
        if 'iv' not in share_data:
            raise HTTPException(
                status_code=status.HTTP_400_BAD_REQUEST,
                detail=f"Share {idx + 1}: Missing initialization vector"
            )
        
        # First share sets the reference values
        if encryption_version is None:
            encryption_version = share_data['encryptionVersion']
            encrypted_mnemonic = share_data['encryptedMnemonic']
            iv = share_data['iv']
            token_address = share_data.get('tokenAddress')
        else:
            # All shares must have the same encrypted mnemonic and IV
            if share_data['encryptedMnemonic'] != encrypted_mnemonic:
                raise HTTPException(
                    status_code=status.HTTP_400_BAD_REQUEST,
                    detail=f"Share {idx + 1}: Encrypted mnemonic mismatch"
                )
            if share_data['iv'] != iv:
                raise HTTPException(
                    status_code=status.HTTP_400_BAD_REQUEST,
                    detail=f"Share {idx + 1}: IV mismatch"
                )
        
        shares.append(share_data['share'])
    
    # Reconstruct the encryption key from shares using Shamir
    try:
        reconstructed_key_hex = _recover_hex_from_shares(shares)
        reconstructed_key = bytes.fromhex(reconstructed_key_hex)
    except Exception as e:
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail=f"Failed to reconstruct encryption key from shares: {str(e)}"
        )
    
    # Decrypt the mnemonic using the reconstructed key
    mnemonic = decrypt_mnemonic(encrypted_mnemonic, iv, reconstructed_key)
    
    result = {"mnemonic": mnemonic}
    if token_address:
        result["token_address"] = token_address
    
    return result

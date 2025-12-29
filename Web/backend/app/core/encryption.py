"""
Encryption utilities for sensitive data
"""
import base64
from cryptography.fernet import Fernet
from cryptography.hazmat.primitives import hashes
from cryptography.hazmat.primitives.kdf.pbkdf2 import PBKDF2HMAC
from cryptography.hazmat.backends import default_backend
import os


# Master encryption key (should be stored securely in environment or vault)
# For production, load from environment variable or secure vault
MASTER_KEY = os.getenv("ENCRYPTION_MASTER_KEY", "default-key-change-in-production-32b")


def _get_fernet_key() -> bytes:
    """Derive Fernet key from master key"""
    kdf = PBKDF2HMAC(
        algorithm=hashes.SHA256(),
        length=32,
        salt=b'aegismint-salt',  # In production, use random salt stored with data
        iterations=100000,
        backend=default_backend()
    )
    key = base64.urlsafe_b64encode(kdf.derive(MASTER_KEY.encode()))
    return key


def encrypt_sensitive_data(data: bytes) -> bytes:
    """
    Encrypt sensitive data (like CA private keys)
    
    Args:
        data: Raw bytes to encrypt
        
    Returns:
        Encrypted bytes
    """
    f = Fernet(_get_fernet_key())
    return f.encrypt(data)


def decrypt_sensitive_data(encrypted_data: bytes) -> bytes:
    """
    Decrypt sensitive data
    
    Args:
        encrypted_data: Encrypted bytes
        
    Returns:
        Decrypted raw bytes
    """
    f = Fernet(_get_fernet_key())
    return f.decrypt(encrypted_data)

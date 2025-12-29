"""
Service for managing Certificate Authority persistence
"""
from datetime import datetime
from typing import Optional, Tuple
import base64
from sqlalchemy.orm import Session
from app.models.system_setting import SystemSetting
from app.core.encryption import encrypt_sensitive_data, decrypt_sensitive_data
from app.services.ca_service import CAService


class CAPersistenceService:
    """Service for storing and retrieving CA credentials"""
    
    CA_CERT_KEY = "ca_certificate_pem"
    CA_KEY_KEY = "ca_private_key_encrypted"
    CA_CREATED_KEY = "ca_created_at"
    CA_EXPIRES_KEY = "ca_expires_at"
    
    @staticmethod
    def generate_and_store_ca(db: Session) -> dict:
        """
        Generate new CA and store in database
        
        Returns:
            dict with ca_certificate, created_at, expires_at
        """
        # Check if CA already exists
        existing_ca = CAPersistenceService.get_ca_info(db)
        if existing_ca and not existing_ca.get('expired', False):
            raise ValueError("Active CA already exists. Cannot generate new CA while current is valid.")
        
        # Generate CA
        ca_cert_pem, ca_key_pem, created_at, expires_at = CAService.generate_ca()
        
        # Encrypt private key
        encrypted_key = encrypt_sensitive_data(ca_key_pem)
        
        # Store in database
        db.query(SystemSetting).filter(SystemSetting.key == CAPersistenceService.CA_CERT_KEY).delete()
        db.query(SystemSetting).filter(SystemSetting.key == CAPersistenceService.CA_KEY_KEY).delete()
        db.query(SystemSetting).filter(SystemSetting.key == CAPersistenceService.CA_CREATED_KEY).delete()
        db.query(SystemSetting).filter(SystemSetting.key == CAPersistenceService.CA_EXPIRES_KEY).delete()
        
        db.add(SystemSetting(
            key=CAPersistenceService.CA_CERT_KEY,
            value=ca_cert_pem.decode('utf-8'),
            encrypted=False,
            description="Certificate Authority public certificate"
        ))
        
        db.add(SystemSetting(
            key=CAPersistenceService.CA_KEY_KEY,
            value=base64.b64encode(encrypted_key).decode('utf-8'),
            encrypted=True,
            description="Certificate Authority private key (encrypted)"
        ))
        
        db.add(SystemSetting(
            key=CAPersistenceService.CA_CREATED_KEY,
            value=created_at.isoformat(),
            encrypted=False,
            description="CA creation timestamp"
        ))
        
        db.add(SystemSetting(
            key=CAPersistenceService.CA_EXPIRES_KEY,
            value=expires_at.isoformat(),
            encrypted=False,
            description="CA expiration timestamp"
        ))
        
        db.commit()
        
        return {
            "ca_certificate": ca_cert_pem.decode('utf-8'),
            "created_at": created_at,
            "expires_at": expires_at
        }
    
    @staticmethod
    def get_ca_credentials(db: Session) -> Optional[Tuple[bytes, bytes]]:
        """
        Get CA certificate and private key
        
        Returns:
            Tuple of (ca_cert_pem, ca_key_pem) or None if not found
        """
        cert_setting = db.query(SystemSetting).filter(
            SystemSetting.key == CAPersistenceService.CA_CERT_KEY
        ).first()
        
        key_setting = db.query(SystemSetting).filter(
            SystemSetting.key == CAPersistenceService.CA_KEY_KEY
        ).first()
        
        if not cert_setting or not key_setting:
            return None
        
        # Decrypt private key
        encrypted_key = base64.b64decode(key_setting.value)
        ca_key_pem = decrypt_sensitive_data(encrypted_key)
        ca_cert_pem = cert_setting.value.encode('utf-8')
        
        return ca_cert_pem, ca_key_pem
    
    @staticmethod
    def get_ca_info(db: Session) -> Optional[dict]:
        """
        Get CA information without private key
        
        Returns:
            dict with certificate, created_at, expires_at, expiring_soon, expired, days_until_expiry
        """
        cert_setting = db.query(SystemSetting).filter(
            SystemSetting.key == CAPersistenceService.CA_CERT_KEY
        ).first()
        
        created_setting = db.query(SystemSetting).filter(
            SystemSetting.key == CAPersistenceService.CA_CREATED_KEY
        ).first()
        
        expires_setting = db.query(SystemSetting).filter(
            SystemSetting.key == CAPersistenceService.CA_EXPIRES_KEY
        ).first()
        
        if not cert_setting or not created_setting or not expires_setting:
            return None
        
        ca_cert_pem = cert_setting.value.encode('utf-8')
        created_at = datetime.fromisoformat(created_setting.value)
        expires_at = datetime.fromisoformat(expires_setting.value)
        
        # Get additional info from certificate
        cert_info = CAService.get_ca_info(ca_cert_pem)
        
        return {
            "ca_certificate": cert_setting.value,
            "created_at": created_at,
            "expires_at": expires_at,
            "expiring_soon": cert_info['expiring_soon'],
            "expired": cert_info['expired'],
            "days_until_expiry": cert_info['days_until_expiry'],
            "subject": cert_info['subject'],
            "issuer": cert_info['issuer']
        }
    
    @staticmethod
    def ca_exists(db: Session) -> bool:
        """Check if CA exists in database"""
        cert_setting = db.query(SystemSetting).filter(
            SystemSetting.key == CAPersistenceService.CA_CERT_KEY
        ).first()
        return cert_setting is not None

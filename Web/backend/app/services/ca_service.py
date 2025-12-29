"""
Certificate Authority (CA) management service
"""
from cryptography import x509
from cryptography.x509.oid import NameOID, ExtensionOID
from cryptography.hazmat.primitives import hashes, serialization
from cryptography.hazmat.primitives.asymmetric import rsa
from cryptography.hazmat.backends import default_backend
from datetime import datetime, timedelta
from typing import Optional, Tuple
from sqlalchemy.orm import Session

from app.core.time import utcnow
from app.core.encryption import encrypt_sensitive_data, decrypt_sensitive_data


class CAService:
    """Certificate Authority management for desktop authentication"""
    
    CA_VALIDITY_DAYS = 365 * 5  # 5 years
    WARNING_DAYS_BEFORE_EXPIRY = 60  # 2 months warning
    
    @staticmethod
    def generate_ca(
        organization: str = "AegisMint",
        country: str = "US",
        state: str = "State",
        locality: str = "City"
    ) -> Tuple[bytes, bytes, datetime, datetime]:
        """
        Generate a new Certificate Authority.
        
        Returns:
            Tuple of (ca_cert_pem, ca_key_pem, created_at, expires_at)
        """
        # Generate RSA private key for CA (4096-bit for strong security)
        private_key = rsa.generate_private_key(
            public_exponent=65537,
            key_size=4096,
            backend=default_backend()
        )
        
        # Create CA subject and issuer (same for self-signed)
        subject = issuer = x509.Name([
            x509.NameAttribute(NameOID.COUNTRY_NAME, country),
            x509.NameAttribute(NameOID.STATE_OR_PROVINCE_NAME, state),
            x509.NameAttribute(NameOID.LOCALITY_NAME, locality),
            x509.NameAttribute(NameOID.ORGANIZATION_NAME, organization),
            x509.NameAttribute(NameOID.COMMON_NAME, f"{organization} Root CA"),
        ])
        
        created_at = utcnow()
        expires_at = created_at + timedelta(days=CAService.CA_VALIDITY_DAYS)
        
        # Build CA certificate
        cert = (
            x509.CertificateBuilder()
            .subject_name(subject)
            .issuer_name(issuer)
            .public_key(private_key.public_key())
            .serial_number(x509.random_serial_number())
            .not_valid_before(created_at)
            .not_valid_after(expires_at)
            .add_extension(
                x509.BasicConstraints(ca=True, path_length=0),
                critical=True,
            )
            .add_extension(
                x509.KeyUsage(
                    digital_signature=True,
                    key_cert_sign=True,
                    crl_sign=True,
                    key_encipherment=False,
                    content_commitment=False,
                    data_encipherment=False,
                    key_agreement=False,
                    encipher_only=False,
                    decipher_only=False,
                ),
                critical=True,
            )
            .sign(private_key, hashes.SHA256(), backend=default_backend())
        )
        
        # Serialize to PEM format
        cert_pem = cert.public_bytes(serialization.Encoding.PEM)
        key_pem = private_key.private_bytes(
            encoding=serialization.Encoding.PEM,
            format=serialization.PrivateFormat.PKCS8,
            encryption_algorithm=serialization.NoEncryption()
        )
        
        return cert_pem, key_pem, created_at, expires_at
    
    @staticmethod
    def sign_certificate(
        csr_pem: bytes,
        ca_cert_pem: bytes,
        ca_key_pem: bytes,
        desktop_app_id: str,
        validity_days: Optional[int] = None
    ) -> bytes:
        """
        Sign a Certificate Signing Request (CSR) to create a desktop certificate.
        
        Args:
            csr_pem: Certificate Signing Request in PEM format
            ca_cert_pem: CA certificate in PEM format
            ca_key_pem: CA private key in PEM format
            desktop_app_id: Desktop application ID
            validity_days: Certificate validity (defaults to match CA expiration)
            
        Returns:
            Signed certificate in PEM format
        """
        # Load CA certificate and key
        ca_cert = x509.load_pem_x509_certificate(ca_cert_pem, default_backend())
        ca_key = serialization.load_pem_private_key(ca_key_pem, password=None, backend=default_backend())
        
        # Load CSR
        csr = x509.load_pem_x509_csr(csr_pem, default_backend())
        
        # Determine validity period
        if validity_days is None:
            # Match CA expiration
            # Handle both old and new cryptography versions
            if hasattr(ca_cert, 'not_valid_after_utc'):
                expires_at = ca_cert.not_valid_after_utc
            else:
                from datetime import timezone
                expires_at = ca_cert.not_valid_after.replace(tzinfo=timezone.utc)
            created_at = utcnow()
        else:
            created_at = utcnow()
            expires_at = created_at + timedelta(days=validity_days)
        
        # Build certificate
        cert = (
            x509.CertificateBuilder()
            .subject_name(csr.subject)
            .issuer_name(ca_cert.subject)
            .public_key(csr.public_key())
            .serial_number(x509.random_serial_number())
            .not_valid_before(created_at)
            .not_valid_after(expires_at)
            .add_extension(
                x509.BasicConstraints(ca=False, path_length=None),
                critical=True,
            )
            .add_extension(
                x509.KeyUsage(
                    digital_signature=True,
                    key_encipherment=True,
                    key_cert_sign=False,
                    crl_sign=False,
                    content_commitment=False,
                    data_encipherment=False,
                    key_agreement=False,
                    encipher_only=False,
                    decipher_only=False,
                ),
                critical=True,
            )
            .add_extension(
                x509.ExtendedKeyUsage([
                    ExtensionOID.CLIENT_AUTH,
                ]),
                critical=True,
            )
            .add_extension(
                x509.SubjectAlternativeName([
                    x509.DNSName(f"desktop-{desktop_app_id}.aegismint.local"),
                ]),
                critical=False,
            )
            .sign(ca_key, hashes.SHA256(), backend=default_backend())
        )
        
        # Serialize to PEM
        cert_pem = cert.public_bytes(serialization.Encoding.PEM)
        return cert_pem
    
    @staticmethod
    def is_ca_expiring_soon(expires_at: datetime) -> bool:
        """Check if CA is expiring within warning period (2 months)"""
        warning_date = expires_at - timedelta(days=CAService.WARNING_DAYS_BEFORE_EXPIRY)
        return utcnow() >= warning_date
    
    @staticmethod
    def get_ca_info(ca_cert_pem: bytes) -> dict:
        """Extract information from CA certificate"""
        cert = x509.load_pem_x509_certificate(ca_cert_pem, default_backend())
        
        # Handle both old and new cryptography versions
        # Newer versions (42.0.0+) have not_valid_before_utc/not_valid_after_utc
        # Older versions only have not_valid_before/not_valid_after (naive datetime)
        if hasattr(cert, 'not_valid_before_utc'):
            not_valid_before = cert.not_valid_before_utc
            not_valid_after = cert.not_valid_after_utc
        else:
            # Older version - convert naive to UTC-aware
            from datetime import timezone
            not_valid_before = cert.not_valid_before.replace(tzinfo=timezone.utc)
            not_valid_after = cert.not_valid_after.replace(tzinfo=timezone.utc)
        
        now_utc = utcnow()
        
        return {
            "subject": cert.subject.rfc4514_string(),
            "issuer": cert.issuer.rfc4514_string(),
            "serial_number": str(cert.serial_number),
            "not_valid_before": not_valid_before,
            "not_valid_after": not_valid_after,
            "is_expired": now_utc > not_valid_after,
            "is_expiring_soon": CAService.is_ca_expiring_soon(not_valid_after),
            "days_until_expiry": (not_valid_after - now_utc).days,
        }

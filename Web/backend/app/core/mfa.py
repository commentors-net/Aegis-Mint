import pyotp

from .config import get_settings


def totp_from_secret(secret: str) -> pyotp.TOTP:
    settings = get_settings()
    return pyotp.TOTP(secret, issuer=settings.totp_issuer)


def verify_otp(secret: str, otp: str) -> bool:
    totp = totp_from_secret(secret)
    # allow small window for clock skew
    return totp.verify(otp, valid_window=1)

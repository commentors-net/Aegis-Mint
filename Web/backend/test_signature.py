"""
Check the signature mismatch issue for AegisMint.Mint
"""
import base64
import hmac
import hashlib
from app.db.session import SessionLocal
from app.models import Desktop

def test_signature():
    db = SessionLocal()
    
    # Get the Mint desktop
    desktop = db.query(Desktop).filter(
        Desktop.desktop_app_id == '3099b0c6-be8a-4ab8-b44e-ef28329946bb',
        Desktop.app_type == 'Mint'
    ).first()
    
    if not desktop:
        print("Desktop not found!")
        db.close()
        return
    
    print(f"Desktop found:")
    print(f"  ID: {desktop.id}")
    print(f"  desktop_app_id: {desktop.desktop_app_id}")
    print(f"  app_type: {desktop.app_type}")
    print(f"  Status: {desktop.status}")
    print(f"  Secret Key (first 30 chars): {desktop.secret_key[:30] if desktop.secret_key else 'None'}...")
    print()
    
    # Test signature generation
    desktop_id = desktop.desktop_app_id
    timestamp = "1737159408"  # example timestamp
    body = ""  # GET request, no body
    
    # Create the message to sign (same as backend)
    message = f"{desktop_id}:{timestamp}:{body}"
    print(f"Message to sign: {message}")
    
    # Compute signature
    if desktop.secret_key:
        key_bytes = base64.b64decode(desktop.secret_key)
        signature = base64.b64encode(
            hmac.new(key_bytes, message.encode('utf-8'), hashlib.sha256).digest()
        ).decode('utf-8')
        print(f"Expected signature: {signature}")
        print()
        print("If the client's signature doesn't match this, the secret keys are different.")
    
    db.close()

if __name__ == "__main__":
    test_signature()

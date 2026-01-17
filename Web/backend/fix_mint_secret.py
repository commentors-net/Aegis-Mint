"""
Fix the secret key mismatch by resetting the Mint desktop registration.
This will force Mint to re-register and get a new secret key.
"""
from app.db.session import SessionLocal
from app.models import Desktop, ApprovalSession, GovernanceAssignment

def reset_mint_desktop():
    db = SessionLocal()
    
    try:
        # Find the Mint desktop
        desktop = db.query(Desktop).filter(
            Desktop.desktop_app_id == '3099b0c6-be8a-4ab8-b44e-ef28329946bb',
            Desktop.app_type == 'Mint'
        ).first()
        
        if not desktop:
            print("Mint desktop not found!")
            db.close()
            return
        
        print(f"Found Mint desktop:")
        print(f"  ID: {desktop.id}")
        print(f"  Status: {desktop.status}")
        print(f"  Secret Key: {desktop.secret_key[:30] if desktop.secret_key else 'None'}...")
        print()
        
        # Option 1: Just clear the secret key, forcing re-registration
        print("Clearing secret key to force re-registration...")
        desktop.secret_key = None
        desktop.status = "Pending"
        
        # Delete any existing assignments and sessions
        db.query(GovernanceAssignment).filter(GovernanceAssignment.desktop_id == desktop.id).delete()
        db.query(ApprovalSession).filter(ApprovalSession.desktop_id == desktop.id).delete()
        
        db.commit()
        print("✓ Secret key cleared. Mint will re-register on next start.")
        print("✓ Assignments and sessions deleted.")
        print()
        print("Next steps:")
        print("1. Start AegisMint.Mint")
        print("2. It will register with a new secret key")
        print("3. Admin should approve it again")
        print("4. Assign to governance authorities")
        
    finally:
        db.close()

if __name__ == "__main__":
    reset_mint_desktop()

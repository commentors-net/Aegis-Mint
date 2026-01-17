"""
Test script to verify desktop assignment fix.
This script checks that assigning a desktop with type "Mint" doesn't also assign "TokenControl".
"""

from sqlalchemy.orm import Session
from app.db.session import SessionLocal
from app.models import Desktop, GovernanceAssignment, User

def test_assignments():
    db: Session = SessionLocal()
    try:
        # Find all desktops
        desktops = db.query(Desktop).all()
        print(f"\n=== All Desktops ===")
        for desktop in desktops:
            print(f"ID: {desktop.id}")
            print(f"  desktop_app_id: {desktop.desktop_app_id}")
            print(f"  app_type: {desktop.app_type}")
            print(f"  name_label: {desktop.name_label}")
            print()
        
        # Find all assignments
        assignments = db.query(GovernanceAssignment).all()
        print(f"\n=== All Assignments ===")
        for assignment in assignments:
            desktop = db.query(Desktop).filter(Desktop.id == assignment.desktop_id).first()
            user = db.query(User).filter(User.id == assignment.user_id).first()
            if desktop and user:
                print(f"User: {user.email}")
                print(f"  -> Desktop ID: {desktop.id}")
                print(f"  -> desktop_app_id: {desktop.desktop_app_id}")
                print(f"  -> app_type: {desktop.app_type}")
                print()
        
        # Check for duplicate assignments (same user assigned to both Mint and TokenControl with same desktop_app_id)
        print(f"\n=== Checking for Duplicate Assignments ===")
        users = db.query(User).all()
        for user in users:
            user_assignments = db.query(GovernanceAssignment).filter(
                GovernanceAssignment.user_id == user.id
            ).all()
            
            desktop_app_ids = {}
            for assignment in user_assignments:
                desktop = db.query(Desktop).filter(Desktop.id == assignment.desktop_id).first()
                if desktop:
                    key = desktop.desktop_app_id
                    if key not in desktop_app_ids:
                        desktop_app_ids[key] = []
                    desktop_app_ids[key].append(desktop.app_type)
            
            for desktop_app_id, app_types in desktop_app_ids.items():
                if len(app_types) > 1:
                    print(f"⚠️  User {user.email} is assigned to BOTH app types for desktop_app_id {desktop_app_id}: {app_types}")
                else:
                    print(f"✓  User {user.email} is assigned to only {app_types[0]} for desktop_app_id {desktop_app_id}")
        
    finally:
        db.close()

if __name__ == "__main__":
    test_assignments()

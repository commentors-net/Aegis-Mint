from app.db.session import SessionLocal
from app.models import GovernanceAssignment, Desktop
from sqlalchemy import and_

db = SessionLocal()

# Find assignments with NULL desktop_id
assignments = db.query(GovernanceAssignment).filter(GovernanceAssignment.desktop_id == None).all()
print(f'Found {len(assignments)} assignments with NULL desktop_id')

# Delete them - they will be recreated properly
if assignments:
    for assignment in assignments:
        db.delete(assignment)
    db.commit()
    print(f'Deleted {len(assignments)} invalid assignments')
    print('Please reassign desktops to governance users from the admin interface')
else:
    print('No invalid assignments found')

db.close()

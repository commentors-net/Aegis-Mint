from app.db.session import SessionLocal
from app.models import Desktop

db = SessionLocal()
d = db.query(Desktop).filter(
    Desktop.desktop_app_id == '3099b0c6-be8a-4ab8-b44e-ef28329946bb',
    Desktop.app_type == 'Mint'
).first()

print(f'Found: {d is not None}')
if d:
    print(f'Status: {d.status}')
    print(f'Secret Key: {d.secret_key[:20] if d.secret_key else None}...')
    print(f'ID: {d.id}')
    
db.close()

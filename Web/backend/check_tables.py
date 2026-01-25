from app.db.session import SessionLocal
from sqlalchemy import text

db = SessionLocal()
result = db.execute(text('SHOW TABLES LIKE "token_user%"'))
for row in result:
    print(row[0])
db.close()

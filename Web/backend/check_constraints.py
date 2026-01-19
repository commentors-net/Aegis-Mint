from app.db.session import engine
from sqlalchemy import text

query = """
SELECT 
    TABLE_NAME, 
    CONSTRAINT_NAME, 
    COLUMN_NAME,
    REFERENCED_TABLE_NAME,
    REFERENCED_COLUMN_NAME
FROM information_schema.KEY_COLUMN_USAGE 
WHERE TABLE_SCHEMA='aegismint_db' 
AND REFERENCED_TABLE_NAME IS NOT NULL
"""

with engine.connect() as conn:
    result = conn.execute(text(query))
    print("All Foreign Key relationships in database:")
    for row in result:
        print(f"Table: {row[0]:30} FK: {row[1]:45} Column: {row[2]:25} -> {row[3]}.{row[4]}")

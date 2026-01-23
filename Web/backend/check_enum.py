#!/usr/bin/env python3
"""Check the actual enum values in the database."""
from sqlalchemy import create_engine, text
from app.core.config import get_settings

settings = get_settings()
engine = create_engine(settings.database_url)

with engine.connect() as conn:
    result = conn.execute(text("SHOW COLUMNS FROM share_operation_logs LIKE 'operation_type'")).fetchone()
    print('Column definition:')
    print(f'  Field: {result[0]}')
    print(f'  Type: {result[1]}')
    print(f'  Null: {result[2]}')
    print(f'  Key: {result[3]}')
    print(f'  Default: {result[4]}')
    print(f'  Extra: {result[5]}')

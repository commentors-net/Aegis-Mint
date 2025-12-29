import pymysql

# Connect to database
connection = pymysql.connect(
    host='127.0.0.1',
    user='apkserve_governer',
    password='aegismint',
    database='apkserve_governance'
)

try:
    with connection.cursor() as cursor:
        # Add certificate columns to desktops table
        print("Adding certificate_pem column...")
        cursor.execute("ALTER TABLE desktops ADD COLUMN certificate_pem TEXT")
        
        print("Adding certificate_issued_at column...")
        cursor.execute("ALTER TABLE desktops ADD COLUMN certificate_issued_at DATETIME")
        
        print("Adding certificate_expires_at column...")
        cursor.execute("ALTER TABLE desktops ADD COLUMN certificate_expires_at DATETIME")
        
        print("Adding csr_submitted column...")
        cursor.execute("ALTER TABLE desktops ADD COLUMN csr_submitted VARCHAR(1) DEFAULT '0'")
        
        print("Adding csr_pem column...")
        cursor.execute("ALTER TABLE desktops ADD COLUMN csr_pem TEXT")
        
    connection.commit()
    print("\n✓ All columns added successfully!")
    
except pymysql.err.OperationalError as e:
    if '1060' in str(e):  # Duplicate column name
        print(f"Column already exists: {e}")
    else:
        print(f"Error: {e}")
        raise
finally:
    connection.close()

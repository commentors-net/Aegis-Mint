"""
Add missing 'encrypted' column to system_settings table
"""
import pymysql
from urllib.parse import urlparse

# Parse connection string from .env
db_url = "mysql+pymysql://apkserve_governer:aegismint@127.0.0.1:3306/apkserve_governance"
parsed = urlparse(db_url.replace('mysql+pymysql://', 'mysql://'))

# Extract connection details
user = parsed.username
password = parsed.password
host = parsed.hostname
database = parsed.path.lstrip('/')

print(f"Connecting to {host} as {user}...")

try:
    conn = pymysql.connect(
        host=host,
        user=user,
        password=password,
        database=database
    )
    
    cursor = conn.cursor()
    
    # Check and add encrypted column
    cursor.execute("SHOW COLUMNS FROM system_settings LIKE 'encrypted'")
    if cursor.fetchone():
        print("✓ Column 'encrypted' already exists")
    else:
        print("Adding 'encrypted' column...")
        cursor.execute("""
            ALTER TABLE system_settings 
            ADD COLUMN encrypted TINYINT(1) NOT NULL DEFAULT 0 
            AFTER value
        """)
        conn.commit()
        print("✓ Column 'encrypted' added!")
    
    # Check and add created_at column
    cursor.execute("SHOW COLUMNS FROM system_settings LIKE 'created_at'")
    if cursor.fetchone():
        print("✓ Column 'created_at' already exists")
    else:
        print("Adding 'created_at' column...")
        cursor.execute("""
            ALTER TABLE system_settings 
            ADD COLUMN created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP 
            AFTER encrypted
        """)
        conn.commit()
        print("✓ Column 'created_at' added!")
    
    # Check and add updated_at column
    cursor.execute("SHOW COLUMNS FROM system_settings LIKE 'updated_at'")
    if cursor.fetchone():
        print("✓ Column 'updated_at' already exists")
    else:
        print("Adding 'updated_at' column...")
        cursor.execute("""
            ALTER TABLE system_settings 
            ADD COLUMN updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP 
            AFTER created_at
        """)
        conn.commit()
        print("✓ Column 'updated_at' added!")
    
    # Check and add description column
    cursor.execute("SHOW COLUMNS FROM system_settings LIKE 'description'")
    if cursor.fetchone():
        print("✓ Column 'description' already exists")
    else:
        print("Adding 'description' column...")
        cursor.execute("""
            ALTER TABLE system_settings 
            ADD COLUMN description VARCHAR(512) NULL 
            AFTER updated_at
        """)
        conn.commit()
        print("✓ Column 'description' added!")
    
    # Check if value column needs to be changed from VARCHAR to TEXT
    cursor.execute("SHOW COLUMNS FROM system_settings WHERE Field = 'value'")
    col_info = cursor.fetchone()
    if col_info and 'text' not in col_info[1].lower():
        print("Converting 'value' column from VARCHAR to TEXT...")
        cursor.execute("""
            ALTER TABLE system_settings 
            MODIFY COLUMN value TEXT NULL
        """)
        conn.commit()
        print("✓ Column 'value' converted to TEXT!")
    else:
        print("✓ Column 'value' is already TEXT")
    
    # Check if key column length is correct
    cursor.execute("SHOW COLUMNS FROM system_settings WHERE Field = 'key'")
    col_info = cursor.fetchone()
    if col_info and 'varchar(128)' in col_info[1].lower():
        print("Updating 'key' column length to 255...")
        cursor.execute("""
            ALTER TABLE system_settings 
            MODIFY COLUMN `key` VARCHAR(255) NOT NULL
        """)
        conn.commit()
        print("✓ Column 'key' updated to VARCHAR(255)!")
    else:
        print("✓ Column 'key' length is correct")
    
    # Verify the table structure
    cursor.execute("DESCRIBE system_settings")
    print("\nCurrent system_settings structure:")
    for row in cursor.fetchall():
        print(f"  {row}")
    
    cursor.close()
    conn.close()
    print("\n✅ Database update completed successfully!")
    
except pymysql.Error as e:
    print(f"❌ Database error: {e}")
    exit(1)

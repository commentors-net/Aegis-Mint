# Quick Start Guide - Phase 5: Token User Portal

## Overview

Phase 5 creates a **separate web application** (`ClientWeb`) for token share users to login and download their assigned shares. This is completely separate from the admin portal.

### Architecture

```
Admin User → Web (Admin Portal) → Database
Token User → ClientWeb (User Portal) → ClientWeb Backend (Proxy) → Web Backend → Database
```

## Setup Instructions

### Step 1: Verify Web Backend is Running

The main backend should already be running on port 8000.

```powershell
cd D:\Jobs\workspace\DiG\Aegis-Mint\Web\backend

# Check if Python debug console terminal shows it's running
# If not, start it:
python -m venv venv
.\venv\Scripts\activate
pip install -r requirements.txt
uvicorn main:app --host 127.0.0.1 --port 8000 --reload
```

### Step 2: Start ClientWeb Backend (Middleware)

```powershell
cd D:\Jobs\workspace\DiG\Aegis-Mint\ClientWeb\backend

# Create virtual environment
python -m venv venv
.\venv\Scripts\activate

# Install dependencies
pip install -r requirements.txt

# Copy environment config
copy .env.example .env
# (No need to edit .env for local development)

# Start server
python main.py
```

You should see:
```
INFO:     Started server process
INFO:     Uvicorn running on http://127.0.0.1:8001
```

### Step 3: Start ClientWeb Frontend

```powershell
cd D:\Jobs\workspace\DiG\Aegis-Mint\ClientWeb\frontend

# Install dependencies (first time only)
npm install

# Start development server
npm run dev
```

You should see:
```
  VITE v5.x.x  ready in xxx ms

  ➜  Local:   http://127.0.0.1:5174/
```

### Step 4: Create a Test Token User

1. Open Web admin UI: http://127.0.0.1:5173
2. Login as Super Admin
3. Navigate to **Share Management**
4. Select a token that has shares uploaded
5. Click **"Manage Users"** accordion
6. Click **"Add User"**
7. Fill in:
   - Name: Test User
   - Email: test@example.com
   - Password: TestPassword123
   - Phone: (optional)
8. Click **Save**

### Step 5: Assign a Share

1. Click **"Manage Shares"** for the token
2. Find an unassigned share
3. Click **"Assign"**
4. Select the test user from dropdown
5. Add notes (optional)
6. Click **"Assign Share"**

### Step 6: Test Token User Login

1. Open ClientWeb: http://127.0.0.1:5174
2. You should see login page
3. Login with:
   - Email: test@example.com
   - Password: TestPassword123
4. Click **Login**

### Step 7: MFA Setup (First Time)

1. You'll see MFA page with QR code
2. Scan QR code with authenticator app (Google Authenticator, Authy, etc.)
3. Enter the 6-digit code from app
4. Click **Verify**

### Step 8: View and Download Share

1. You'll be redirected to Dashboard
2. You should see your assigned share(s)
3. Click **"Download Share"**
4. Confirm the download
5. File will download as `aegis-share-XXX.json`
6. Share status will update to "Downloaded"
7. Download button will show "Already Downloaded"

## Application URLs

| Application | URL | Purpose |
|------------|-----|---------|
| Web Backend | http://127.0.0.1:8000 | Main API (hidden from token users) |
| Web Frontend | http://127.0.0.1:5173 | Admin portal |
| ClientWeb Backend | http://127.0.0.1:8001 | Middleware/proxy |
| ClientWeb Frontend | http://127.0.0.1:5174 | Token user portal |

## Testing Checklist

- [ ] Web backend running on port 8000
- [ ] ClientWeb backend running on port 8001
- [ ] ClientWeb frontend running on port 5174
- [ ] Token user created with password
- [ ] Share assigned to token user
- [ ] Token user can login with email/password
- [ ] QR code displays on first login
- [ ] MFA verification works with authenticator app
- [ ] Dashboard shows assigned share
- [ ] Download button works
- [ ] File downloads with correct name
- [ ] Share status updates to "Downloaded"
- [ ] Download button becomes disabled
- [ ] Logout works
- [ ] Login again shows same share as "Already Downloaded"

## Troubleshooting

**Can't connect to backend**:
- Check Web backend is running on port 8000
- Check ClientWeb backend is running on port 8001
- Look at ClientWeb backend console for errors

**Login fails**:
- Verify token user has `password_hash` set (created via admin UI)
- Check `is_active` is true in database
- Look at ClientWeb backend logs

**MFA QR code not showing**:
- This is normal if MFA already set up
- Clear `mfa_secret` in database to reset

**No shares showing**:
- Verify share is assigned in admin UI
- Check share_assignments table in database
- Look at ClientWeb backend logs

**Download fails**:
- Check `download_allowed` is true
- Admin needs to re-enable if already downloaded
- Look at Web backend logs (port 8000)

## Next Steps

1. Test the complete flow with different users
2. Test re-enabling downloads after they're disabled
3. Test with multiple shares assigned to same user
4. Configure for production deployment (separate servers, SSL, etc.)

## Notes

- ClientWeb can be deployed on a completely different server
- Token users never see the main backend URL (127.0.0.1:8000)
- All requests go through ClientWeb backend middleware
- This provides security isolation and allows separate scaling
- Perfect for giving external users access without exposing admin backend

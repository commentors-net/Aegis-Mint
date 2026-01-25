# ClientWeb - Token Share User Portal

Complete web application for token share users to access and download their assigned shares.

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                      Token Share User                        │
│                         (Browser)                            │
└────────────────────────────┬────────────────────────────────┘
                             │
                             ↓
┌─────────────────────────────────────────────────────────────┐
│              ClientWeb Frontend (React + Vite)               │
│                   Port: 5174                                 │
│  - Login UI                                                  │
│  - MFA Setup/Verification                                    │
│  - Dashboard (My Shares)                                     │
│  - Download Interface                                        │
└────────────────────────────┬────────────────────────────────┘
                             │
                             ↓
┌─────────────────────────────────────────────────────────────┐
│           ClientWeb Backend (FastAPI Middleware)             │
│                   Port: 8001                                 │
│  - Authentication Proxy                                      │
│  - Share Download Proxy                                      │
│  - Session Management                                        │
│  - Security Layer                                            │
└────────────────────────────┬────────────────────────────────┘
                             │
                             ↓
┌─────────────────────────────────────────────────────────────┐
│              Main Backend (Aegis Mint API)                   │
│                   Port: 8000                                 │
│  - Token User Authentication                                 │
│  - Share Assignment Management                               │
│  - Download Tracking                                         │
│  - Database Operations                                       │
└────────────────────────────┬────────────────────────────────┘
                             │
                             ↓
┌─────────────────────────────────────────────────────────────┐
│                      MySQL Database                          │
│  - token_share_users                                         │
│  - share_files                                               │
│  - share_assignments                                         │
│  - share_download_log                                        │
└─────────────────────────────────────────────────────────────┘
```

## Components

### Backend (`/backend`)
- **Framework**: FastAPI
- **Purpose**: Middleware/proxy to main Aegis Mint backend
- **Port**: 8001
- **Features**:
  - Forwards authentication requests
  - Proxies share download requests
  - Hides main backend URL from users
  - Can add rate limiting, caching, etc.

### Frontend (`/frontend`)
- **Framework**: React 18 + TypeScript + Vite
- **Port**: 5174
- **Pages**:
  - Login page (email/password)
  - MFA page (TOTP with QR code)
  - Dashboard (view/download shares)

## Quick Start

### 1. Start Main Backend (If not running)
```bash
cd D:\Jobs\workspace\DiG\Aegis-Mint\Web\backend
python -m venv venv
venv\Scripts\activate
pip install -r requirements.txt
uvicorn main:app --host 127.0.0.1 --port 8000 --reload
```

### 2. Start ClientWeb Backend
```bash
cd D:\Jobs\workspace\DiG\Aegis-Mint\ClientWeb\backend
python -m venv venv
venv\Scripts\activate
pip install -r requirements.txt
copy .env.example .env
# Edit .env if needed
python main.py
```

### 3. Start ClientWeb Frontend
```bash
cd D:\Jobs\workspace\DiG\Aegis-Mint\ClientWeb\frontend
npm install
npm run dev
```

### 4. Access Application
Open http://127.0.0.1:5174 in your browser.

## User Flow

1. **Login**: Token user enters email and password
2. **MFA Setup** (first time): Scan QR code with authenticator app
3. **MFA Verification**: Enter 6-digit OTP code
4. **Dashboard**: View assigned shares
5. **Download**: Click download button (one-time download by default)
6. **Re-download**: Contact admin to re-enable if needed

## API Endpoints

### ClientWeb Backend (Port 8001)

**Authentication**:
- `POST /api/auth/login` - Login with email/password
- `POST /api/auth/verify-otp` - Verify MFA code
- `POST /api/auth/refresh` - Refresh access token

**Shares**:
- `GET /api/shares/my-shares` - List user's shares
- `GET /api/shares/download/{assignment_id}` - Download share
- `GET /api/shares/history` - Download history

## Security Features

- Token users never see main backend URL
- All requests go through middleware layer
- JWT token authentication
- MFA (TOTP) required for all users
- One-time download by default
- Complete audit trail in database
- Can be deployed on separate server

## Deployment

### Production Checklist
- [ ] Change `DEBUG=false` in backend .env
- [ ] Update `BACKEND_API_URL` to production URL
- [ ] Update `JWT_SECRET_KEY` (must match main backend)
- [ ] Update `CORS_ORIGINS` for production frontend URL
- [ ] Build frontend: `npm run build`
- [ ] Serve frontend with nginx/Apache
- [ ] Run backend with gunicorn/uvicorn in production mode
- [ ] Set up SSL/TLS certificates
- [ ] Configure firewall rules

## Development vs Production

**Development** (Current):
- Main Backend: http://127.0.0.1:8000
- ClientWeb Backend: http://127.0.0.1:8001
- ClientWeb Frontend: http://127.0.0.1:5174

**Production** (Example):
- Main Backend: https://aegis-admin.example.com (internal only)
- ClientWeb Backend: https://shares-api.example.com
- ClientWeb Frontend: https://shares.example.com

## Troubleshooting

**Backend connection errors**:
- Ensure main backend is running on port 8000
- Check `BACKEND_API_URL` in ClientWeb backend .env

**CORS errors**:
- Verify `CORS_ORIGINS` includes frontend URL
- Check browser console for specific errors

**Login failures**:
- Verify token_share_users have password_hash set
- Check is_active flag is true
- Ensure MFA is set up (or scan QR code)

**Download not working**:
- Check `download_allowed` flag in share_assignments
- Admin needs to re-enable if already downloaded

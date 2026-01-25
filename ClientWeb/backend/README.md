# ClientWeb Backend

Middleware/proxy layer for token share user portal.

## Purpose

This backend acts as a secure proxy between token share users and the main Aegis Mint backend. It provides:

- **Authentication Forwarding**: Proxies login/MFA requests to main backend
- **Session Management**: Manages token share user sessions
- **Security Layer**: Hides main backend URL from end users
- **Rate Limiting**: (Future) Prevent abuse
- **Request Logging**: Audit trail for user actions

## Architecture

```
Token User (Browser)
    ↓
ClientWeb Frontend (React)
    ↓
ClientWeb Backend (This FastAPI App) ← You are here
    ↓
Main Backend (Aegis Mint API)
    ↓
Database (MySQL)
```

## Setup

1. **Create virtual environment**:
   ```bash
   python -m venv venv
   venv\Scripts\activate  # Windows
   ```

2. **Install dependencies**:
   ```bash
   pip install -r requirements.txt
   ```

3. **Configure environment**:
   ```bash
   copy .env.example .env
   # Edit .env with your settings
   ```

4. **Run server**:
   ```bash
   python main.py
   # Or with uvicorn directly:
   uvicorn main:app --host 127.0.0.1 --port 8001 --reload
   ```

## API Endpoints

### Authentication
- `POST /api/auth/login` - Token user login
- `POST /api/auth/verify-otp` - Verify MFA code
- `POST /api/auth/refresh` - Refresh access token

### Shares
- `GET /api/shares/my-shares` - List assigned shares
- `GET /api/shares/download/{assignment_id}` - Download share file
- `GET /api/shares/history` - Download history

## Configuration

Key settings in `.env`:

- `BACKEND_API_URL`: Main Aegis Mint backend URL (default: http://127.0.0.1:8000)
- `PORT`: This server's port (default: 8001)
- `CORS_ORIGINS`: Allowed frontend origins
- `DEBUG`: Enable debug mode (default: true)

## Security

- All requests are proxied to main backend
- JWT tokens validated by main backend
- No direct database access
- Token users never see main backend URL
- Can be deployed on separate server for additional isolation

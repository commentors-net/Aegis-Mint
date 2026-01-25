# ClientWeb Frontend

React + TypeScript frontend for Token Share User portal.

## Setup

1. **Install dependencies**:
   ```bash
   npm install
   ```

2. **Run development server**:
   ```bash
   npm run dev
   ```
   Opens at http://127.0.0.1:5174

3. **Build for production**:
   ```bash
   npm run build
   ```

## Features

- **Login**: Email + password authentication
- **MFA**: TOTP-based two-factor authentication with QR code setup
- **Dashboard**: View assigned shares
- **Download**: One-click share download with automatic disabling

## Architecture

```
User → Frontend (React) → Backend Middleware (FastAPI) → Main Backend → Database
```

The frontend communicates only with the ClientWeb backend (port 8001), which proxies requests to the main Aegis Mint backend (port 8000).

## Development

- Frontend: http://127.0.0.1:5174
- Backend: http://127.0.0.1:8001
- Vite proxy: `/api` → `http://127.0.0.1:8001/api`

## Pages

- `/login` - Email/password login
- `/mfa` - MFA verification with QR code setup
- `/dashboard` - View and download shares

# AegisMint Governance Unlock (Web)

FastAPI backend + React/TypeScript frontend for governance unlocks (15-minute window) with JWT and TOTP (Google Authenticator).

## Backend
Location: `Web/backend`

### Setup
```bash
cd Web/backend
python -m venv .venv
. .venv/Scripts/activate   # or source .venv/bin/activate
pip install -r requirements.txt
# optional: copy .env.example to .env and edit secrets
# cp .env.example .env
```

### Environment
Set secrets before running:
```
TOKENCONTROL_JWT_SECRET=please-change-me
TOKENCONTROL_JWT_ISSUER=aegismint-gov
TOKENCONTROL_USER=operator
TOKENCONTROL_PASSWORD=StrongPassword!
TOKENCONTROL_TOTP_SECRET=BASE32_TOTP_SECRET
TOKENCONTROL_UNLOCK_MINUTES=15
```

### Google Authenticator / TOTP setup
Use the TOTP secret from your `.env` to enroll a 2FA device.
- Secret: `BASE32SECRET3232` (replace with your own in production)
- Issuer: `aegismint-gov`
- Label: `AegisMint:operator`
- OTP type: Time-based (TOTP)
- otpauth URI (for QR generation):
  ```
  otpauth://totp/AegisMint:operator?secret=BASE32SECRET3232&issuer=aegismint-gov
  ```
Add this to Google Authenticator (or similar), then use the 6-digit codes with `/auth/login`.

### Run
```bash
uvicorn main:app --host 0.0.0.0 --port 8000
```

Endpoints:
- `POST /auth/login` — body `{ username, password, totp }`, returns JWT (Bearer) valid for unlock scope.
- `POST /unlock` — body `{ reason }`, requires `Authorization: Bearer <token>`, returns unlock window metadata. (TODO: wire to actual box unlock.)

Security: JSON responses are `Cache-Control: no-store`; JWT is scope `unlock`; TOTP checked with small time skew window.

## Frontend
Location: `Web/frontend`

### Setup
```bash
cd Web/frontend
npm install
npm run dev   # served at http://localhost:5173, proxies /api -> http://localhost:8000
```

Behavior:
- In-memory JWT (never persisted); auto-expiry countdown.
- Auth form (user/pass/TOTP), unlock request with reason/ticket.
- Uses `/api/auth/login` and `/api/unlock` with Bearer token.

### Build
```bash
npm run build
npm run preview
```

## Integration notes
- Keep backend behind TLS/VPN and rate-limit `/auth/login`.
- Replace the TODO in `/unlock` with the real box unlock RPC/controller.
- Rotate `TOKENCONTROL_JWT_SECRET` and `TOKENCONTROL_TOTP_SECRET`; avoid defaults in production.

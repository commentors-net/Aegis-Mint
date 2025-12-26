# AegisMint Governance Web

FastAPI backend + React/TypeScript frontend for TokenControl desktop unlock approvals (per DesktopAppId, per-session N approvals, unlock window from Nth approval).

## Backend (`Web/backend`)
- FastAPI app lives in `app/` (`app/main.py`) with routers for auth, desktop registration/unlock-status, governance approvals, and super-admin CRUD.
- Models & schemas cover Users, Desktops, ApprovalSessions, Approvals, GovernanceAssignments, AuditLog.
- Auth flow: `POST /auth/login` (email+password) -> challenge_id, then `POST /auth/verify-otp` (challenge_id+otp) -> JWT (access+refresh) with role claim.
- Governance: `POST /api/governance/desktops/{desktopAppId}/approve` enforces one approval per user per session; unlocks at Nth approval.
- Admin: manage users and desktops, assign authorities, fetch audit logs.

### Run
```bash
cd Web/backend
python -m venv .venv
.\.venv\Scripts\activate      # or source .venv/bin/activate
pip install -r requirements.txt
cp .env.example .env          # then set secrets + DB URL
uvicorn main:app --reload
```

Key envs (see `.env.example`):
- `TOKENCONTROL_DATABASE_URL` (default sqlite for dev; Postgres recommended)
- `TOKENCONTROL_JWT_SECRET`, `TOKENCONTROL_JWT_ISSUER`
- `TOKENCONTROL_ACCESS_TOKEN_EXP_MINUTES`, `TOKENCONTROL_REFRESH_TOKEN_EXP_MINUTES`
- `TOKENCONTROL_UNLOCK_MINUTES_DEFAULT`, `TOKENCONTROL_REQUIRED_APPROVALS_DEFAULT`
- `TOKENCONTROL_CORS_ORIGINS`

Alembic is wired to `app.db.base.Base` for migrations; tables also auto-create on startup for dev.

## Frontend (`Web/frontend`)
- Vite + React Router + CSS theme matching the governance mock (`aegis-governance-mock.html`).
- Auth pages: /login (email/password) → /mfa (OTP) → routes by role (/admin or /gov).
- Admin pages: Authorities, Desktops, Audit Logs, System Settings scaffolds.
- Governance pages: Assigned desktops list + detail view with per-session approval rule messaging.
- API helpers in `src/api/` ready to call the FastAPI routes with Bearer tokens kept in memory only.

### Run
```bash
cd Web/frontend
npm install
npm run dev      # proxies /api -> http://localhost:8000
```

Style entry: `src/styles/theme.css`; main app at `src/app/App.tsx` with layouts in `src/layouts/` and pages under `src/pages/`.

## WPF (TokenControl) integration sketch
- First run: generate/stash `DesktopAppId`, call `POST /api/desktop/register` with metadata, then immediately call `GET /api/desktop/{id}/unlock-status`.
- Locked UX: show lock screen until `isUnlocked` with `remainingSeconds > 0`; poll unlock-status on a short interval (server time is source of truth).
- Unlock: honor `unlockedUntilUtc` and keep polling to detect expiry; when expired, return to lock screen and optionally start a new approval session.
- Authentication to API: store a per-desktop secret or client cert issued at registration and send on each call.

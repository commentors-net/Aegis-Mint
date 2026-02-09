# Aegis Mint

Aegis Mint is a multi-part system for token minting, governance approvals, and secure distribution of Shamir recovery shares. It includes desktop applications, a governance web portal, and a separate Share Portal for token users.

## Components (Repo Layout)
- `Web/backend` - FastAPI backend for governance, admin, and share management APIs.
- `Web/frontend` - React admin and governance portal (Token approvals, share assignment UI).
- `ClientWeb/backend` - FastAPI proxy for token users (share portal API).
- `ClientWeb/frontend` - React Share Portal for token users (login, MFA, downloads).
- `Mint` - Desktop apps (AegisMint.Mint, TokenControl, ShareManager).
- `Resources` - User manuals, architecture, requirements, and testing references.
- `Scripts` - Supporting scripts (vault key export, installer helpers).

## Developer Quick Start (Local)

1) Start Web backend
```bash
cd Web/backend
python -m venv .venv
.\.venv\Scripts\activate
pip install -r requirements.txt
cp .env.example .env
uvicorn main:app --reload
```

2) Start Web frontend
```bash
cd Web/frontend
npm install
npm run dev
```

3) Start ClientWeb backend
```bash
cd ClientWeb/backend
python -m venv venv
venv\Scripts\activate
pip install -r requirements.txt
copy .env.example .env
python main.py
```

4) Start ClientWeb frontend
```bash
cd ClientWeb/frontend
npm install
npm run dev
```

Local URLs (default):
- Admin/Governance portal: http://127.0.0.1:5173
- Share Portal: http://127.0.0.1:5174
- Web backend: http://127.0.0.1:8000
- ClientWeb backend: http://127.0.0.1:8001

For more detail, see `Web/README.md`, `ClientWeb/README.md`, and `ClientWeb/QUICK-START.md`.

## End User Overview

Admin flow:
1) Mint a token with the desktop app and upload shares.
2) Use the Admin portal to create token users and assign shares.
3) Re-enable downloads if a user loses their share.

Token user flow:
1) Log in to the Share Portal with email/password and MFA.
2) Download assigned shares (one-time by default).
3) View download history on the dashboard.

## Key Documentation
- Share management architecture and flows: `SHARE-MANAGEMENT-FLOW-FULL-IMPLEMENTATION.md`
- Legacy share flow reference: `SHARE-MANAGEMENT-FLOW.md`
- Database restructuring summary: `DATABASE-RESTRUCTURING-COMPLETE.md`
- Migration history (service IPC): `MIGRATION-SUMMARY.md`
- Admin app changes: `ADMINAPP-UPDATES.md`
- Share management test plan: `TEST-PLAN-SHARE-MANAGEMENT-USER-FRIENDLY.md`
- Token control test plan: `TEST-PLAN-USER-FRIENDLY.md`
- Deployment notes for Web frontend: `Web/frontend/DEPLOYMENT.md`
- ClientWeb implementation notes: `ClientWeb/IMPLEMENTATION.md`
- ClientWeb API details: `ClientWeb/backend/README.md`

## Manuals and Reference
- User guide: `Resources/User-Manual.md`
- Architecture overview: `Resources/AegisMint-architecture.md`
- System requirements: `Resources/AegisMint-requirements.md`
- Core service overview: `Resources/AegisMint-Core-Service-overview.md`
- Admin app manual: `Resources/AegisMint-AdminApp-manual.md`
- Client library manual: `Resources/AegisMint-Client-manual.md`
- Uninstaller scenarios: `Resources/Uninstaller-Test-Scenarios.md`

## Testing
- Share management test plan: `TEST-PLAN-SHARE-MANAGEMENT-USER-FRIENDLY.md`
- Token control end-to-end test plan: `TEST-PLAN-USER-FRIENDLY.md`
- IPC test results (service): `TEST-RESULTS.md`

## Notes
- Token users are global by email and mapped to tokens via `token_user_assignments`.
- Share downloads are one-time by default and all attempts are logged.
- The ClientWeb backend proxies to the main backend for token user security isolation.

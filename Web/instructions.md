# Web Recovery Flow Guidelines (React TS + FastAPI)

## Goal
Add a dedicated, hardened recovery flow that reconstructs the genesis mnemonic from Shamir shares. This must be isolated from normal deploy/mint operations and avoid any logging of secrets.

## Backend (FastAPI)
- Endpoint: `POST /recover` – accepts exactly the threshold number of shares from the same split; validate format and count.
- Perform Shamir reconstruction server-side in memory only. Never log or persist shares or the mnemonic.
- Security:
  - Require strong auth (e.g., JWT + role, or mutual TLS) and rate limiting.
  - Enforce HTTPS/TLS; consider IP allowlists or VPN-only access.
  - Disable response caching; set strict cache-control headers.
- Handling:
  - On success, return the mnemonic once. Immediately zero and discard intermediate buffers.
  - On failure, return generic errors; avoid leaking structure/validation details.
  - Log only metadata (who/when/attempt count); never log shares or mnemonic.

## Frontend (React + TypeScript)
- Gated UI:
  - Auth gate plus an explicit warning about sensitivity.
  - Form that accepts exactly the threshold shares (text areas/file upload). Enforce count and validate basic structure client-side.
- Display:
  - Show the mnemonic briefly with a deliberate “Copy once” control.
  - Auto-clear mnemonic from state after a short timeout or on navigation.
  - Disable browser autofill; set `autocomplete="off"`; prevent caching.
- UX safeguards:
  - Confirm intent before submission.
  - Show status for in-flight requests and errors without revealing details.

## Data & Transport
- HTTPS/TLS required; consider mutual TLS for operators.
- Do not cache responses; add `Cache-Control: no-store`.
- Never store shares or mnemonic client-side beyond transient state; no localStorage/sessionStorage.

## Testing
- Positive: threshold shares from one split reconstruct the original mnemonic.
- Negative: fewer than threshold fails; mixed shares from different splits fail.
- Security checks: no logs containing shares/mnemonic; headers disable caching.

## Operational Notes
- Run recovery in a restricted environment separate from minting.
- Restrict who can call `/recover`; audit every attempt (who/when), not contents.
- After displaying the mnemonic, encourage offline handling and immediate clearance from the UI.*** End Patch

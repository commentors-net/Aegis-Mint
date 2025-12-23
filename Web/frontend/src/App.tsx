import { useEffect, useMemo, useState } from "react";

type TokenResponse = {
  access_token: string;
  expires_at: string;
};

type UnlockResponse = {
  unlocked_until: string;
  window_minutes: number;
};

const api = async <T,>(path: string, options: RequestInit = {}): Promise<T> => {
  const res = await fetch(`/api${path}`, {
    ...options,
    headers: {
      "Content-Type": "application/json",
      ...options.headers,
    },
  });
  if (!res.ok) {
    const msg = await res.text();
    throw new Error(msg || res.statusText);
  }
  return res.json() as Promise<T>;
};

export default function App() {
  const [username, setUsername] = useState("operator");
  const [password, setPassword] = useState("");
  const [totp, setTotp] = useState("");
  const [reason, setReason] = useState("");

  const [token, setToken] = useState<string | null>(null);
  const [expiresAt, setExpiresAt] = useState<Date | null>(null);
  const [unlockUntil, setUnlockUntil] = useState<Date | null>(null);
  const [status, setStatus] = useState<string>("Locked");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const expiresInSeconds = useMemo(() => {
    if (!expiresAt) return 0;
    const diff = expiresAt.getTime() - Date.now();
    return Math.max(0, Math.floor(diff / 1000));
  }, [expiresAt]);

  useEffect(() => {
    if (!expiresAt) return;
    const id = setInterval(() => {
      if (expiresAt.getTime() <= Date.now()) {
        setToken(null);
        setExpiresAt(null);
        setStatus("Locked");
      }
    }, 1000);
    return () => clearInterval(id);
  }, [expiresAt]);

  const handleLogin = async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await api<TokenResponse>("/auth/login", {
        method: "POST",
        body: JSON.stringify({ username, password, totp }),
      });
      setToken(res.access_token);
      setExpiresAt(new Date(res.expires_at));
      setStatus("Authenticated — ready to request unlock");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Login failed");
      setToken(null);
      setExpiresAt(null);
      setStatus("Locked");
    } finally {
      setLoading(false);
    }
  };

  const handleUnlock = async () => {
    if (!token) {
      setError("Authenticate first.");
      return;
    }
    setLoading(true);
    setError(null);
    try {
      const res = await api<UnlockResponse>("/unlock", {
        method: "POST",
        headers: { Authorization: `Bearer ${token}` },
        body: JSON.stringify({ reason }),
      });
      const until = new Date(res.unlocked_until);
      setUnlockUntil(until);
      setStatus(`Unlocked until ${until.toLocaleString()} (window ${res.window_minutes}m)`);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Unlock failed");
    } finally {
      setLoading(false);
    }
  };

  const logout = () => {
    setToken(null);
    setExpiresAt(null);
    setUnlockUntil(null);
    setStatus("Locked");
    setPassword("");
    setTotp("");
  };

  return (
    <div className="page">
      <div className="card stack">
        <div>
          <h2>Aegis Token Control Governance</h2>
          <p className="muted">
            Authenticate with password + TOTP, then request a timed unlock (default 15 minutes). JWTs are kept only
            in-memory and expire automatically.
          </p>
        </div>

        <div className="stack">
          <div className="row">
            <div style={{ flex: 1 }}>
              <label htmlFor="username">Username</label>
              <input
                id="username"
                autoComplete="off"
                value={username}
                onChange={(e) => setUsername(e.target.value)}
                disabled={loading}
              />
            </div>
            <div style={{ flex: 1 }}>
              <label htmlFor="password">Password</label>
              <input
                id="password"
                type="password"
                autoComplete="off"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                disabled={loading}
              />
            </div>
            <div style={{ flex: 1 }}>
              <label htmlFor="totp">TOTP (Google Authenticator)</label>
              <input
                id="totp"
                type="text"
                inputMode="numeric"
                pattern="[0-9]*"
                autoComplete="off"
                value={totp}
                onChange={(e) => setTotp(e.target.value)}
                disabled={loading}
                placeholder="6-digit code"
              />
            </div>
          </div>

          <div className="row">
            <button onClick={handleLogin} disabled={loading}>
              {loading ? "Working..." : "Authenticate"}
            </button>
            {token && (
              <button onClick={logout} disabled={loading} style={{ background: "#475569" }}>
                Logout
              </button>
            )}
            {expiresAt && (
              <span className="status">
                JWT expires in {Math.floor(expiresInSeconds / 60)}m {expiresInSeconds % 60}s
              </span>
            )}
          </div>
        </div>

        <div className="stack">
          <label htmlFor="reason">Unlock reason / ticket</label>
          <textarea
            id="reason"
            rows={3}
            autoComplete="off"
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            disabled={loading || !token}
            placeholder="Ticket ID and justification for unlock window"
          />
          <div className="row">
            <button onClick={handleUnlock} disabled={loading || !token}>
              {loading ? "Working..." : "Request 15m Unlock"}
            </button>
            {unlockUntil && <span className="status">Unlock window ends at {unlockUntil.toLocaleTimeString()}</span>}
          </div>
        </div>

        {error && <div className="status" style={{ background: "#fee2e2", borderColor: "#fecdd3", color: "#991b1b" }}>{error}</div>}
        <div className="status">{status}</div>
      </div>
    </div>
  );
}

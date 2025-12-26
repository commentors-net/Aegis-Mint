import { useEffect, useState } from "react";

import * as adminApi from "../../api/admin";
import Button from "../../components/Button";
import { useAuth } from "../../auth/useAuth";

export default function SystemSettingsPage() {
  const { token } = useAuth();
  const [requiredApprovalsDefault, setRequired] = useState(2);
  const [unlockMinutesDefault, setUnlock] = useState(15);
  const [loading, setLoading] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const load = async () => {
    if (!token) return;
    setLoading(true);
    setError(null);
    try {
      const res = await adminApi.getSystemSettings(token);
      setRequired(res.requiredApprovalsDefault);
      setUnlock(res.unlockMinutesDefault);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to load settings");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    load();
  }, [token]);

  const save = async () => {
    if (!token) return;
    setLoading(true);
    setError(null);
    setMessage(null);
    try {
      await adminApi.updateSystemSettings(token, { requiredApprovalsDefault, unlockMinutesDefault });
      setMessage("Saved");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to save settings");
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="grid two">
      <div className="card ghost">
        <div className="hd">
          <h3>Global defaults</h3>
        </div>
        <div className="bd stack">
          <label className="field">
            <div className="field-label">Default required approvals (N)</div>
            <input
              className="input"
              type="number"
              min={1}
              value={requiredApprovalsDefault}
              onChange={(e) => setRequired(Number(e.target.value))}
            />
          </label>
          <label className="field">
            <div className="field-label">Unlock duration (minutes)</div>
            <input
              className="input"
              type="number"
              min={1}
              value={unlockMinutesDefault}
              onChange={(e) => setUnlock(Number(e.target.value))}
            />
          </label>
          <Button onClick={save} disabled={loading}>
            Save
          </Button>
          {message && <span className="muted">{message}</span>}
          {error && <span className="muted" style={{ color: "#f97066" }}>{error}</span>}
          <p className="muted">You can keep policy per-desktop only and skip global defaults.</p>
        </div>
      </div>
      <div className="card ghost">
        <div className="hd">
          <h3>Security</h3>
        </div>
        <div className="bd">
          <ul className="muted">
            <li>Short-lived access tokens + refresh token.</li>
            <li>Rate-limit approvals.</li>
            <li>TokenControl should authenticate to API (desktop secret/cert).</li>
          </ul>
        </div>
      </div>
    </div>
  );
}

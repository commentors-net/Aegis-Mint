import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";

import Button from "../components/Button";
import Badge from "../components/Badge";
import { useAuth } from "./useAuth";
import Shell from "../layouts/Shell";

export default function LoginPage() {
  const navigate = useNavigate();
  const { login, challengeId, loading, error, enrollmentSecret, otpauthUrl, mfaQrBase64 } = useAuth();
  const [email, setEmail] = useState("authority@example.com");
  const [password, setPassword] = useState("");

  useEffect(() => {
    if (challengeId) {
      navigate("/mfa");
    }
  }, [challengeId, navigate]);

  return (
    <Shell>
      <div className="grid two">
        <div className="card">
          <div className="hd">
            <h2>Login</h2>
            <Badge tone="warn">Password + TOTP</Badge>
          </div>
          <div className="bd stack">
            <label className="field">
              <div className="field-label">Email</div>
              <input className="input" value={email} onChange={(e) => setEmail(e.target.value)} autoComplete="off" />
            </label>
            <label className="field">
              <div className="field-label">Password</div>
              <input
                className="input"
                type="password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                autoComplete="off"
              />
            </label>
            <div className="row">
              <Button onClick={() => login(email, password)} disabled={loading}>
              {loading ? "Working..." : "Sign in"}
            </Button>
            {error && <span className="muted">{error}</span>}
          </div>
          <p className="muted">
            Login flow: email/password + 2FA OTP → route to the right dashboard by role.
          </p>
          {enrollmentSecret && (
            <div className="status" style={{ background: "rgba(77,183,255,0.1)", borderColor: "rgba(77,183,255,0.4)" }}>
              <div className="strong">Set up MFA</div>
              <div className="muted small">Scan the QR or add this secret to your authenticator (first login only):</div>
              {mfaQrBase64 && (
                <img src={mfaQrBase64} alt="MFA QR" style={{ maxWidth: 160, background: "#fff", padding: 6, borderRadius: 8 }} />
              )}
              <div className="mono">{enrollmentSecret}</div>
              {otpauthUrl && (
                <div className="muted small" style={{ wordBreak: "break-all" }}>
                  otpauth:// link: {otpauthUrl}
                </div>
              )}
            </div>
          )}
        </div>
      </div>
      <div className="card">
        <div className="hd">
          <h2>System summary</h2>
          </div>
          <div className="bd stack">
            <div className="kpi">
              <div className="tile">
                <div className="num">DesktopAppId</div>
                <div className="lbl">Unique GUID per TokenControl install</div>
              </div>
              <div className="tile">
                <div className="num">N approvals</div>
                <div className="lbl">Per desktop (configured by Super Admin)</div>
              </div>
              <div className="tile">
                <div className="num">15 minutes</div>
                <div className="lbl">Unlock from Nth approval time</div>
              </div>
            </div>
            <p className="muted">
              TokenControl first run registers DesktopAppId. Subsequent runs poll server for unlock-status. Governance
              approves desktops assigned to them.
            </p>
          </div>
        </div>
      </div>
    </Shell>
  );
}

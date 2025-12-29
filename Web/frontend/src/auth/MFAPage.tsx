import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";

import Badge from "../components/Badge";
import Button from "../components/Button";
import Shell from "../layouts/Shell";
import { useAuth } from "./useAuth";

export default function MFAPage() {
  const navigate = useNavigate();
  const { verifyOtp, challengeId, loading, error, role, enrollmentSecret, otpauthUrl, mfaQrBase64 } = useAuth();
  const [otp, setOtp] = useState("");

  useEffect(() => {
    if (!challengeId) {
      navigate("/login");
    }
  }, [challengeId, navigate]);

  useEffect(() => {
    if (role === "SuperAdmin") {
      navigate("/admin");
    } else if (role === "GovernanceAuthority") {
      navigate("/gov");
    }
  }, [role, navigate]);

  const handleVerify = () => {
    verifyOtp(otp);
  };

  return (
    <Shell>
      <div className="grid two">
        <div className="card">
          <div className="hd">
            <h2>Two-Factor Authentication</h2>
            <Badge>Step 2 of 2</Badge>
          </div>
          <div className="bd stack">
            <div className="muted">
              Enter the 6-digit code from your authenticator app (TOTP). Codes are never stored or logged.
            </div>
            {enrollmentSecret && (
              <div className="status" style={{ background: "rgba(77,183,255,0.1)", borderColor: "rgba(77,183,255,0.4)" }}>
                <div className="strong">First-time setup</div>
                <div className="muted small">Add this secret to your authenticator, then enter the 6-digit code:</div>
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
            <div className="row">
              <input
                className="input mono"
                inputMode="numeric"
                maxLength={6}
                placeholder="123456"
                value={otp}
                onChange={(e) => setOtp(e.target.value)}
              />
              <Button variant="ghost" onClick={() => alert("Resend / recovery flow would go here")}>
                Need help?
              </Button>
            </div>
            <div className="row">
              <Button onClick={handleVerify} disabled={loading || !otp}>
                {loading ? "Verifying..." : "Verify & Continue"}
              </Button>
              <Button variant="ghost" onClick={() => navigate("/login")}>
                Back
              </Button>
            </div>
            {error && <span className="muted">{error}</span>}
            <p className="muted small">
              Backend note: implement TOTP with user secret (e.g., pyotp) or integrate with an IdP. Access tokens remain
              in-memory only.
            </p>
          </div>
        </div>
        <div className="card">
          <div className="hd">
            <h2>Security notes</h2>
          </div>
          <div className="bd">
            <ul className="muted">
              <li>Require MFA for SuperAdmin and GovernanceAuthority.</li>
              <li>Use short-lived access tokens + refresh tokens.</li>
              <li>Log all approvals and unlock events.</li>
            </ul>
          </div>
        </div>
      </div>
    </Shell>
  );
}

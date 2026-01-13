import { ReactNode, useState } from "react";
import { Link } from "react-router-dom";

import * as authApi from "../api/auth";
import Badge from "../components/Badge";
import Button from "../components/Button";
import { useAuth } from "../auth/useAuth";

export default function Shell({ children }: { children: ReactNode }) {
  const { role, user, token, logout } = useAuth();
  const [showPwd, setShowPwd] = useState(false);
  const [current, setCurrent] = useState("");
  const [next, setNext] = useState("");
  const [pwdMessage, setPwdMessage] = useState<string | null>(null);
  const [pwdError, setPwdError] = useState<string | null>(null);
  const [working, setWorking] = useState(false);

  const changePassword = async () => {
    if (!token) return;
    setWorking(true);
    setPwdMessage(null);
    setPwdError(null);
    try {
      await authApi.changePassword(token, current, next);
      setPwdMessage("Password updated");
      setCurrent("");
      setNext("");
      setShowPwd(false);
    } catch (err) {
      setPwdError(err instanceof Error ? err.message : "Failed to change password");
    } finally {
      setWorking(false);
    }
  };

  return (
    <div className="page-shell">
      <header className="topbar">
        <div className="brand">
          <span className="brand-mark" />
          <div>
            <div className="brand-title">Aegis Governance</div>
            <div className="brand-sub">TokenControl unlock approvals</div>
          </div>
          <Badge tone={role ? "good" : "warn"}>
            {user ? `Signed in: ${user.email}` : "Not signed in"}
          </Badge>
        </div>
        <div className="nav">
          {role === "SuperAdmin" && (
            <Link to="/admin" className="link">
              Admin
            </Link>
          )}
          {role === "GovernanceAuthority" && (
            <Link to="/gov" className="link">
              Governance
            </Link>
          )}
          {user && (
            <Button variant="ghost" onClick={() => setShowPwd(true)}>
              Change password
            </Button>
          )}
          {user && (
            <Button variant="ghost" onClick={logout} className="logout-btn">
              Logout
            </Button>
          )}
        </div>
      </header>
      <main className="page-body">{children}</main>
      {showPwd && (
        <div className="modal">
          <div className="modal-card">
            <div className="hd">
              <h3>Change password</h3>
              <Button variant="ghost" size="sm" onClick={() => setShowPwd(false)}>
                Close
              </Button>
            </div>
            <div className="bd stack">
              <label className="field">
                <div className="field-label">Current password</div>
                <input className="input" type="password" value={current} onChange={(e) => setCurrent(e.target.value)} />
              </label>
              <label className="field">
                <div className="field-label">New password</div>
                <input className="input" type="password" value={next} onChange={(e) => setNext(e.target.value)} />
              </label>
              <Button onClick={changePassword} disabled={working || !current || !next}>
                Save
              </Button>
              {pwdMessage && <span className="muted">{pwdMessage}</span>}
              {pwdError && <span className="muted" style={{ color: "#f97066" }}>{pwdError}</span>}
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

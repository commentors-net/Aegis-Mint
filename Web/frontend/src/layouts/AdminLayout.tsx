import { NavLink, Outlet } from "react-router-dom";

import Shell from "./Shell";

const tabs = [
  { path: "authorities", label: "Governance Authorities" },
  { path: "desktops", label: "Manage Desktops" },
  { path: "assign-desktops", label: "Assign Desktops" },
  { path: "mint-approval", label: "Mint Approval" },
  { path: "tokens", label: "Share Management" },
  { path: "certificates", label: "Certificates" },
  { path: "recover-shares", label: "Recover Shares" },
  { path: "downloads", label: "Downloads" },
  { path: "audit", label: "Audit Logs" },
  { path: "settings", label: "System Settings" },
];

export default function AdminLayout() {
  return (
    <Shell>
      <section className="card">
        <div className="hd">
          <div>
            <h2>Super Admin Console</h2>
            <p className="muted">Activate desktops, manage governance users, and audit approvals.</p>
          </div>
          <div className="tabs">
            {tabs.map((t) => (
              <NavLink key={t.path} to={t.path} className="tab">
                {t.label}
              </NavLink>
            ))}
          </div>
        </div>
        <div className="bd">
          <Outlet />
        </div>
      </section>
    </Shell>
  );
}

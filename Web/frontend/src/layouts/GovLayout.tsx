import { NavLink, Outlet } from "react-router-dom";

import Shell from "./Shell";

export default function GovLayout() {
  return (
    <Shell>
      <section className="card">
        <div className="hd">
          <div>
            <h2>Governance Dashboard</h2>
            <p className="muted">Approve assigned desktops; one approval per session until unlock expires.</p>
          </div>
          <div className="tabs">
            <NavLink to="assigned" className="tab">
              Assigned desktops
            </NavLink>
          </div>
        </div>
        <div className="bd">
          <Outlet />
        </div>
      </section>
    </Shell>
  );
}

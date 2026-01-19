import { useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";

import * as govApi from "../../api/governance";
import { useAuth } from "../../auth/useAuth";
import Badge from "../../components/Badge";
import Button from "../../components/Button";
import Toast from "../../components/Toast";
import { Table, Td, Th } from "../../components/Table";

export default function AssignedDesktopsPage() {
  const navigate = useNavigate();
  const { token } = useAuth();
  const [rows, setRows] = useState<govApi.AssignedDesktop[]>([]);
  const [search, setSearch] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [, setTick] = useState(0); // trigger re-render for countdown
  const [fetchedAt, setFetchedAt] = useState(Date.now());
  const [toast, setToast] = useState<{ message: string; type: "success" | "error" } | null>(null);

  useEffect(() => {
    const id = setInterval(() => setTick((v) => v + 1), 1000);
    return () => clearInterval(id);
  }, []);

  const refresh = async () => {
    if (!token) return;
    setLoading(true);
    setError(null);
    try {
      const data = await govApi.getAssignedDesktops(token);
      setRows(data);
      setFetchedAt(Date.now());
      setToast({ message: "Desktops refreshed successfully", type: "success" });
    } catch (err) {
      const errorMsg = err instanceof Error ? err.message : "Failed to load assigned desktops";
      setError(errorMsg);
      setToast({ message: errorMsg, type: "error" });
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    refresh();
  }, [token]);

  const filtered = useMemo(() => {
    // Only show Active desktops
    const activeRows = rows.filter((r) => r.status === "Active");
    if (!search) return activeRows;
    const q = search.toLowerCase();
    return activeRows.filter((r) => (r.nameLabel || "").toLowerCase().includes(q) || r.desktopAppId.toLowerCase().includes(q));
  }, [rows, search]);

  const formatLocal = (value?: string) => {
    if (!value) return "N/A";
    const normalized = value.match(/[zZ]|[+-]\d{2}:\d{2}$/) ? value : `${value}Z`;
    const d = new Date(normalized);
    if (isNaN(d.getTime())) return value;
    return d.toLocaleString();
  };

  const remainingSeconds = (row: govApi.AssignedDesktop) => {
    const base = row.remainingSeconds ?? 0;
    const elapsed = Math.floor((Date.now() - fetchedAt) / 1000);
    let remaining = Math.max(0, base - elapsed);
    if (remaining === 0 && row.unlockedUntilUtc) {
      const diff = new Date(row.unlockedUntilUtc).getTime() - Date.now();
      remaining = Math.max(0, Math.floor(diff / 1000));
    }
    return remaining;
  };

  const handleApprove = async (desktopAppId: string, appType: string) => {
    if (!token) return;
    setLoading(true);
    setError(null);
    try {
      await govApi.approveDesktop(desktopAppId, appType, token);
      setToast({ message: "Desktop approved successfully", type: "success" });
      refresh();
    } catch (err) {
      const errorMsg = err instanceof Error ? err.message : "Approval failed";
      setError(errorMsg);
      setToast({ message: errorMsg, type: "error" });
    } finally {
      setLoading(false);
    }
  };

  return (
    <>
      {toast && (
        <Toast
          message={toast.message}
          type={toast.type}
          onClose={() => setToast(null)}
        />
      )}
      <div className="stack">
      <div className="row">
        <input className="input" placeholder="Search by name / DesktopAppId" value={search} onChange={(e) => setSearch(e.target.value)} />
        <div className="spacer" />
        <Button size="sm" variant="ghost" onClick={refresh} disabled={loading}>
          Refresh
        </Button>
      </div>
      {error && <div className="status" style={{ background: "#fee2e2", borderColor: "#fecdd3", color: "#991b1b" }}>{error}</div>}
      <Table>
        <thead>
          <tr>
            <Th>Desktop</Th>
            <Th>DesktopAppId</Th>
            <Th>Type</Th>
            <Th>Approvals</Th>
            <Th>Action</Th>
          </tr>
        </thead>
        <tbody>
          {filtered.map((d) => {
            const remaining = remainingSeconds(d);
            const disableApprove = d.alreadyApproved && remaining > 0;
            const sessionLabel =
              d.sessionStatus === "Unlocked"
                ? remaining > 0
                  ? `Unlocked (${Math.floor(remaining / 60)}m ${remaining % 60}s left)`
                  : "Expired"
                : d.sessionStatus === "Pending"
                  ? "Session: Pending"
                  : d.sessionStatus;
            return (
              <tr key={d.desktopAppId + d.appType} onClick={() => navigate(`/gov/desktops/${d.desktopAppId}/${d.appType || "TokenControl"}`)} style={{ cursor: "pointer" }}>
                <Td>
                  <div className="strong">{d.nameLabel || "Unlabeled"}</div>
                  <div className="muted small">Last seen: {formatLocal(d.lastSeenAtUtc)}</div>
                </Td>
                <Td className="mono">{d.desktopAppId}</Td>
                <Td>
                  <Badge tone={d.appType === "Mint" ? "info" : "neutral"}>{d.appType || "TokenControl"}</Badge>
                </Td>
                <Td>
                  <b>
                    {d.approvalsSoFar} / {d.requiredApprovalsN}
                  </b>
                  <div className="muted small">{sessionLabel}</div>
                </Td>
                <Td>
                  <Button
                    size="sm"
                    onClick={(e) => {
                      e.stopPropagation();
                      handleApprove(d.desktopAppId, d.appType || "TokenControl");
                    }}
                    disabled={disableApprove || loading}
                  >
                    {disableApprove ? (remaining > 0 ? "Unlocked" : "Approved") : "Approve"}
                  </Button>
                </Td>
              </tr>
            );
          })}
        </tbody>
      </Table>
      <p className="muted">
        Governance can approve only once per desktop per active approval session. Approve disables until unlock window
        expires.
      </p>
      </div>
    </>
  );
}

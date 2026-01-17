import { useEffect, useMemo, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";

import * as govApi from "../../api/governance";
import { useAuth } from "../../auth/useAuth";
import Badge from "../../components/Badge";
import Button from "../../components/Button";
import { Table, Td, Th } from "../../components/Table";

export default function DesktopDetailPage() {
  const navigate = useNavigate();
  const { desktopAppId, appType } = useParams<{ desktopAppId: string; appType: string }>();
  const { token } = useAuth();
  const [detail, setDetail] = useState<govApi.ApprovalSummary | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [tick, setTick] = useState(0);
  const [fetchedAt, setFetchedAt] = useState(Date.now());

  useEffect(() => {
    const id = setInterval(() => setTick((v) => v + 1), 1000);
    return () => clearInterval(id);
  }, []);

  const load = async () => {
    if (!token || !desktopAppId || !appType) return;
    setLoading(true);
    setError(null);
    try {
      const res = await govApi.getDesktopHistory(desktopAppId, appType, token);
      setDetail(res);
      setFetchedAt(Date.now());
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to load history");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    load();
  }, [desktopAppId, appType, token]);

  const remainingSeconds = useMemo(() => {
    if (!detail) return 0;
    const base = detail.remainingSeconds ?? 0;
    const elapsed = Math.floor((Date.now() - fetchedAt) / 1000);
    let remaining = Math.max(0, base - elapsed);
    if (remaining === 0 && detail.unlockedUntilUtc) {
      const diff = new Date(detail.unlockedUntilUtc).getTime() - Date.now();
      remaining = Math.max(0, Math.floor(diff / 1000));
    }
    return remaining;
  }, [detail, fetchedAt, tick]);

  const isUnlockedActive = detail?.status === "Unlocked" && remainingSeconds > 0;
  const displayStatus = detail
    ? detail.status === "Unlocked" && remainingSeconds <= 0
      ? "Expired"
      : detail.status
    : "None";
  const canApproveAgain = !detail || detail.status !== "Unlocked" || remainingSeconds <= 0;

  const formatLocal = (value?: string) => {
    if (!value) return "N/A";
    // Incoming timestamps are UTC. Ensure we parse as UTC even if the string has no timezone designator.
    const normalized = value.match(/[zZ]|[+-]\d{2}:\d{2}$/) ? value : `${value}Z`;
    const d = new Date(normalized);
    if (isNaN(d.getTime())) return value;
    return d.toLocaleString();
  };

  const approve = async () => {
    if (!token || !desktopAppId || !appType) return;
    setLoading(true);
    setError(null);
    try {
      await govApi.approveDesktop(desktopAppId, appType, token);
      await load();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Approval failed");
    } finally {
      setLoading(false);
    }
  };

  if (!desktopAppId) {
    return (
      <div className="stack">
        <p className="muted">Select a desktop from the list.</p>
        <Button variant="ghost" onClick={() => navigate("/gov/assigned")}>
          Back to list
        </Button>
      </div>
    );
  }

  return (
    <div className="stack">
      <div className="row">
        <div className="pill">Selected: {desktopAppId}</div>
        <Badge tone={isUnlockedActive ? "good" : "warn"}>{displayStatus}</Badge>
        <div className="spacer" />
        <Button disabled={!canApproveAgain || loading} onClick={approve}>
          Approve
        </Button>
      </div>

      <div className="card ghost">
        <div className="hd">
          <h3>Current session</h3>
          <Badge tone="neutral">{displayStatus}</Badge>
        </div>
        <div className="bd stack">
          {error && <span className="muted" style={{ color: "#f97066" }}>{error}</span>}
          <div className="row">
            <div>
              <div className="muted small">SessionId</div>
              <div className="mono">{detail?.sessionId ?? "N/A"}</div>
            </div>
            <div>
              <div className="muted small">Approvals</div>
              <div className="strong">
                {detail?.approvals.length ?? 0} / {detail?.requiredApprovalsSnapshot ?? "N/A"}
              </div>
            </div>
            <div>
              <div className="muted small">Unlock window</div>
              <div className="strong">
                {isUnlockedActive
                  ? `${Math.floor(remainingSeconds / 60)}m ${remainingSeconds % 60}s left`
                  : detail?.unlockedUntilUtc
                    ? "Expired"
                    : "Pending"}
              </div>
            </div>
            {detail?.unlockedUntilUtc && (
              <div>
                <div className="muted small">Unlocked until</div>
                <div className="mono">{formatLocal(detail.unlockedUntilUtc)}</div>
              </div>
            )}
          </div>
          <Table>
            <thead>
              <tr>
                <Th>Approver</Th>
                <Th>Approved At (Local)</Th>
              </tr>
            </thead>
            <tbody>
              {(detail?.approvals || []).map((a) => (
                <tr key={a.approverUserId + a.approvedAtUtc}>
                  <Td>{a.approverEmail ?? a.approverUserId}</Td>
                  <Td className="mono">{formatLocal(a.approvedAtUtc)}</Td>
                </tr>
              ))}
              {(!detail || detail.approvals.length === 0) && (
                <tr>
                  <Td className="muted">Waiting for first approver</Td>
                  <Td className="muted">--</Td>
                </tr>
              )}
            </tbody>
          </Table>
          <p className="muted">
            Rule: a governance user can approve only once per desktop per session. After unlock expires, a new session can
            start and they can approve again.
          </p>
        </div>
      </div>
    </div>
  );
}

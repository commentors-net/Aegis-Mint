import { useEffect, useMemo, useState } from "react";

import * as adminApi from "../../api/admin";
import { Table, Td, Th } from "../../components/Table";
import { useAuth } from "../../auth/useAuth";
import Button from "../../components/Button";

export default function AuditLogsPage() {
  const { token } = useAuth();
  const [logs, setLogs] = useState<adminApi.AuditEntry[]>([]);
  const [page, setPage] = useState(1);
  const [pageSize] = useState(10);
  const [total, setTotal] = useState(0);
  const [q, setQ] = useState("");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const totalPages = useMemo(() => Math.max(1, Math.ceil(total / pageSize)), [total, pageSize]);

  const load = async (pageArg = page, qArg = q) => {
    if (!token) return;
    setLoading(true);
    setError(null);
    try {
      const res = await adminApi.getAuditLogs(token, { page: pageArg, pageSize, q: qArg });
      setLogs(res.items);
      setTotal(res.total);
      setPage(res.page);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to load audit logs");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    load(1, q);
  }, [token]);

  const applyFilter = () => load(1, q);
  const nextPage = () => {
    if (page < totalPages) load(page + 1, q);
  };
  const prevPage = () => {
    if (page > 1) load(page - 1, q);
  };

  return (
    <div className="stack">
      <div className="row">
        <input
          className="input"
          placeholder="Filter by DesktopAppId / actor / action / details"
          value={q}
          onChange={(e) => setQ(e.target.value)}
          onKeyDown={(e) => e.key === "Enter" && applyFilter()}
        />
        <Button size="sm" variant="ghost" onClick={applyFilter} disabled={loading}>
          Filter
        </Button>
        <div className="spacer" />
        <Button size="sm" variant="ghost" onClick={() => load(1, "")} disabled={loading}>
          Clear
        </Button>
      </div>
      {error && <div className="status" style={{ background: "#fee2e2", borderColor: "#fecdd3", color: "#991b1b" }}>{error}</div>}
      <Table>
        <thead>
          <tr>
            <Th>When (UTC)</Th>
            <Th>DesktopAppId</Th>
            <Th>Action</Th>
            <Th>Actor</Th>
            <Th>Details</Th>
          </tr>
        </thead>
        <tbody>
          {logs.map((log) => (
            <tr key={log.id}>
              <Td className="mono">{log.at_utc}</Td>
              <Td className="mono">{log.desktop_app_id ?? "—"}</Td>
              <Td>{log.action}</Td>
              <Td>{log.actor_user_id ?? "system"}</Td>
              <Td className="muted">{log.details ?? "—"}</Td>
            </tr>
          ))}
        </tbody>
      </Table>
      <div className="row">
        <Button size="sm" variant="ghost" onClick={prevPage} disabled={loading || page <= 1}>
          Prev
        </Button>
        <span className="muted">
          Page {page} of {totalPages} (total {total})
        </span>
        <Button size="sm" variant="ghost" onClick={nextPage} disabled={loading || page >= totalPages}>
          Next
        </Button>
      </div>
    </div>
  );
}

import { useEffect, useMemo, useState } from "react";

import * as adminApi from "../../api/admin";
import Badge from "../../components/Badge";
import Button from "../../components/Button";
import { Table, Td, Th } from "../../components/Table";
import Toast from "../../components/Toast";
import { useAuth } from "../../auth/useAuth";

type DesktopRow = adminApi.Desktop;

type ModalMode = "create" | "edit" | null;

export default function DesktopsPage() {
  const { token } = useAuth();
  const enableRegister = (import.meta.env.VITE_ENABLE_DESKTOP_REGISTER || "").toLowerCase() === "true";
  const [rows, setRows] = useState<DesktopRow[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState("");
  const [toast, setToast] = useState<{ message: string; type: "success" | "error" } | null>(null);

  const [modalMode, setModalMode] = useState<ModalMode>(null);
  const [selected, setSelected] = useState<DesktopRow | null>(null);
  const [form, setForm] = useState({
    desktopAppId: "",
    nameLabel: "",
    requiredApprovalsN: 2,
    unlockMinutes: 15,
  });

  const filtered = useMemo(() => {
    // Filter out disabled desktops
    const activeRows = rows.filter((r) => r.status !== "Disabled");
    if (!search) return activeRows;
    const q = search.toLowerCase();
    return activeRows.filter((r) => (r.nameLabel || "").toLowerCase().includes(q) || r.desktopAppId.toLowerCase().includes(q));
  }, [rows, search]);

  const refresh = async () => {
    if (!token) return;
    setLoading(true);
    setError(null);
    try {
      const data = await adminApi.listDesktops(token);
      setRows(data);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to load desktops");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    refresh();
  }, [token]);

  const openCreate = () => {
    setForm({ desktopAppId: "", nameLabel: "", requiredApprovalsN: 2, unlockMinutes: 15 });
    setModalMode("create");
    setSelected(null);
  };

  const openEdit = (row: DesktopRow) => {
    setSelected(row);
    setForm({
      desktopAppId: row.desktopAppId,
      nameLabel: row.nameLabel || "",
      requiredApprovalsN: row.requiredApprovalsN,
      unlockMinutes: row.unlockMinutes,
    });
    setModalMode("edit");
  };

  const closeModal = () => {
    setModalMode(null);
    setSelected(null);
  };

  const handleCreate = async () => {
    if (!token) return;
    setLoading(true);
    setError(null);
    try {
      await adminApi.createDesktop(token, {
        desktopAppId: form.desktopAppId,
        nameLabel: form.nameLabel || undefined,
        requiredApprovalsN: form.requiredApprovalsN,
        unlockMinutes: form.unlockMinutes,
      });
      setToast({ message: "Desktop created successfully", type: "success" });
      closeModal();
      refresh();
    } catch (err) {
      const errorMsg = err instanceof Error ? err.message : "Failed to create desktop";
      setError(errorMsg);
      setToast({ message: errorMsg, type: "error" });
    } finally {
      setLoading(false);
    }
  };

  const handleSaveEdit = async () => {
    if (!token || !selected) return;
    setLoading(true);
    setError(null);
    try {
      await adminApi.updateDesktop(token, selected.desktopAppId, {
        nameLabel: form.nameLabel,
        requiredApprovalsN: form.requiredApprovalsN,
        unlockMinutes: form.unlockMinutes,
      });
      setToast({ message: "Desktop updated successfully", type: "success" });
      closeModal();
      refresh();
    } catch (err) {
      const errorMsg = err instanceof Error ? err.message : "Failed to update desktop";
      setError(errorMsg);
      setToast({ message: errorMsg, type: "error" });
    } finally {
      setLoading(false);
    }
  };

  const handleDisable = async (row: DesktopRow) => {
    if (!token) return;
    setLoading(true);
    setError(null);
    try {
      await adminApi.updateDesktop(token, row.desktopAppId, { status: "Disabled" });
      setToast({ message: "Desktop disabled successfully", type: "success" });
      refresh();
    } catch (err) {
      const errorMsg = err instanceof Error ? err.message : "Failed to disable desktop";
      setError(errorMsg);
      setToast({ message: errorMsg, type: "error" });
    } finally {
      setLoading(false);
    }
  };

  const handleEnable = async (row: DesktopRow) => {
    if (!token) return;
    setLoading(true);
    setError(null);
    try {
      await adminApi.updateDesktop(token, row.desktopAppId, { status: "Active" });
      setToast({ message: "Desktop enabled successfully", type: "success" });
      refresh();
    } catch (err) {
      const errorMsg = err instanceof Error ? err.message : "Failed to enable desktop";
      setError(errorMsg);
      setToast({ message: errorMsg, type: "error" });
    } finally {
      setLoading(false);
    }
  };

  const handleApprove = async (row: DesktopRow) => {
    if (!token) return;
    setLoading(true);
    setError(null);
    try {
      await adminApi.approveDesktop(token, row.desktopAppId, {
        requiredApprovalsN: row.requiredApprovalsN,
        unlockMinutes: row.unlockMinutes,
      });
      setToast({ message: "Desktop approved successfully", type: "success" });
      refresh();
    } catch (err) {
      const errorMsg = err instanceof Error ? err.message : "Failed to approve desktop";
      setError(errorMsg);
      setToast({ message: errorMsg, type: "error" });
    } finally {
      setLoading(false);
    }
  };

  const handleReject = async (row: DesktopRow) => {
    if (!token) return;
    setLoading(true);
    setError(null);
    try {
      await adminApi.rejectDesktop(token, row.desktopAppId);
      setToast({ message: "Desktop rejected successfully", type: "success" });
      refresh();
    } catch (err) {
      const errorMsg = err instanceof Error ? err.message : "Failed to reject desktop";
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
        {enableRegister && (
          <Button size="sm" onClick={openCreate}>
            + Register Desktop
          </Button>
        )}
        <div className="spacer" />
        <input className="input" placeholder="Search by name / DesktopAppId" value={search} onChange={(e) => setSearch(e.target.value)} />
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
            <Th>N approvals</Th>
            <Th>Unlock minutes</Th>
            <Th>Status</Th>
            <Th>Actions</Th>
          </tr>
        </thead>
        <tbody>
          {filtered.map((d) => (
            <tr key={d.desktopAppId}>
              <Td>
                <div className="strong">{d.nameLabel || "Unlabeled"}</div>
                <div className="muted small">{d.lastSeenAtUtc ? `Last seen: ${new Date(d.lastSeenAtUtc).toLocaleString()}` : "—"}</div>
              </Td>
              <Td className="mono">{d.desktopAppId}</Td>
              <Td>{d.requiredApprovalsN}</Td>
              <Td>{d.unlockMinutes}</Td>
              <Td>
                <Badge tone={d.status === "Active" ? "good" : d.status === "Pending" ? "warn" : "bad"}>{d.status}</Badge>
              </Td>
              <Td>
                <div className="row" style={{ justifyContent: "flex-end" }}>
                  {d.status === "Pending" && (
                    <>
                      <Button size="sm" onClick={() => handleApprove(d)}>
                        Approve
                      </Button>
                      <Button size="sm" variant="danger" onClick={() => handleReject(d)}>
                        Reject
                      </Button>
                    </>
                  )}
                  {d.status === "Active" && (
                    <>
                      <Button size="sm" variant="ghost" onClick={() => openEdit(d)}>
                        Edit
                      </Button>
                      <Button size="sm" variant="danger" onClick={() => handleDisable(d)}>
                        Disable
                      </Button>
                    </>
                  )}
                  {d.status === "Disabled" && (
                    <>
                      <Button size="sm" variant="ghost" onClick={() => openEdit(d)}>
                        Edit
                      </Button>
                      <Button size="sm" onClick={() => handleEnable(d)}>
                        Enable
                      </Button>
                    </>
                  )}
                </div>
              </Td>
            </tr>
          ))}
        </tbody>
      </Table>
      <p className="muted">
        TokenControl first run calls <span className="mono">POST /api/desktop/register</span>. SuperAdmin can approve (activate) or
        reject/disable registrations, and edit per-desktop N and unlock minutes. Manual register button is hidden unless
        `VITE_ENABLE_DESKTOP_REGISTER=true`.
      </p>

      {modalMode && (
        <div className="modal">
          <div className="modal-card">
            <div className="hd">
              <h3>{modalMode === "create" ? "Register Desktop" : `Edit ${selected?.desktopAppId}`}</h3>
              <Button variant="ghost" size="sm" onClick={closeModal}>
                Close
              </Button>
            </div>
            <div className="bd stack">
              {modalMode === "create" && (
                <label className="field">
                  <div className="field-label">DesktopAppId (GUID)</div>
                  <input
                    className="input"
                    value={form.desktopAppId}
                    onChange={(e) => setForm({ ...form, desktopAppId: e.target.value })}
                    placeholder="52a3f9d2-...."
                  />
                </label>
              )}
              <label className="field">
                <div className="field-label">Name label</div>
                <input className="input" value={form.nameLabel} onChange={(e) => setForm({ ...form, nameLabel: e.target.value })} />
              </label>
              <label className="field">
                <div className="field-label">Required approvals (N)</div>
                <input
                  className="input"
                  type="number"
                  min={1}
                  value={form.requiredApprovalsN}
                  onChange={(e) => setForm({ ...form, requiredApprovalsN: Number(e.target.value) })}
                />
              </label>
              <label className="field">
                <div className="field-label">Unlock duration (minutes)</div>
                <input
                  className="input"
                  type="number"
                  min={1}
                  value={form.unlockMinutes}
                  onChange={(e) => setForm({ ...form, unlockMinutes: Number(e.target.value) })}
                />
              </label>
              <Button onClick={modalMode === "create" ? handleCreate : handleSaveEdit} disabled={loading}>
                Save
              </Button>
            </div>
          </div>
        </div>
      )}
      </div>
    </>
  );
}

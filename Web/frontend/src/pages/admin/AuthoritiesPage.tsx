import { useEffect, useMemo, useState } from "react";

import * as adminApi from "../../api/admin";
import Badge from "../../components/Badge";
import Button from "../../components/Button";
import { Table, Td, Th } from "../../components/Table";
import Toast from "../../components/Toast";
import { useAuth } from "../../auth/useAuth";

type ModalMode = "add" | "edit" | null;

export default function AuthoritiesPage() {
  const { token } = useAuth();
  const [users, setUsers] = useState<adminApi.User[]>([]);
  const [desktops, setDesktops] = useState<adminApi.Desktop[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState("");
  const [toast, setToast] = useState<{ message: string; type: "success" | "error" } | null>(null);

  const [modalMode, setModalMode] = useState<ModalMode>(null);
  const [selectedUser, setSelectedUser] = useState<adminApi.User | null>(null);

  const [form, setForm] = useState({
    email: "",
    password: "",
    role: "GovernanceAuthority" as adminApi.User["role"],
    phone: "",
    mfa_secret: "",
    is_active: true,
  });

  const filteredUsers = useMemo(() => {
    if (!search) return users;
    const q = search.toLowerCase();
    return users.filter((u) => u.email.toLowerCase().includes(q));
  }, [users, search]);

  const refresh = async () => {
    if (!token) return;
    setLoading(true);
    setError(null);
    try {
      const [u, d] = await Promise.all([adminApi.listUsers(token), adminApi.listDesktops(token)]);
      setUsers(u);
      setDesktops(d);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to load data");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    refresh();
  }, [token]);

  const openAdd = () => {
    setForm({ email: "", password: "", role: "GovernanceAuthority", phone: "", mfa_secret: "", is_active: true });
    setSelectedUser(null);
    setModalMode("add");
  };

  const openEdit = (user: adminApi.User) => {
    setSelectedUser(user);
    setForm({ email: user.email, password: "", role: user.role, phone: user.phone || "", mfa_secret: "", is_active: user.is_active });
    setModalMode("edit");
  };

  const closeModal = () => {
    setModalMode(null);
    setSelectedUser(null);
  };

  const handleSaveAdd = async () => {
    if (!token) return;
    setLoading(true);
    setError(null);
    try {
      await adminApi.createUser(token, {
        email: form.email,
        password: form.password,
        role: form.role,
        phone: form.phone || undefined,
        mfa_secret: form.mfa_secret,
      });
      setToast({ message: "User created successfully", type: "success" });
      closeModal();
      refresh();
    } catch (err) {
      const errorMsg = err instanceof Error ? err.message : "Failed to create user";
      setError(errorMsg);
      setToast({ message: errorMsg, type: "error" });
    } finally {
      setLoading(false);
    }
  };

  const handleSaveEdit = async () => {
    if (!token || !selectedUser) return;
    setLoading(true);
    setError(null);
    try {
      await adminApi.updateUser(token, selectedUser.id, {
        role: form.role,
        phone: form.phone || undefined,
        is_active: form.is_active,
        ...(form.password ? { password: form.password } : {}),
        ...(form.mfa_secret ? { mfa_secret: form.mfa_secret } : {}),
      });
      setToast({ message: "User updated successfully", type: "success" });
      closeModal();
      refresh();
    } catch (err) {
      const errorMsg = err instanceof Error ? err.message : "Failed to update user";
      setError(errorMsg);
      setToast({ message: errorMsg, type: "error" });
    } finally {
      setLoading(false);
    }
  };

  const handleDisable = async (user: adminApi.User) => {
    if (!token) return;
    setLoading(true);
    setError(null);
    try {
      await adminApi.updateUser(token, user.id, { is_active: false });
      setToast({ message: "User disabled successfully", type: "success" });
      refresh();
    } catch (err) {
      const errorMsg = err instanceof Error ? err.message : "Failed to disable user";
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
        <Button size="sm" onClick={openAdd}>
          + Add Authority
        </Button>
        <div className="spacer" />
        <input className="input" placeholder="Search authorities..." value={search} onChange={(e) => setSearch(e.target.value)} />
        <Button size="sm" variant="ghost" onClick={refresh} disabled={loading}>
          Refresh
        </Button>
      </div>
      {error && <div className="status" style={{ background: "#fee2e2", borderColor: "#fecdd3", color: "#991b1b" }}>{error}</div>}
      <Table>
        <thead>
          <tr>
            <Th>Email</Th>
            <Th>Role</Th>
            <Th>Status</Th>
            <Th>Actions</Th>
          </tr>
        </thead>
        <tbody>
          {filteredUsers.map((u) => (
            <tr key={u.id}>
              <Td>{u.email}</Td>
              <Td>{u.role}</Td>
              <Td>
                <Badge tone={u.is_active ? "good" : "warn"}>{u.is_active ? "Active" : "Disabled"}</Badge>
              </Td>
              <Td>
                <div className="row" style={{ justifyContent: "flex-end" }}>
                  <Button size="sm" variant="ghost" onClick={() => openEdit(u)}>
                    Edit
                  </Button>
                  <Button size="sm" variant="danger" onClick={() => handleDisable(u)} disabled={!u.is_active}>
                    Disable
                  </Button>
                </div>
              </Td>
            </tr>
          ))}
        </tbody>
      </Table>
      <p className="muted">CRUD governance users and assign the desktops they can approve.</p>

      {modalMode && (
        <div className="modal">
          <div className="modal-card">
            <div className="hd">
              <h3>
                {modalMode === "add" && "Add Authority"}
                {modalMode === "edit" && "Edit Authority"}
              </h3>
              <Button variant="ghost" size="sm" onClick={closeModal}>
                Close
              </Button>
            </div>
            <div className="bd stack">
              {modalMode === "add" && (
                <>
                  <label className="field">
                    <div className="field-label">Email</div>
                    <input className="input" value={form.email} onChange={(e) => setForm({ ...form, email: e.target.value })} />
                  </label>
                  <label className="field">
                    <div className="field-label">Password</div>
                    <input
                      className="input"
                      type="password"
                      value={form.password}
                      onChange={(e) => setForm({ ...form, password: e.target.value })}
                    />
                  </label>
                  <label className="field">
                    <div className="field-label">Phone (optional)</div>
                    <input
                      className="input"
                      type="tel"
                      value={form.phone}
                      onChange={(e) => setForm({ ...form, phone: e.target.value })}
                      placeholder="+1234567890"
                    />
                  </label>
                  <label className="field">
                    <div className="field-label">Role</div>
                    <select className="input" value={form.role} onChange={(e) => setForm({ ...form, role: e.target.value as any })}>
                      <option value="GovernanceAuthority">GovernanceAuthority</option>
                      <option value="SuperAdmin">SuperAdmin</option>
                    </select>
                  </label>
                  <Button onClick={handleSaveAdd} disabled={loading}>
                    Save
                  </Button>
                </>
              )}

              {modalMode === "edit" && (
                <>
                  <label className="field">
                    <div className="field-label">Role</div>
                    <select className="input" value={form.role} onChange={(e) => setForm({ ...form, role: e.target.value as any })}>
                      <option value="GovernanceAuthority">GovernanceAuthority</option>
                      <option value="SuperAdmin">SuperAdmin</option>
                    </select>
                  </label>
                  <label className="field">
                    <div className="field-label">Password (optional)</div>
                    <input
                      className="input"
                      type="password"
                      value={form.password}
                      onChange={(e) => setForm({ ...form, password: e.target.value })}
                      placeholder="Leave blank to keep existing"
                    />
                  </label>
                  <label className="field">
                    <div className="field-label">Phone (optional)</div>
                    <input
                      className="input"
                      type="tel"
                      value={form.phone}
                      onChange={(e) => setForm({ ...form, phone: e.target.value })}
                      placeholder="+1234567890"
                    />
                  </label>
                  <label className="field">
                    <div className="field-label">MFA Secret (optional Base32)</div>
                    <input
                      className="input"
                      value={form.mfa_secret}
                      onChange={(e) => setForm({ ...form, mfa_secret: e.target.value })}
                      placeholder="Leave blank to keep existing"
                    />
                  </label>
                  <label className="field">
                    <div className="field-label">Status</div>
                    <select
                      className="input"
                      value={form.is_active ? "Active" : "Disabled"}
                      onChange={(e) => setForm({ ...form, is_active: e.target.value === "Active" })}
                    >
                      <option value="Active">Active</option>
                      <option value="Disabled">Disabled</option>
                    </select>
                  </label>
                  <Button onClick={handleSaveEdit} disabled={loading}>
                    Save
                  </Button>
                </>
              )}
            </div>
          </div>
        </div>
      )}
      </div>
    </>
  );
}

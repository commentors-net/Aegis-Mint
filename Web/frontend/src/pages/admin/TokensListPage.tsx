import { useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";

import * as sharesApi from "../../api/shares";
import Badge from "../../components/Badge";
import Button from "../../components/Button";
import { Table, Td, Th } from "../../components/Table";
import Toast from "../../components/Toast";
import { useAuth } from "../../auth/useAuth";

export default function TokensListPage() {
  const { token } = useAuth();
  const navigate = useNavigate();
  const [tokens, setTokens] = useState<sharesApi.TokenDeployment[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState("");
  const [filterUploaded, setFilterUploaded] = useState<"all" | "uploaded" | "pending">("all");
  const [toast, setToast] = useState<{ message: string; type: "success" | "error" } | null>(null);
  
  // Accordion state
  const [expandedTokens, setExpandedTokens] = useState<Set<string>>(new Set());
  const [tokenUsers, setTokenUsers] = useState<Record<string, sharesApi.TokenShareUser[]>>({});
  const [usersLoading, setUsersLoading] = useState<Set<string>>(new Set());
  
  // User modal state
  const [userModal, setUserModal] = useState<{
    mode: "add" | "edit" | null;
    tokenId: string | null;
    user: sharesApi.TokenShareUser | null;
  }>({ mode: null, tokenId: null, user: null });
  
  const [userForm, setUserForm] = useState({
    name: "",
    email: "",
    phone: "",
    password: "",
  });
  
  const [showPassword, setShowPassword] = useState(false);

  const filteredTokens = useMemo(() => {
    let result = tokens;

    // Filter by upload status
    if (filterUploaded === "uploaded") {
      result = result.filter((t) => t.shares_uploaded);
    } else if (filterUploaded === "pending") {
      result = result.filter((t) => !t.shares_uploaded);
    }

    // Filter by search
    if (search) {
      const q = search.toLowerCase();
      result = result.filter(
        (t) =>
          t.token_name.toLowerCase().includes(q) ||
          t.token_symbol.toLowerCase().includes(q) ||
          t.contract_address.toLowerCase().includes(q)
      );
    }

    return result;
  }, [tokens, search, filterUploaded]);

  const refresh = async () => {
    if (!token) return;
    setLoading(true);
    setError(null);
    try {
      const data = await sharesApi.listTokenDeployments(token);
      setTokens(data.sort((a, b) => new Date(b.created_at_utc).getTime() - new Date(a.created_at_utc).getTime()));
    } catch (err) {
      const errorMsg = err instanceof Error ? err.message : "Failed to load tokens";
      setError(errorMsg);
      setToast({ message: errorMsg, type: "error" });
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    refresh();
  }, [token]);

  const toggleExpand = async (tokenId: string) => {
    const newExpanded = new Set(expandedTokens);
    if (newExpanded.has(tokenId)) {
      newExpanded.delete(tokenId);
    } else {
      newExpanded.add(tokenId);
      // Load users if not already loaded
      if (!tokenUsers[tokenId]) {
        await loadUsers(tokenId);
      }
    }
    setExpandedTokens(newExpanded);
  };

  const loadUsers = async (tokenId: string) => {
    if (!token) return;
    const newLoading = new Set(usersLoading);
    newLoading.add(tokenId);
    setUsersLoading(newLoading);
    
    try {
      const users = await sharesApi.getTokenShareUsers(token, tokenId);
      setTokenUsers(prev => ({ ...prev, [tokenId]: users }));
    } catch (err) {
      setToast({ message: "Failed to load users", type: "error" });
    } finally {
      const newLoading = new Set(usersLoading);
      newLoading.delete(tokenId);
      setUsersLoading(newLoading);
    }
  };

  const openAddUser = (tokenId: string) => {
    setUserForm({ name: "", email: "", phone: "", password: "" });
    setUserModal({ mode: "add", tokenId, user: null });
  };

  const openEditUser = (tokenId: string, user: sharesApi.TokenShareUser) => {
    setUserForm({ name: user.name, email: user.email, phone: user.phone || "", password: "" });
    setUserModal({ mode: "edit", tokenId, user });
  };

  const closeUserModal = () => {
    setUserModal({ mode: null, tokenId: null, user: null });
  };

  const handleSaveUser = async () => {
    if (!token || !userModal.tokenId) return;
    
    try {
      if (userModal.mode === "add") {
        // Check if email already exists for other tokens
        console.log("Checking email:", userForm.email, "for token:", userModal.tokenId);
        const checkResponse = await fetch(
          `${import.meta.env.VITE_API_BASE_URL || ""}/api/token-share-users/check-email/${encodeURIComponent(userForm.email)}?token_deployment_id=${userModal.tokenId}`,
          {
            headers: { Authorization: `Bearer ${token}` }
          }
        );
        
        console.log("Check response status:", checkResponse.status);
        
        if (checkResponse.ok) {
          const checkData = await checkResponse.json();
          console.log("Check data:", checkData);
          if (checkData.exists && checkData.tokens.length > 0) {
            const tokenNames = checkData.tokens.map((t: any) => t.token_name).join(", ");
            const confirmed = confirm(
              `This email is already registered for: ${tokenNames}\n\n` +
              `Do you want to assign the same user to this token as well?`
            );
            if (!confirmed) {
              console.log("User canceled adding duplicate email");
              return; // User canceled
            }
            console.log("User confirmed adding duplicate email");
          } else {
            console.log("Email is unique, proceeding with creation");
          }
        } else {
          console.error("Check email endpoint failed:", checkResponse.status);
        }
        
        await sharesApi.createTokenShareUser(token, {
          token_deployment_id: userModal.tokenId,
          name: userForm.name,
          email: userForm.email,
          phone: userForm.phone || undefined,
          password: userForm.password,
        });
        setToast({ message: "User created successfully", type: "success" });
      } else if (userModal.mode === "edit" && userModal.user) {
        await sharesApi.updateTokenShareUser(token, userModal.user.id, {
          name: userForm.name,
          email: userForm.email,
          phone: userForm.phone || undefined,
          password: userForm.password || undefined,
        });
        setToast({ message: "User updated successfully", type: "success" });
      }
      
      closeUserModal();
      await loadUsers(userModal.tokenId);
    } catch (err) {
      const errorMsg = err instanceof Error ? err.message : "Failed to save user";
      setToast({ message: errorMsg, type: "error" });
    }
  };

  const handleDeleteUser = async (tokenId: string, userId: string) => {
    if (!token || !confirm("Are you sure you want to delete this user?")) return;
    
    try {
      await sharesApi.deleteTokenShareUser(token, userId);
      setToast({ message: "User deleted successfully", type: "success" });
      await loadUsers(tokenId);
    } catch (err) {
      const errorMsg = err instanceof Error ? err.message : "Failed to delete user";
      setToast({ message: errorMsg, type: "error" });
    }
  };

  const handleViewShares = (tokenId: string) => {
    navigate(`/admin/tokens/${tokenId}/shares`);
  };

  const formatDate = (dateString: string) => {
    const date = new Date(dateString);
    return date.toLocaleString("en-US", {
      year: "numeric",
      month: "short",
      day: "numeric",
      hour: "2-digit",
      minute: "2-digit",
    });
  };

  return (
    <div className="p-6">
      {toast && (
        <Toast
          message={toast.message}
          type={toast.type}
          onClose={() => setToast(null)}
        />
      )}

      <div className="mb-6">
        <h1 className="text-2xl font-bold text-gray-900">Token Deployments</h1>
        <p className="mt-2 text-sm text-gray-600">
          Manage tokens, their users, and share assignments. Expand each token to manage its users.
        </p>
      </div>

      {/* Filters - Updated to match other pages */}
      <div className="stack">
        <div className="row">
          <div className="spacer" />
          <select
            className="input"
            value={filterUploaded}
            onChange={(e) => setFilterUploaded(e.target.value as any)}
            style={{ width: "auto", minWidth: "150px" }}
          >
            <option value="all" style={{ color: "#111827", backgroundColor: "#ffffff" }}>All Tokens</option>
            <option value="uploaded" style={{ color: "#111827", backgroundColor: "#ffffff" }}>Shares Uploaded</option>
            <option value="pending" style={{ color: "#111827", backgroundColor: "#ffffff" }}>Upload Pending</option>
          </select>
          <input
            className="input"
            placeholder="Search tokens..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            style={{ minWidth: "250px" }}
          />
          <Button size="sm" variant="ghost" onClick={refresh} disabled={loading}>
            Refresh
          </Button>
        </div>
        {error && (
          <div className="status" style={{ background: "#fee2e2", borderColor: "#fecdd3", color: "#991b1b" }}>
            {error}
          </div>
        )}
      </div>

      {/* Tokens List with Accordion */}
      <div className="overflow-x-auto rounded-lg border border-gray-200 bg-white shadow">
        <Table>
          <thead className="bg-gray-50">
            <tr>
              <Th style={{ width: "40px" }}></Th>
              <Th>Token Name</Th>
              <Th>Symbol</Th>
              <Th>Network</Th>
              <Th>Contract Address</Th>
              <Th>Shares Status</Th>
              <Th>Created</Th>
              <Th>Actions</Th>
            </tr>
          </thead>
          <tbody>
            {loading && (
              <tr>
                <td colSpan={8} className="text-center py-4 text-gray-500">
                  Loading tokens...
                </td>
              </tr>
            )}

            {!loading && filteredTokens.length === 0 && (
              <tr>
                <td colSpan={8} className="text-center py-4 text-gray-500">
                  {search || filterUploaded !== "all"
                    ? "No tokens match your filters"
                    : "No token deployments found"}
                </td>
              </tr>
            )}

            {!loading &&
              filteredTokens.map((tok) => {
                const isExpanded = expandedTokens.has(tok.id);
                const users = tokenUsers[tok.id] || [];
                const usersAreLoading = usersLoading.has(tok.id);

                return (
                  <>
                    {/* Token Row */}
                    <tr key={tok.id} className="hover:bg-gray-50 border-b border-gray-200">
                      <Td>
                        <button
                          onClick={() => toggleExpand(tok.id)}
                          className="flex items-center justify-center w-6 h-6 text-gray-600 hover:text-gray-900 hover:bg-gray-100 rounded"
                          title={isExpanded ? "Collapse" : "Expand users"}
                        >
                          {isExpanded ? "−" : "+"}
                        </button>
                      </Td>
                      <Td className="font-medium text-gray-900">{tok.token_name}</Td>
                      <Td>
                        <Badge tone="neutral">{tok.token_symbol}</Badge>
                      </Td>
                      <Td>
                        <Badge tone="neutral">{tok.network}</Badge>
                      </Td>
                      <Td className="text-sm text-gray-600">
                        <span title={tok.contract_address}>
                          {tok.contract_address.slice(0, 6)}...{tok.contract_address.slice(-4)}
                        </span>
                      </Td>
                      <Td>
                        {tok.shares_uploaded ? (
                          <div className="flex flex-col gap-1">
                            <Badge tone="good">
                              ✓ {tok.shares_uploaded_count} share{tok.shares_uploaded_count !== 1 ? "s" : ""}
                            </Badge>
                            {tok.shares_uploaded_at && (
                              <span className="text-xs text-gray-500">
                                {formatDate(tok.shares_uploaded_at)}
                              </span>
                            )}
                          </div>
                        ) : (
                          <Badge tone="warn">⏳ Pending</Badge>
                        )}
                      </Td>
                      <Td className="text-sm text-gray-600">{formatDate(tok.created_at_utc)}</Td>
                      <Td>
                        <Button
                          onClick={() => handleViewShares(tok.id)}
                          size="sm"
                          variant={tok.shares_uploaded ? "primary" : "secondary"}
                        >
                          {tok.shares_uploaded ? "Manage Shares" : "Upload Shares"}
                        </Button>
                      </Td>
                    </tr>

                    {/* Expanded Users Section */}
                    {isExpanded && (
                      <tr>
                        <td colSpan={8} className="bg-gray-50 p-0 border-b border-gray-200">
                          <div className="p-4">
                            <div className="mb-3 flex items-center justify-between">
                              <h4 className="text-sm font-semibold text-gray-700">
                                Users for {tok.token_name} ({users.length})
                              </h4>
                              <Button onClick={() => openAddUser(tok.id)} size="sm" variant="primary">
                                + Add User
                              </Button>
                            </div>

                            {usersAreLoading && (
                              <div className="text-center text-sm text-gray-500 py-4">Loading users...</div>
                            )}

                            {!usersAreLoading && users.length === 0 && (
                              <div className="text-center text-sm text-gray-500 py-4">
                                No users added yet. Click "Add User" to create one.
                              </div>
                            )}

                            {!usersAreLoading && users.length > 0 && (
                              <div className="overflow-x-auto border border-gray-200 rounded">
                                <table className="min-w-full divide-y divide-gray-200 bg-white">
                                  <thead className="bg-gray-100">
                                    <tr>
                                      <th className="px-4 py-2 text-left text-xs font-semibold text-gray-700 uppercase tracking-wider">
                                        Name
                                      </th>
                                      <th className="px-4 py-2 text-left text-xs font-semibold text-gray-700 uppercase tracking-wider">
                                        Email
                                      </th>
                                      <th className="px-4 py-2 text-left text-xs font-semibold text-gray-700 uppercase tracking-wider">
                                        Phone
                                      </th>
                                      <th className="px-4 py-2 text-left text-xs font-semibold text-gray-700 uppercase tracking-wider">
                                        Created
                                      </th>
                                      <th className="px-4 py-2 text-right text-xs font-semibold text-gray-700 uppercase tracking-wider">
                                        Actions
                                      </th>
                                    </tr>
                                  </thead>
                                  <tbody className="divide-y divide-gray-200 bg-white">
                                    {users.map((user) => (
                                      <tr key={user.id} className="hover:bg-gray-50">
                                        <td className="px-4 py-3 text-sm text-gray-900">{user.name}</td>
                                        <td className="px-4 py-3 text-sm text-gray-600">{user.email}</td>
                                        <td className="px-4 py-3 text-sm text-gray-600">{user.phone || "—"}</td>
                                        <td className="px-4 py-3 text-sm text-gray-600">
                                          {formatDate(user.created_at_utc)}
                                        </td>
                                        <td className="px-4 py-3 text-right">
                                          <div className="flex justify-end gap-2">
                                            <Button
                                              onClick={() => openEditUser(tok.id, user)}
                                              size="sm"
                                              variant="secondary"
                                            >
                                              Edit
                                            </Button>
                                            <Button
                                              onClick={() => handleDeleteUser(tok.id, user.id)}
                                              size="sm"
                                              variant="danger"
                                            >
                                              Delete
                                            </Button>
                                          </div>
                                        </td>
                                      </tr>
                                    ))}
                                  </tbody>
                                </table>
                              </div>
                            )}
                          </div>
                        </td>
                      </tr>
                    )}
                  </>
                );
              })}
          </tbody>
        </Table>
      </div>

      {/* Summary Stats */}
      {!loading && tokens.length > 0 && (
        <div className="mt-4 flex gap-4 text-sm text-gray-600">
          <span>
            <strong>{tokens.length}</strong> total token{tokens.length !== 1 ? "s" : ""}
          </span>
          <span>•</span>
          <span>
            <strong>{tokens.filter((t) => t.shares_uploaded).length}</strong> with shares uploaded
          </span>
          <span>•</span>
          <span>
            <strong>{tokens.filter((t) => !t.shares_uploaded).length}</strong> pending upload
          </span>
        </div>
      )}

      {/* User Modal */}
      {userModal.mode && (
        <div className="modal">
          <div className="modal-card">
            <div className="hd">
              <h3>
                {userModal.mode === "add" && "Add User"}
                {userModal.mode === "edit" && "Edit User"}
              </h3>
              <Button variant="ghost" size="sm" onClick={closeUserModal}>
                Close
              </Button>
            </div>
            <div className="bd stack">
              <label className="field">
                <div className="field-label">Name</div>
                <input
                  className="input"
                  value={userForm.name}
                  onChange={(e) => setUserForm({ ...userForm, name: e.target.value })}
                  placeholder="John Doe"
                />
              </label>
              <label className="field">
                <div className="field-label">Email</div>
                <input
                  className="input"
                  type="email"
                  value={userForm.email}
                  onChange={(e) => setUserForm({ ...userForm, email: e.target.value })}
                  placeholder="john@example.com"
                />
                <p className="mt-1 text-xs text-gray-600">
                  ℹ️ Same email can be used across different tokens. Email must be unique within each token.
                </p>
              </label>
              <label className="field">
                <div className="field-label">Phone (optional)</div>
                <input
                  className="input"
                  type="tel"
                  value={userForm.phone}
                  onChange={(e) => setUserForm({ ...userForm, phone: e.target.value })}
                  placeholder="+1234567890"
                />
              </label>
              <label className="field">
                <div className="field-label">Password{userModal.mode === "edit" && " (leave blank to keep current)"}</div>
                <div style={{ position: "relative" }}>
                  <input
                    className="input"
                    type={showPassword ? "text" : "password"}
                    value={userForm.password}
                    onChange={(e) => setUserForm({ ...userForm, password: e.target.value })}
                    placeholder="Enter password"
                    style={{ paddingRight: "40px" }}
                  />
                  <button
                    type="button"
                    onClick={() => setShowPassword(!showPassword)}
                    style={{
                      position: "absolute",
                      right: "8px",
                      top: "50%",
                      transform: "translateY(-50%)",
                      background: "none",
                      border: "none",
                      cursor: "pointer",
                      padding: "4px",
                      display: "flex",
                      alignItems: "center",
                      color: "#6b7280",
                    }}
                    title={showPassword ? "Hide password" : "Show password"}
                  >
                    {showPassword ? (
                      <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                        <path d="M17.94 17.94A10.07 10.07 0 0 1 12 20c-7 0-11-8-11-8a18.45 18.45 0 0 1 5.06-5.94M9.9 4.24A9.12 9.12 0 0 1 12 4c7 0 11 8 11 8a18.5 18.5 0 0 1-2.16 3.19m-6.72-1.07a3 3 0 1 1-4.24-4.24"></path>
                        <line x1="1" y1="1" x2="23" y2="23"></line>
                      </svg>
                    ) : (
                      <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                        <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"></path>
                        <circle cx="12" cy="12" r="3"></circle>
                      </svg>
                    )}
                  </button>
                </div>
              </label>
              <Button
                onClick={handleSaveUser}
                disabled={!userForm.name || !userForm.email || (userModal.mode === "add" && !userForm.password)}
              >
                {userModal.mode === "add" ? "Save" : "Update"}
              </Button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

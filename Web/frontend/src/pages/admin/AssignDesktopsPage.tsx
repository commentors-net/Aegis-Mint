import { useEffect, useMemo, useState } from "react";

import * as adminApi from "../../api/admin";
import Badge from "../../components/Badge";
import Button from "../../components/Button";
import { useAuth } from "../../auth/useAuth";

export default function AssignDesktopsPage() {
  const { token } = useAuth();
  const [desktops, setDesktops] = useState<adminApi.Desktop[]>([]);
  const [users, setUsers] = useState<adminApi.User[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState("");
  
  const [selectedDesktop, setSelectedDesktop] = useState<adminApi.Desktop | null>(null);
  const [assignedUsers, setAssignedUsers] = useState<string[]>([]);
  const [tempAssignments, setTempAssignments] = useState<string[]>([]);
  const [userSearch, setUserSearch] = useState("");
  const [saving, setSaving] = useState(false);

  const filteredDesktops = useMemo(() => {
    if (!search) return desktops;
    const q = search.toLowerCase();
    return desktops.filter((d) => 
      (d.nameLabel || "").toLowerCase().includes(q) || 
      d.desktopAppId.toLowerCase().includes(q)
    );
  }, [desktops, search]);

  const filteredUsers = useMemo(() => {
    if (!userSearch) return users;
    const q = userSearch.toLowerCase();
    return users.filter((u) => u.email.toLowerCase().includes(q));
  }, [users, userSearch]);

  const refresh = async () => {
    if (!token) return;
    setLoading(true);
    setError(null);
    try {
      const [d, u] = await Promise.all([
        adminApi.listDesktops(token),
        adminApi.listUsers(token),
      ]);
      setDesktops(d);
      setUsers(u);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to load data");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    refresh();
  }, [token]);

  const openDesktopModal = async (desktop: adminApi.Desktop) => {
    if (!token) return;
    setLoading(true);
    setError(null);
    try {
      // Get all users and check which ones are assigned to this desktop
      const allUsers = await adminApi.listUsers(token);
      const assigned: string[] = [];
      
      for (const user of allUsers) {
        try {
          const userAssignments = await adminApi.getUserAssignments(token, user.id);
          if (userAssignments.includes(desktop.desktopAppId)) {
            assigned.push(user.id);
          }
        } catch (err) {
          console.error(`Failed to get assignments for user ${user.email}`, err);
        }
      }
      
      setAssignedUsers(assigned);
      setTempAssignments(assigned);
      setSelectedDesktop(desktop);
      setUserSearch("");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to load assignments");
    } finally {
      setLoading(false);
    }
  };

  const closeModal = () => {
    setSelectedDesktop(null);
    setAssignedUsers([]);
    setTempAssignments([]);
    setUserSearch("");
  };

  const toggleUserAssignment = (userId: string) => {
    setTempAssignments((prev) =>
      prev.includes(userId) ? prev.filter((id) => id !== userId) : [...prev, userId]
    );
  };

  const handleSaveAssignments = async () => {
    if (!token || !selectedDesktop) return;
    
    setSaving(true);
    setError(null);
    
    try {
      // Find users that need to be updated
      const usersToUpdate = users.filter(
        (u) => tempAssignments.includes(u.id) !== assignedUsers.includes(u.id)
      );

      for (const user of usersToUpdate) {
        const currentAssignments = await adminApi.getUserAssignments(token, user.id);
        const shouldBeAssigned = tempAssignments.includes(user.id);
        
        let newAssignments: string[];
        if (shouldBeAssigned && !currentAssignments.includes(selectedDesktop.desktopAppId)) {
          newAssignments = [...currentAssignments, selectedDesktop.desktopAppId];
        } else if (!shouldBeAssigned && currentAssignments.includes(selectedDesktop.desktopAppId)) {
          newAssignments = currentAssignments.filter((id) => id !== selectedDesktop.desktopAppId);
        } else {
          continue;
        }
        
        await adminApi.setUserAssignments(token, user.id, newAssignments);
      }

      setAssignedUsers(tempAssignments);
      closeModal();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to save assignments");
    } finally {
      setSaving(false);
    }
  };

  const getAssignedUserCount = (desktopAppId: string) => {
    // This would need to be pre-loaded for efficiency
    // For now, we'll show a placeholder
    return "...";
  };

  const getDesktopStatusBadge = (desktop: adminApi.Desktop) => {
    switch (desktop.status) {
      case "Active":
        return <Badge tone="good">Active</Badge>;
      case "Pending":
        return <Badge tone="warn">Pending</Badge>;
      case "Disabled":
        return <Badge tone="bad">Disabled</Badge>;
      default:
        return <Badge>{desktop.status}</Badge>;
    }
  };

  return (
    <div>
      <div style={{ marginBottom: "1.5rem", display: "flex", gap: "1rem", alignItems: "center" }}>
        <input
          type="text"
          placeholder="Search desktops..."
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          style={{ flex: 1, padding: "0.5rem", borderRadius: "4px", border: "1px solid #ddd" }}
        />
        <Button onClick={refresh} disabled={loading}>
          {loading ? "Loading..." : "Refresh"}
        </Button>
      </div>

      {error && (
        <div style={{ padding: "1rem", marginBottom: "1rem", backgroundColor: "#fee", borderRadius: "4px", color: "#c00" }}>
          {error}
        </div>
      )}

      <div style={{ 
        display: "grid", 
        gridTemplateColumns: "repeat(auto-fill, minmax(280px, 1fr))", 
        gap: "1rem" 
      }}>
        {filteredDesktops.map((desktop) => (
          <div
            key={desktop.desktopAppId}
            style={{
              border: "1px solid #ddd",
              borderRadius: "8px",
              padding: "1.25rem",
              cursor: "pointer",
              transition: "all 0.2s",
              backgroundColor: "#fff",
            }}
            onClick={() => openDesktopModal(desktop)}
            onMouseEnter={(e) => {
              e.currentTarget.style.boxShadow = "0 4px 12px rgba(0,0,0,0.1)";
              e.currentTarget.style.transform = "translateY(-2px)";
            }}
            onMouseLeave={(e) => {
              e.currentTarget.style.boxShadow = "none";
              e.currentTarget.style.transform = "translateY(0)";
            }}
          >
            <div style={{ display: "flex", alignItems: "center", gap: "0.75rem", marginBottom: "0.75rem" }}>
              <div style={{ 
                width: "48px", 
                height: "48px", 
                borderRadius: "8px", 
                backgroundColor: "#f0f0f0",
                display: "flex",
                alignItems: "center",
                justifyContent: "center",
                fontSize: "24px"
              }}>
                💻
              </div>
              <div style={{ flex: 1, minWidth: 0 }}>
                <div style={{ 
                  fontWeight: 600, 
                  fontSize: "14px", 
                  overflow: "hidden", 
                  textOverflow: "ellipsis", 
                  whiteSpace: "nowrap" 
                }}>
                  {desktop.nameLabel || "Unnamed"}
                </div>
                <div style={{ 
                  fontSize: "11px", 
                  color: "#666", 
                  overflow: "hidden", 
                  textOverflow: "ellipsis", 
                  whiteSpace: "nowrap" 
                }}>
                  {desktop.desktopAppId.substring(0, 12)}...
                </div>
              </div>
            </div>
            
            <div style={{ marginBottom: "0.5rem" }}>
              {getDesktopStatusBadge(desktop)}
            </div>

            <div style={{ 
              fontSize: "12px", 
              color: "#666",
              padding: "0.5rem",
              backgroundColor: "#f8f9fa",
              borderRadius: "4px"
            }}>
              <div>Required Approvals: {desktop.requiredApprovalsN}</div>
              <div>Unlock Duration: {desktop.unlockMinutes} min</div>
            </div>
          </div>
        ))}
      </div>

      {filteredDesktops.length === 0 && !loading && (
        <div style={{ textAlign: "center", padding: "3rem", color: "#999" }}>
          {search ? "No desktops found matching your search" : "No desktops available"}
        </div>
      )}

      {/* Modal for assigning users */}
      {selectedDesktop && (
        <div
          style={{
            position: "fixed",
            inset: 0,
            backgroundColor: "rgba(0, 0, 0, 0.5)",
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            zIndex: 1000,
          }}
          onClick={closeModal}
        >
          <div
            style={{
              backgroundColor: "white",
              borderRadius: "8px",
              padding: "2rem",
              maxWidth: "600px",
              width: "90%",
              maxHeight: "80vh",
              overflow: "hidden",
              display: "flex",
              flexDirection: "column",
            }}
            onClick={(e) => e.stopPropagation()}
          >
            <div style={{ marginBottom: "1.5rem" }}>
              <h3 style={{ margin: 0, marginBottom: "0.5rem" }}>
                Assign Users to {selectedDesktop.nameLabel || "Desktop"}
              </h3>
              <p style={{ margin: 0, color: "#666", fontSize: "14px" }}>
                Select governance users who can approve access for this desktop
              </p>
            </div>

            <input
              type="text"
              placeholder="Search users..."
              value={userSearch}
              onChange={(e) => setUserSearch(e.target.value)}
              style={{
                padding: "0.5rem",
                borderRadius: "4px",
                border: "1px solid #ddd",
                marginBottom: "1rem",
              }}
            />

            <div style={{ 
              flex: 1, 
              overflow: "auto", 
              border: "1px solid #ddd", 
              borderRadius: "4px",
              marginBottom: "1.5rem"
            }}>
              {filteredUsers.length === 0 ? (
                <div style={{ padding: "2rem", textAlign: "center", color: "#999" }}>
                  {userSearch ? "No users found" : "No governance users available"}
                </div>
              ) : (
                filteredUsers.map((user) => (
                  <label
                    key={user.id}
                    style={{
                      display: "flex",
                      alignItems: "center",
                      padding: "0.75rem 1rem",
                      cursor: "pointer",
                      borderBottom: "1px solid #f0f0f0",
                      transition: "background-color 0.15s",
                    }}
                    onMouseEnter={(e) => {
                      e.currentTarget.style.backgroundColor = "#f8f9fa";
                    }}
                    onMouseLeave={(e) => {
                      e.currentTarget.style.backgroundColor = "transparent";
                    }}
                  >
                    <input
                      type="checkbox"
                      checked={tempAssignments.includes(user.id)}
                      onChange={() => toggleUserAssignment(user.id)}
                      style={{ marginRight: "0.75rem", cursor: "pointer" }}
                    />
                    <div style={{ flex: 1 }}>
                      <div style={{ fontWeight: 500, fontSize: "14px" }}>{user.email}</div>
                      <div style={{ fontSize: "12px", color: "#666" }}>
                        {user.role} • {user.is_active ? "Active" : "Inactive"}
                      </div>
                    </div>
                  </label>
                ))
              )}
            </div>

            <div style={{ display: "flex", gap: "0.75rem", justifyContent: "flex-end" }}>
              <Button variant="secondary" onClick={closeModal} disabled={saving}>
                Cancel
              </Button>
              <Button onClick={handleSaveAssignments} disabled={saving}>
                {saving ? "Saving..." : "Save Assignments"}
              </Button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

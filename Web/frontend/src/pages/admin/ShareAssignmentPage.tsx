import { useEffect, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";

import * as sharesApi from "../../api/shares";
import Badge from "../../components/Badge";
import Button from "../../components/Button";
import { Table, Td, Th } from "../../components/Table";
import Toast from "../../components/Toast";
import { useAuth } from "../../auth/useAuth";

export default function ShareAssignmentPage() {
  const { token } = useAuth();
  const { tokenId } = useParams<{ tokenId: string }>();
  const navigate = useNavigate();

  const [tokenInfo, setTokenInfo] = useState<sharesApi.TokenDeployment | null>(null);
  const [shares, setShares] = useState<sharesApi.ShareFile[]>([]);
  const [users, setUsers] = useState<sharesApi.User[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [toast, setToast] = useState<{ message: string; type: "success" | "error" } | null>(null);

  const [showAssignModal, setShowAssignModal] = useState(false);
  const [selectedShare, setSelectedShare] = useState<sharesApi.ShareFile | null>(null);
  const [assignUserId, setAssignUserId] = useState("");
  const [assignNotes, setAssignNotes] = useState("");

  const [showReEnableModal, setShowReEnableModal] = useState(false);
  const [reEnableShare, setReEnableShare] = useState<sharesApi.ShareFile | null>(null);

  const refresh = async () => {
    if (!token || !tokenId) return;
    setLoading(true);
    setError(null);
    try {
      const [tokensData, sharesData, usersData] = await Promise.all([
        sharesApi.listTokenDeployments(token),
        sharesApi.getTokenShares(token, tokenId),
        sharesApi.listUsers(token),
      ]);

      const tok = tokensData.find((t) => t.id === tokenId);
      setTokenInfo(tok || null);
      setShares(sharesData.sort((a, b) => a.share_number - b.share_number));
      setUsers(usersData.filter((u) => u.is_active && u.role === "GovernanceAuthority"));
    } catch (err) {
      const errorMsg = err instanceof Error ? err.message : "Failed to load data";
      setError(errorMsg);
      setToast({ message: errorMsg, type: "error" });
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    refresh();
  }, [token, tokenId]);

  const handleAssignClick = (share: sharesApi.ShareFile) => {
    if (share.is_assigned) {
      setToast({ message: "This share is already assigned", type: "error" });
      return;
    }
    setSelectedShare(share);
    setAssignUserId("");
    setAssignNotes("");
    setShowAssignModal(true);
  };

  const handleAssignSubmit = async () => {
    if (!token || !selectedShare || !assignUserId) return;
    setLoading(true);
    try {
      await sharesApi.createShareAssignment(token, {
        share_file_id: selectedShare.id,
        user_id: assignUserId,
        assignment_notes: assignNotes || undefined,
      });
      setToast({ message: "Share assigned successfully", type: "success" });
      setShowAssignModal(false);
      refresh();
    } catch (err) {
      const errorMsg = err instanceof Error ? err.message : "Failed to assign share";
      setToast({ message: errorMsg, type: "error" });
    } finally {
      setLoading(false);
    }
  };

  const handleUnassign = async (share: sharesApi.ShareFile) => {
    if (!token || !share.assigned_to) return;
    if (!confirm(`Unassign share #${share.share_number} from ${share.assigned_to.user_email}?`)) return;

    setLoading(true);
    try {
      await sharesApi.deleteShareAssignment(token, share.assigned_to.assignment_id);
      setToast({ message: "Share unassigned successfully", type: "success" });
      refresh();
    } catch (err) {
      const errorMsg = err instanceof Error ? err.message : "Failed to unassign share";
      setToast({ message: errorMsg, type: "error" });
    } finally {
      setLoading(false);
    }
  };

  const handleReEnableClick = (share: sharesApi.ShareFile) => {
    if (!share.is_assigned) return;
    setReEnableShare(share);
    setShowReEnableModal(true);
  };

  const handleReEnableSubmit = async () => {
    if (!token || !reEnableShare || !reEnableShare.assigned_to) return;
    setLoading(true);
    try {
      await sharesApi.updateShareAssignment(token, reEnableShare.assigned_to.assignment_id, {
        download_allowed: true,
      });
      setToast({ message: "Download re-enabled successfully", type: "success" });
      setShowReEnableModal(false);
      refresh();
    } catch (err) {
      const errorMsg = err instanceof Error ? err.message : "Failed to re-enable download";
      setToast({ message: errorMsg, type: "error" });
    } finally {
      setLoading(false);
    }
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

  const assignedCount = shares.filter((s) => s.is_assigned).length;
  const unassignedCount = shares.length - assignedCount;

  return (
    <div className="p-6">
      {toast && <Toast message={toast.message} type={toast.type} onClose={() => setToast(null)} />}

      {/* Header */}
      <div className="mb-6">
        <div className="mb-2 flex items-center gap-2">
          <Button onClick={() => navigate("/admin/tokens")} variant="secondary" size="sm">
            ← Back to Tokens
          </Button>
        </div>
        <h1 className="text-2xl font-bold text-gray-900">
          {tokenInfo ? `${tokenInfo.token_name} (${tokenInfo.token_symbol})` : "Loading..."}
        </h1>
        <p className="mt-2 text-sm text-gray-600">Manage share assignments for governance authority users</p>
      </div>

      {error && (
        <div className="mb-4 rounded-md bg-red-50 p-4 text-sm text-red-700">
          <strong>Error:</strong> {error}
        </div>
      )}

      {/* Summary Stats */}
      {!loading && shares.length > 0 && (
        <div className="mb-4 grid grid-cols-1 gap-4 sm:grid-cols-3">
          <div className="rounded-lg border border-gray-200 bg-white p-4 shadow-sm">
            <div className="text-2xl font-bold text-gray-900">{shares.length}</div>
            <div className="text-sm text-gray-600">Total Shares</div>
          </div>
          <div className="rounded-lg border border-gray-200 bg-white p-4 shadow-sm">
            <div className="text-2xl font-bold text-green-600">{assignedCount}</div>
            <div className="text-sm text-gray-600">Assigned</div>
          </div>
          <div className="rounded-lg border border-gray-200 bg-white p-4 shadow-sm">
            <div className="text-2xl font-bold text-orange-600">{unassignedCount}</div>
            <div className="text-sm text-gray-600">Unassigned</div>
          </div>
        </div>
      )}

      {/* Shares Table */}
      <div className="overflow-x-auto rounded-lg border border-gray-200 bg-white shadow">
        <Table>
          <thead className="bg-gray-50">
            <tr>
              <Th>Share #</Th>
              <Th>File Name</Th>
              <Th>Status</Th>
              <Th>Assigned To</Th>
              <Th>Download Status</Th>
              <Th>Actions</Th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-200">
            {loading && (
              <tr>
                <td colSpan={6} className="text-center">
                  Loading shares...
                </td>
              </tr>
            )}

            {!loading && shares.length === 0 && (
              <tr>
                <td colSpan={6} className="text-center text-gray-500">
                  No shares found for this token
                </td>
              </tr>
            )}

            {!loading &&
              shares.map((share) => (
                <tr key={share.id} className="hover:bg-gray-50">
                  <Td>{share.share_number}</Td>
                  <Td>{share.file_name}</Td>
                  <Td>
                    {share.is_assigned ? (
                      <Badge tone="good">✓ Assigned</Badge>
                    ) : (
                      <Badge tone="warn">⚪ Unassigned</Badge>
                    )}
                  </Td>
                  <Td>
                    {share.assigned_to ? (
                      <div className="text-sm">{share.assigned_to.user_email}</div>
                    ) : (
                      <span className="text-gray-400">—</span>
                    )}
                  </Td>
                  <Td>
                    {share.assigned_to ? (
                      <div className="flex flex-col gap-1">
                        <div className="flex items-center gap-2">
                          {share.assigned_to.download_allowed ? (
                            <Badge tone="good">✓ Allowed</Badge>
                          ) : (
                            <Badge tone="bad">✕ Disabled</Badge>
                          )}
                          <span className="text-xs text-gray-500">
                            {share.assigned_to.download_count}x downloaded
                          </span>
                        </div>
                      </div>
                    ) : (
                      <span className="text-gray-400">—</span>
                    )}
                  </Td>
                  <Td>
                    <div className="flex gap-2">
                      {!share.is_assigned ? (
                        <Button onClick={() => handleAssignClick(share)} size="sm" variant="primary">
                          Assign
                        </Button>
                      ) : (
                        <>
                          <Button onClick={() => handleUnassign(share)} size="sm" variant="secondary">
                            Unassign
                          </Button>
                          {share.assigned_to && !share.assigned_to.download_allowed && (
                            <Button onClick={() => handleReEnableClick(share)} size="sm" variant="primary">
                              Re-enable
                            </Button>
                          )}
                        </>
                      )}
                    </div>
                  </Td>
                </tr>
              ))}
          </tbody>
        </Table>
      </div>

      {/* Assign Modal */}
      {showAssignModal && selectedShare && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black bg-opacity-50">
          <div className="w-full max-w-md rounded-lg bg-white p-6 shadow-xl">
            <h2 className="mb-4 text-xl font-bold">Assign Share #{selectedShare.share_number}</h2>

            <div className="mb-4">
              <label className="mb-1 block text-sm font-medium text-gray-700">
                Assign to User <span className="text-red-500">*</span>
              </label>
              <select
                value={assignUserId}
                onChange={(e) => setAssignUserId(e.target.value)}
                className="w-full rounded-md border border-gray-300 px-3 py-2 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
                required
              >
                <option value="">Select a user...</option>
                {users.map((user) => (
                  <option key={user.id} value={user.id}>
                    {user.email}
                  </option>
                ))}
              </select>
            </div>

            <div className="mb-6">
              <label className="mb-1 block text-sm font-medium text-gray-700">Assignment Notes (Optional)</label>
              <textarea
                value={assignNotes}
                onChange={(e) => setAssignNotes(e.target.value)}
                rows={3}
                className="w-full rounded-md border border-gray-300 px-3 py-2 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
                placeholder="Add any notes about this assignment..."
              />
            </div>

            <div className="flex justify-end gap-2">
              <Button
                onClick={() => setShowAssignModal(false)}
                variant="secondary"
                disabled={loading}
              >
                Cancel
              </Button>
              <Button
                onClick={handleAssignSubmit}
                variant="primary"
                disabled={loading || !assignUserId}
              >
                {loading ? "Assigning..." : "Assign Share"}
              </Button>
            </div>
          </div>
        </div>
      )}

      {/* Re-enable Modal */}
      {showReEnableModal && reEnableShare && reEnableShare.assigned_to && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black bg-opacity-50">
          <div className="w-full max-w-md rounded-lg bg-white p-6 shadow-xl">
            <h2 className="mb-4 text-xl font-bold">Re-enable Download</h2>

            <div className="mb-6 text-sm text-gray-700">
              <p className="mb-2">
                You are about to re-enable download for share <strong>#{reEnableShare.share_number}</strong> assigned to:
              </p>
              <div className="rounded-md bg-gray-50 p-3">
                <p className="font-medium">{reEnableShare.assigned_to.user_email}</p>
                <p className="mt-1 text-xs text-gray-600">
                  Previously downloaded {reEnableShare.assigned_to.download_count} time(s)
                </p>
              </div>
              <p className="mt-3 text-yellow-700">
                ⚠️ This will allow the user to download the share again. Use this only if the user lost their file and needs
                to re-download.
              </p>
            </div>

            <div className="flex justify-end gap-2">
              <Button onClick={() => setShowReEnableModal(false)} variant="secondary" disabled={loading}>
                Cancel
              </Button>
              <Button onClick={handleReEnableSubmit} variant="primary" disabled={loading}>
                {loading ? "Enabling..." : "Re-enable Download"}
              </Button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

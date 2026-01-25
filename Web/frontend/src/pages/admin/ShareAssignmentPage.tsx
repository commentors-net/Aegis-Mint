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
  const [users, setUsers] = useState<sharesApi.TokenShareUser[]>([]);
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
        sharesApi.getTokenShareUsers(token, tokenId),
      ]);

      const tok = tokensData.find((t) => t.id === tokenId);
      setTokenInfo(tok || null);
      setShares(sharesData.sort((a, b) => a.share_number - b.share_number));
      setUsers(usersData);
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
      <div className="mb-6 rounded-2xl border border-gray-200 bg-white/90 p-6 shadow-sm backdrop-blur">
        <div className="flex flex-col gap-6 lg:flex-row lg:items-end lg:justify-between">
          <div>
            <div className="mb-4">
              <Button onClick={() => navigate("/admin/tokens")} variant="secondary" size="sm">
                ← Back to Tokens
              </Button>
            </div>
            <div className="flex flex-wrap items-center gap-3">
              <h1 className="text-3xl font-bold text-gray-900">
                {tokenInfo ? tokenInfo.token_name : "Loading..."}
              </h1>
              {tokenInfo && (
                <span className="rounded-full bg-gray-100 px-3 py-1 text-xs font-semibold uppercase tracking-wide text-gray-600">
                  {tokenInfo.token_symbol}
                </span>
              )}
            </div>
            <p className="mt-2 text-sm text-gray-600">
              Manage share assignments for token-specific users
            </p>
          </div>

          {!loading && (
            <div className="w-full rounded-xl border border-gray-200 bg-white px-4 py-3 shadow-sm lg:w-auto">
              <div className="space-y-2 text-sm">
                <div className="flex items-center justify-between gap-6">
                  <span className="font-medium text-gray-600">Total Shares</span>
                  <span className="text-xs text-gray-400">&nbsp;&nbsp;&nbsp;&nbsp;•&nbsp;&nbsp;&nbsp;&nbsp;</span>
                  <span className="font-semibold text-gray-900 tabular-nums">{shares.length}</span>
                </div>
                <div className="flex items-center justify-between gap-6">
                  <span className="font-medium text-gray-600">Assigned</span>
                  <span className="text-xs text-gray-400">&nbsp;&nbsp;•&nbsp;&nbsp;</span>
                  <span className="font-semibold text-green-600 tabular-nums">{assignedCount}</span>
                </div>
                <div className="flex items-center justify-between gap-6">
                  <span className="font-medium text-gray-600">Unassigned</span>
                  <span className="text-xs text-gray-400">&nbsp;&nbsp;&nbsp;&nbsp;•&nbsp;&nbsp;&nbsp;&nbsp;</span>
                  <span className="font-semibold text-orange-600 tabular-nums">{unassignedCount}</span>
                </div>
              </div>
            </div>
          )}
        </div>
      </div>

      {error && (
        <div className="mb-4 rounded-md bg-red-50 p-4 text-sm text-red-700">
          <strong>Error:</strong> {error}
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
                <td colSpan={6} className="text-center">
                  <div className="py-8 px-4">
                    <div className="text-gray-600 mb-3 text-lg font-medium">
                      No shares found for this token
                    </div>
                    <div className="text-gray-500 text-sm mb-4 max-w-2xl mx-auto">
                      Shares are created when you mint a token using the <strong>AegisMint.Mint desktop application</strong>.
                      The desktop app automatically generates and uploads recovery shares during the minting process.
                    </div>
                    <div className="bg-blue-50 border border-blue-200 rounded-lg p-4 text-left max-w-2xl mx-auto">
                      <div className="font-semibold text-blue-900 mb-2">📋 To upload shares:</div>
                      <ol className="text-sm text-blue-800 space-y-1 list-decimal list-inside">
                        <li>Open the <strong>AegisMint.Mint</strong> desktop application</li>
                        <li>Click the <strong>"Mint Token"</strong> button to create a new token</li>
                        <li>Shares will be automatically generated and uploaded to this system</li>
                        <li>Return here to assign shares to users</li>
                      </ol>
                    </div>
                    <div className="mt-4">
                      <Button onClick={() => navigate('/admin/tokens')} variant="secondary" size="sm">
                        ← Back to Tokens List
                      </Button>
                    </div>
                  </div>
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
                      <span className="text-gray-500 text-sm">Not assigned yet</span>
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
        <div className="modal">
          <div className="modal-card">
            <div className="hd">
              <h3>Assign Share #{selectedShare.share_number}</h3>
              <Button variant="ghost" size="sm" onClick={() => setShowAssignModal(false)}>
                Close
              </Button>
            </div>
            <div className="bd stack">
              <label className="field">
                <div className="field-label">
                  Assign to User <span className="text-red-500">*</span>
                </div>
                <select
                  value={assignUserId}
                  onChange={(e) => setAssignUserId(e.target.value)}
                  className="input"
                  style={{ color: '#1f2937' }}
                  required
                >
                  <option value="" style={{ color: '#6b7280' }}>Select a user...</option>
                  {users.map((user) => (
                    <option key={user.id} value={user.id} style={{ color: '#1f2937' }}>
                      {user.name} ({user.email})
                    </option>
                  ))}
                </select>
                {users.length === 0 && (
                  <p className="mt-2 text-sm text-amber-600">
                    No users found for this token. Please add users in the Tokens page first.
                  </p>
                )}
              </label>

              <label className="field">
                <div className="field-label">Assignment Notes (Optional)</div>
                <textarea
                  value={assignNotes}
                  onChange={(e) => setAssignNotes(e.target.value)}
                  rows={3}
                  className="input"
                  placeholder="Add any notes about this assignment..."
                />
              </label>

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
        </div>
      )}

      {/* Re-enable Modal */}
      {showReEnableModal && reEnableShare && reEnableShare.assigned_to && (
        <div className="modal">
          <div className="modal-card">
            <div className="hd">
              <h3>Re-enable Download</h3>
              <Button variant="ghost" size="sm" onClick={() => setShowReEnableModal(false)}>
                Close
              </Button>
            </div>
            <div className="bd stack">
              <div className="text-sm text-gray-700">
                <p className="mb-3">
                  You are about to re-enable download for share <strong>#{reEnableShare.share_number}</strong> assigned to:
                </p>
                <div className="rounded-md bg-gray-50 p-3 border border-gray-200">
                  <p className="font-medium text-gray-900">{reEnableShare.assigned_to.user_email}</p>
                  <p className="mt-1 text-xs text-gray-600">
                    Previously downloaded {reEnableShare.assigned_to.download_count} time(s)
                  </p>
                </div>
                <div className="mt-4 rounded-md bg-yellow-50 border border-yellow-200 p-3">
                  <p className="text-sm text-yellow-800">
                    ⚠️ <strong>Warning:</strong> This will allow the user to download the share again. Use this only if the user lost their file and needs to re-download.
                  </p>
                </div>
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
        </div>
      )}
    </div>
  );
}

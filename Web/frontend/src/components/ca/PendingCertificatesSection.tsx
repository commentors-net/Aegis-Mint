import { useState, useEffect } from "react";
import { caApi, type PendingCertificate } from "../../api/ca";
import { useAuth } from "../../auth/useAuth";
import Button from "../../components/Button";
import { Table, Th, Td } from "../../components/Table";

export default function PendingCertificatesSection() {
  const [pending, setPending] = useState<PendingCertificate[]>([]);
  const [loading, setLoading] = useState(true);
  const [processing, setProcessing] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);
  const { token } = useAuth();

  const loadPending = async () => {
    if (!token) return;
    setLoading(true);
    setError(null);
    try {
      const data = await caApi.getPendingCertificates(token);
      setPending(data.pending_requests);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to load pending certificates");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadPending();
    // Refresh every 30 seconds
    const interval = setInterval(loadPending, 30000);
    return () => clearInterval(interval);
  }, [token]);

  const handleApprove = async (desktopAppId: string, nameLabel: string) => {
    if (!token) return;

    const confirmed = window.confirm(
      `Approve certificate request for "${nameLabel}"?\n\n` +
      `Desktop ID: ${desktopAppId}\n\n` +
      "This will sign the certificate and allow the desktop to use certificate-based authentication."
    );

    if (!confirmed) return;

    setProcessing(desktopAppId);
    setError(null);
    setSuccess(null);

    try {
      await caApi.approveCertificate(desktopAppId, token);
      setSuccess(`Certificate approved for ${nameLabel}`);
      await loadPending();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to approve certificate");
    } finally {
      setProcessing(null);
    }
  };

  const handleReject = async (desktopAppId: string, nameLabel: string) => {
    if (!token) return;

    const confirmed = window.confirm(
      `Reject certificate request for "${nameLabel}"?\n\n` +
      `Desktop ID: ${desktopAppId}\n\n` +
      "The desktop will need to submit a new request."
    );

    if (!confirmed) return;

    setProcessing(desktopAppId);
    setError(null);
    setSuccess(null);

    try {
      await caApi.rejectCertificate(desktopAppId, token);
      setSuccess(`Certificate request rejected for ${nameLabel}`);
      await loadPending();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to reject certificate");
    } finally {
      setProcessing(null);
    }
  };

  if (loading) {
    return (
      <div className="bg-white rounded-lg border border-gray-200 p-6 mt-6">
        <h2 className="text-xl font-semibold mb-4">Pending Certificate Requests</h2>
        <p className="text-gray-600">Loading...</p>
      </div>
    );
  }

  return (
    <div className="bg-white rounded-lg border border-gray-200 p-6 mt-6">
      <div className="flex justify-between items-center mb-4">
        <h2 className="text-xl font-semibold">Pending Certificate Requests</h2>
        <Button onClick={loadPending} variant="ghost" size="sm">
          Refresh
        </Button>
      </div>

      {error && (
        <div className="mb-4 p-4 bg-red-50 border border-red-200 rounded text-red-800">
          {error}
        </div>
      )}

      {success && (
        <div className="mb-4 p-4 bg-green-50 border border-green-200 rounded text-green-800">
          {success}
        </div>
      )}

      {pending.length === 0 ? (
        <p className="text-gray-600 text-center py-8">No pending certificate requests</p>
      ) : (
        <Table>
          <thead>
            <tr>
              <Th>Name</Th>
              <Th>Desktop ID</Th>
              <Th>Machine</Th>
              <Th>User</Th>
              <Th>Submitted</Th>
              <Th>Status</Th>
              <Th>Actions</Th>
            </tr>
          </thead>
          <tbody>
            {pending.map((row) => (
              <tr key={row.desktop_app_id}>
                <Td>{row.name_label || <span className="text-gray-400 italic">Unnamed</span>}</Td>
                <Td>
                  <span className="font-mono text-xs">{row.desktop_app_id.substring(0, 12)}...</span>
                </Td>
                <Td>{row.machine_name || <span className="text-gray-400">-</span>}</Td>
                <Td>{row.os_user || <span className="text-gray-400">-</span>}</Td>
                <Td>
                  {(() => {
                    try {
                      return new Date(row.csr_submitted_at).toLocaleString();
                    } catch {
                      return <span className="text-gray-400">-</span>;
                    }
                  })()}
                </Td>
                <Td>
                  <span
                    className={`px-2 py-1 rounded text-xs ${
                      row.status === "Active" ? "bg-green-100 text-green-800" : "bg-gray-100 text-gray-800"
                    }`}
                  >
                    {row.status}
                  </span>
                </Td>
                <Td>
                  <div className="flex gap-2">
                    <Button
                      size="sm"
                      onClick={() => handleApprove(row.desktop_app_id, row.name_label)}
                      disabled={processing === row.desktop_app_id}
                    >
                      {processing === row.desktop_app_id ? "..." : "✓ Approve"}
                    </Button>
                    <Button
                      size="sm"
                      variant="ghost"
                      onClick={() => handleReject(row.desktop_app_id, row.name_label)}
                      disabled={processing === row.desktop_app_id}
                    >
                      {processing === row.desktop_app_id ? "..." : "✗ Reject"}
                    </Button>
                  </div>
                </Td>
              </tr>
            ))}
          </tbody>
        </Table>
      )}
    </div>
  );
}

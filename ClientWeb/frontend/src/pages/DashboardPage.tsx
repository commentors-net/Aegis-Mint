import { useEffect, useState } from "react";
import { useAuth } from "../auth/AuthContext";
import { getMyShares, downloadShare, getDownloadHistory, ShareItem, DownloadHistoryItem } from "../api/client";

export default function DashboardPage() {
  const { user, logout } = useAuth();
  const [shares, setShares] = useState<ShareItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [downloading, setDownloading] = useState<string | null>(null);
  const [history, setHistory] = useState<DownloadHistoryItem[]>([]);
  const [historyLoading, setHistoryLoading] = useState(true);
  const [historyError, setHistoryError] = useState("");

  useEffect(() => {
    loadShares();
    loadHistory();
  }, []);

  const loadShares = async () => {
    try {
      const response = await getMyShares();
      setShares(response.data);
    } catch (err: any) {
      setError("Failed to load shares");
    } finally {
      setLoading(false);
    }
  };

  const handleDownload = async (assignmentId: string, shareNumber: number) => {
    if (!confirm("Are you sure you want to download this share? It will be disabled after download.")) {
      return;
    }

    setDownloading(assignmentId);
    try {
      const response = await downloadShare(assignmentId);
      const disposition = response.headers?.["content-disposition"];
      const match = disposition ? /filename="?([^"]+)"?/i.exec(disposition) : null;
      const fallbackName = `share-${String(shareNumber).padStart(2, "0")}.aegisshare`;
      const filename = match?.[1] || fallbackName;
      
      // Create download link
      const url = window.URL.createObjectURL(new Blob([response.data]));
      const link = document.createElement("a");
      link.href = url;
      link.setAttribute("download", filename);
      document.body.appendChild(link);
      link.click();
      link.parentNode?.removeChild(link);
      
      // Reload shares to update status
      await loadShares();
      await loadHistory();
    } catch (err: any) {
      alert(err.response?.data?.detail || "Download failed");
    } finally {
      setDownloading(null);
    }
  };

  const loadHistory = async () => {
    try {
      setHistoryLoading(true);
      setHistoryError("");
      const response = await getDownloadHistory();
      setHistory(response.data);
    } catch (err: any) {
      setHistoryError("Failed to load download history");
    } finally {
      setHistoryLoading(false);
    }
  };

  const formatDateTime = (dateString: string) => {
    const date = new Date(dateString);
    if (Number.isNaN(date.getTime())) return dateString;
    return date.toLocaleString("en-US", {
      year: "numeric",
      month: "short",
      day: "numeric",
      hour: "2-digit",
      minute: "2-digit",
    });
  };

  return (
    <div className="dashboard-page">
      <header className="dashboard-header">
        <div className="header-content">
          <h1>Aegis Mint - Share Portal</h1>
          <div className="user-info">
            <span>{user?.user_name}</span>
            <span className="user-email">{user?.user_email}</span>
            <button onClick={logout} className="btn-secondary">Logout</button>
          </div>
        </div>
      </header>

      <main className="dashboard-main">
        <div className="shares-section">
          <h2>My Assigned Shares</h2>
          
          {loading && <div className="loading">Loading shares...</div>}
          
          {error && <div className="error-message">{error}</div>}
          
          {!loading && shares.length === 0 && (
            <div className="no-shares">
              <p>No shares assigned yet.</p>
              <p>Contact your administrator if you believe this is an error.</p>
            </div>
          )}
          
          {!loading && shares.length > 0 && (
            <div className="shares-grid">
              {shares.map((share) => (
                <div key={share.assignment_id} className="share-card">
                  <div className="share-header">
                    <h3>Share #{share.share_number}</h3>
                    <span className={`status ${share.download_allowed ? "available" : "downloaded"}`}>
                      {share.download_allowed ? "Available" : "Downloaded"}
                    </span>
                  </div>
                  
                  <div className="share-info">
                    <div className="info-row">
                      <label>Token:</label>
                      <span className="token-highlight">{share.token_name} ({share.token_symbol})</span>
                    </div>
                    <div className="info-row">
                      <label>Contract:</label>
                      <span className="contract-address">{share.contract_address}</span>
                    </div>
                    <div className="info-row">
                      <label>Download Count:</label>
                      <span>{share.download_count}</span>
                    </div>
                    {share.last_downloaded_at_utc && (
                      <div className="info-row">
                        <label>Last Downloaded:</label>
                        <span>{new Date(share.last_downloaded_at_utc).toLocaleString()}</span>
                      </div>
                    )}
                  </div>
                  
                  <div className="share-actions">
                    <button
                      onClick={() => handleDownload(share.assignment_id, share.share_number)}
                      disabled={!share.download_allowed || downloading === share.assignment_id}
                      className="btn-primary"
                    >
                      {downloading === share.assignment_id
                        ? "Downloading..."
                        : share.download_allowed
                        ? "Download Share"
                        : "Already Downloaded"}
                    </button>
                  </div>
                  
                  {!share.download_allowed && (
                    <div className="share-note">
                      Contact administrator to re-enable download if you lost the file.
                    </div>
                  )}
                </div>
              ))}
            </div>
          )}

          <div className="history-section">
            <h2>Download History</h2>

            {historyLoading && <div className="loading">Loading history...</div>}

            {historyError && <div className="error-message">{historyError}</div>}

            {!historyLoading && history.length === 0 && (
              <div className="no-history">
                <p>No download history yet.</p>
              </div>
            )}

            {!historyLoading && history.length > 0 && (
              <div className="history-table-wrap">
                <table className="history-table">
                  <thead>
                    <tr>
                      <th>Share</th>
                      <th>Token</th>
                      <th>Downloaded</th>
                      <th>Status</th>
                      <th>IP Address</th>
                    </tr>
                  </thead>
                  <tbody>
                    {history.map((entry) => (
                      <tr key={entry.id}>
                        <td>#{entry.share_number}</td>
                        <td>{entry.token_name}</td>
                        <td>{formatDateTime(entry.downloaded_at_utc)}</td>
                        <td>
                          <span className={`history-status ${entry.success ? "ok" : "fail"}`}>
                            {entry.success ? "Success" : "Failed"}
                          </span>
                          {!entry.success && entry.failure_reason && (
                            <div className="history-failure">{entry.failure_reason}</div>
                          )}
                        </td>
                        <td>{entry.ip_address || "Unknown"}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        </div>
      </main>
    </div>
  );
}

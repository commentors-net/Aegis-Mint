import { useEffect, useState } from "react";
import { useAuth } from "../auth/AuthContext";
import { getMyShares, downloadShare, ShareItem } from "../api/client";

export default function DashboardPage() {
  const { user, logout } = useAuth();
  const [shares, setShares] = useState<ShareItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [downloading, setDownloading] = useState<string | null>(null);

  useEffect(() => {
    loadShares();
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
      
      // Create download link
      const url = window.URL.createObjectURL(new Blob([response.data]));
      const link = document.createElement("a");
      link.href = url;
      link.setAttribute("download", `aegis-share-${String(shareNumber).padStart(3, "0")}.json`);
      document.body.appendChild(link);
      link.click();
      link.parentNode?.removeChild(link);
      
      // Reload shares to update status
      await loadShares();
    } catch (err: any) {
      alert(err.response?.data?.detail || "Download failed");
    } finally {
      setDownloading(null);
    }
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
        </div>
      </main>
    </div>
  );
}

import { useEffect, useState } from "react";

import * as downloadsApi from "../../api/downloads";
import Button from "../../components/Button";
import { Table, Td, Th } from "../../components/Table";
import Toast from "../../components/Toast";
import { useAuth } from "../../auth/useAuth";

type FileRow = downloadsApi.FileInfo;

export default function DownloadsPage() {
  const { token } = useAuth();
  const [files, setFiles] = useState<FileRow[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [toast, setToast] = useState<{ message: string; type: "success" | "error" } | null>(null);
  const [adding, setAdding] = useState(false);
  const [urlInput, setUrlInput] = useState("");

  const refresh = async () => {
    if (!token) return;
    setLoading(true);
    setError(null);
    try {
      const data = await downloadsApi.downloadsApi.listFiles(token);
      setFiles(data);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to load files");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    refresh();
  }, [token]);

  const handleAddLink = async () => {
    if (!token || !urlInput.trim()) {
      setToast({ message: "Please enter a GitHub release URL", type: "error" });
      return;
    }

    setAdding(true);
    setError(null);
    try {
      const result = await downloadsApi.downloadsApi.addLink(token, urlInput.trim());
      setToast({ message: result.message, type: "success" });
      setUrlInput(""); // Clear input
      refresh();
    } catch (err) {
      const errorMsg = err instanceof Error ? err.message : "Failed to add link";
      setError(errorMsg);
      setToast({ message: errorMsg, type: "error" });
    } finally {
      setAdding(false);
    }
  };

  const handleDownload = async (filename: string) => {
    if (!token) return;
    try {
      const result = await downloadsApi.downloadsApi.getDownloadUrl(token, filename);
      // Open GitHub URL in new tab to trigger download
      window.open(result.url, "_blank");
      setToast({ message: "Download started", type: "success" });
    } catch (err) {
      const errorMsg = err instanceof Error ? err.message : "Failed to get download URL";
      setToast({ message: errorMsg, type: "error" });
    }
  };

  const handleDelete = async (filename: string) => {
    if (!token) return;
    if (!confirm(`Are you sure you want to delete "${filename}"?`)) return;

    setLoading(true);
    setError(null);
    try {
      const result = await downloadsApi.downloadsApi.deleteFile(token, filename);
      setToast({ message: result.message, type: "success" });
      refresh();
    } catch (err) {
      const errorMsg = err instanceof Error ? err.message : "Failed to delete file";
      setError(errorMsg);
      setToast({ message: errorMsg, type: "error" });
    } finally {
      setLoading(false);
    }
  };

  const formatFileSize = (bytes: number): string => {
    if (bytes === 0) return "0 B";
    const k = 1024;
    const sizes = ["B", "KB", "MB", "GB"];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return Math.round(bytes / Math.pow(k, i) * 100) / 100 + " " + sizes[i];
  };

  const formatDate = (isoString: string): string => {
    const date = new Date(isoString);
    return date.toLocaleString();
  };

  return (
    <>
      {toast && <Toast message={toast.message} type={toast.type} onClose={() => setToast(null)} />}

      <div style={{ marginBottom: "1.5rem" }}>
        <h3>Application Downloads</h3>
        <p style={{ color: "var(--muted)", marginTop: "0.25rem" }}>
          Manage GitHub release links for desktop application installers.
        </p>
      </div>

      {error && (
        <div style={{ padding: "1rem", background: "var(--danger-bg)", border: "1px solid var(--danger)", borderRadius: "6px", marginBottom: "1rem" }}>
          {error}
        </div>
      )}

      <div style={{ marginBottom: "1.5rem" }}>
        <div style={{ display: "flex", gap: "0.5rem", alignItems: "flex-start", marginBottom: "0.5rem" }}>
          <input
            type="text"
            value={urlInput}
            onChange={(e) => setUrlInput(e.target.value)}
            placeholder="https://github.com/user/repo/releases/download/tag/App-Setup.exe"
            disabled={adding}
            style={{
              flex: 1,
              padding: "0.5rem",
              border: "1px solid var(--border)",
              borderRadius: "6px",
              background: "var(--bg)",
              color: "var(--fg)",
              fontSize: "0.875rem",
            }}
            onKeyDown={(e) => {
              if (e.key === "Enter") {
                handleAddLink();
              }
            }}
          />
          <Button onClick={handleAddLink} disabled={adding || !urlInput.trim()}>
            {adding ? "Adding..." : "Add Link"}
          </Button>
        </div>
        <p style={{ color: "var(--muted)", fontSize: "0.875rem", margin: 0 }}>
          Enter a GitHub release URL pointing to an .exe file
        </p>
      </div>

      {loading && <p>Loading files...</p>}

      {!loading && files.length === 0 && (
        <div style={{ padding: "2rem", textAlign: "center", color: "var(--muted)" }}>
          No download links added yet. Add your first GitHub release link to get started.
        </div>
      )}

      {!loading && files.length > 0 && (
        <Table>
          <thead>
            <tr>
              <Th>Filename</Th>
              <Th>Added</Th>
              <Th style={{ textAlign: "right" }}>Actions</Th>
            </tr>
          </thead>
          <tbody>
            {files.map((file) => (
              <tr key={file.filename}>
                <Td>
                  <strong>{file.filename}</strong>
                </Td>
                <Td>{formatDate(file.created_at)}</Td>
                <Td style={{ textAlign: "right" }}>
                  <div style={{ display: "inline-flex", gap: "0.5rem" }}>
                    <Button variant="outline" onClick={() => handleDownload(file.filename)}>
                      Download
                    </Button>
                    <Button variant="outline" onClick={() => handleDelete(file.filename)}>
                      Delete
                    </Button>
                  </div>
                </Td>
              </tr>
            ))}
          </tbody>
        </Table>
      )}
    </>
  );
}

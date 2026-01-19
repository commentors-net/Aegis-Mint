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
  const [uploading, setUploading] = useState(false);

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

  const handleUpload = async (e: React.ChangeEvent<HTMLInputElement>) => {
    if (!token || !e.target.files || e.target.files.length === 0) return;

    const file = e.target.files[0];
    
    // Validate file extension
    if (!file.name.toLowerCase().endsWith(".exe")) {
      setToast({ message: "Only .exe files are allowed", type: "error" });
      e.target.value = ""; // Reset input
      return;
    }

    setUploading(true);
    setError(null);
    try {
      const result = await downloadsApi.downloadsApi.uploadFile(token, file);
      setToast({ message: result.message, type: "success" });
      e.target.value = ""; // Reset input
      refresh();
    } catch (err) {
      const errorMsg = err instanceof Error ? err.message : "Failed to upload file";
      setError(errorMsg);
      setToast({ message: errorMsg, type: "error" });
    } finally {
      setUploading(false);
    }
  };

  const handleDownload = async (filename: string) => {
    if (!token) return;
    try {
      await downloadsApi.downloadsApi.downloadFile(token, filename);
      setToast({ message: "Download started", type: "success" });
    } catch (err) {
      const errorMsg = err instanceof Error ? err.message : "Failed to download file";
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

  const formatDate = (timestamp: string): string => {
    const date = new Date(parseFloat(timestamp) * 1000);
    return date.toLocaleString();
  };

  return (
    <>
      {toast && <Toast message={toast.message} type={toast.type} onClose={() => setToast(null)} />}

      <div style={{ marginBottom: "1.5rem" }}>
        <h3>Application Downloads</h3>
        <p style={{ color: "var(--muted)", marginTop: "0.25rem" }}>
          Upload and manage .exe installer files for desktop applications.
        </p>
      </div>

      {error && (
        <div style={{ padding: "1rem", background: "var(--danger-bg)", border: "1px solid var(--danger)", borderRadius: "6px", marginBottom: "1rem" }}>
          {error}
        </div>
      )}

      <div style={{ marginBottom: "1.5rem", display: "flex", gap: "1rem", alignItems: "center" }}>
        <label
          htmlFor="file-upload"
          style={{
            display: "inline-flex",
            alignItems: "center",
            padding: "0.5rem 1rem",
            background: "var(--accent)",
            color: "var(--bg)",
            borderRadius: "6px",
            cursor: uploading ? "not-allowed" : "pointer",
            opacity: uploading ? 0.5 : 1,
            fontWeight: 500,
          }}
        >
          {uploading ? "Uploading..." : "Upload .exe File"}
        </label>
        <input
          id="file-upload"
          type="file"
          accept=".exe"
          onChange={handleUpload}
          disabled={uploading}
          style={{ display: "none" }}
        />
        <span style={{ color: "var(--muted)", fontSize: "0.875rem" }}>
          Only .exe files are allowed
        </span>
      </div>

      {loading && <p>Loading files...</p>}

      {!loading && files.length === 0 && (
        <div style={{ padding: "2rem", textAlign: "center", color: "var(--muted)" }}>
          No files uploaded yet. Upload your first .exe file to get started.
        </div>
      )}

      {!loading && files.length > 0 && (
        <Table>
          <thead>
            <tr>
              <Th>Filename</Th>
              <Th>Size</Th>
              <Th>Uploaded</Th>
              <Th style={{ textAlign: "right" }}>Actions</Th>
            </tr>
          </thead>
          <tbody>
            {files.map((file) => (
              <tr key={file.filename}>
                <Td>
                  <strong>{file.filename}</strong>
                </Td>
                <Td>{formatFileSize(file.size)}</Td>
                <Td>{formatDate(file.uploaded_at)}</Td>
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

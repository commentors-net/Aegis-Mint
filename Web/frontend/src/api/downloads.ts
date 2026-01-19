import { apiFetch, API_BASE } from "./client";

export interface FileInfo {
  filename: string;
  size: number;
  uploaded_at: string;
}

export const downloadsApi = {
  async listFiles(token: string): Promise<FileInfo[]> {
    return apiFetch(`/api/admin/downloads`, { token });
  },

  async uploadFile(token: string, file: File): Promise<{ filename: string; message: string }> {
    const formData = new FormData();
    formData.append("file", file);

    const response = await fetch(`${API_BASE}/api/admin/downloads/upload`, {
      method: "POST",
      headers: {
        Authorization: `Bearer ${token}`,
      },
      body: formData,
    });

    if (!response.ok) {
      const error = await response.json();
      throw new Error(error.detail || "Upload failed");
    }

    return response.json();
  },

  async downloadFile(token: string, filename: string): Promise<void> {
    const response = await fetch(`${API_BASE}/api/admin/downloads/download/${encodeURIComponent(filename)}`, {
      method: "GET",
      headers: {
        Authorization: `Bearer ${token}`,
      },
    });

    if (!response.ok) {
      throw new Error("Download failed");
    }

    // Create blob and trigger download
    const blob = await response.blob();
    const url = window.URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = filename;
    document.body.appendChild(a);
    a.click();
    window.URL.revokeObjectURL(url);
    document.body.removeChild(a);
  },

  async deleteFile(token: string, filename: string): Promise<{ filename: string; message: string }> {
    return apiFetch(`/api/admin/downloads/${encodeURIComponent(filename)}`, {
      token,
      method: "DELETE",
    });
  },
};

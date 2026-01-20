import { apiFetch, API_BASE } from "./client";

export interface FileInfo {
  filename: string;
  url: string;
  created_at: string;
}

export const downloadsApi = {
  async listFiles(token: string): Promise<FileInfo[]> {
    return apiFetch(`/api/admin/downloads`, { token });
  },

  async addLink(token: string, url: string): Promise<{ filename: string; message: string }> {
    return apiFetch(`/api/admin/downloads/add`, {
      token,
      method: "POST",
      body: { url },
    });
  },

  async getDownloadUrl(token: string, filename: string): Promise<{ url: string }> {
    return apiFetch(`/api/admin/downloads/url/${encodeURIComponent(filename)}`, { token });
  },

  async deleteFile(token: string, filename: string): Promise<{ filename: string; message: string }> {
    return apiFetch(`/api/admin/downloads/${encodeURIComponent(filename)}`, {
      token,
      method: "DELETE",
    });
  },
};

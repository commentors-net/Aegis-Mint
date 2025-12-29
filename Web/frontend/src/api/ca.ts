import { apiFetch } from "./client";

export interface CAStatus {
  exists: boolean;
  ca_certificate?: string;
  created_at?: string;
  expires_at?: string;
  expiring_soon?: boolean;
  expired?: boolean;
  days_until_expiry?: number;
  subject?: string;
  issuer?: string;
}

export interface GenerateCAResponse {
  success: boolean;
  message: string;
  ca_certificate: string;
  created_at: string;
  expires_at: string;
}

export interface PendingCertificate {
  desktop_app_id: string;
  name_label: string;
  machine_name: string;
  os_user: string;
  csr_submitted_at: string;
  status: string;
}

export interface PendingCertificatesResponse {
  pending_requests: PendingCertificate[];
}

export interface ApproveCertificateResponse {
  success: boolean;
  certificate: string;
  expires_at: string;
}

export const caApi = {
  async getStatus(token: string): Promise<CAStatus> {
    return apiFetch<CAStatus>("/admin/ca/status", { token });
  },

  async generate(token: string): Promise<GenerateCAResponse> {
    return apiFetch<GenerateCAResponse>("/admin/ca/generate", {
      method: "POST",
      token,
    });
  },

  async downloadCertificate(token: string): Promise<{ ca_certificate: string; expires_at: string; filename: string }> {
    return apiFetch("/admin/ca/certificate", { token });
  },

  async getPendingCertificates(token: string): Promise<PendingCertificatesResponse> {
    return apiFetch<PendingCertificatesResponse>("/admin/ca/pending-certificates", { token });
  },

  async approveCertificate(desktopAppId: string, token: string): Promise<ApproveCertificateResponse> {
    return apiFetch<ApproveCertificateResponse>(`/admin/ca/approve-certificate/${desktopAppId}`, {
      method: "POST",
      token,
    });
  },

  async rejectCertificate(desktopAppId: string, token: string): Promise<{ success: boolean; message: string }> {
    return apiFetch(`/admin/ca/reject-certificate/${desktopAppId}`, {
      method: "POST",
      token,
    });
  },
};

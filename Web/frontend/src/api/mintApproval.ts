import { apiFetch } from './client';

export interface MintDesktop {
  id: string;
  desktop_app_id: string;
  app_type: string;
  name_label: string | null;
  status: string;
  required_approvals_n: number;
  unlock_minutes: number;
  machine_name: string | null;
  os_user: string | null;
  token_control_version: string | null;
  created_at_utc: string;
  last_seen_at_utc: string | null;
}

export interface MintApprovalRequest {
  unlockMinutes?: number;
}

export interface ApprovalSummary {
  sessionId: string;
  desktopAppId: string;
  status: string;
  requiredApprovalsSnapshot: number;
  approvalsCount: number;
  unlockedUntilUtc: string | null;
  remainingSeconds: number;
}

export const mintApprovalApi = {
  async listMintDesktops(token: string): Promise<MintDesktop[]> {
    return apiFetch<MintDesktop[]>('/api/admin/mint/desktops', { token });
  },

  async approveMintDesktop(desktopAppId: string, unlockMinutes: number = 15, token: string): Promise<MintDesktop> {
    return apiFetch<MintDesktop>(`/api/admin/mint/desktops/${desktopAppId}/approve`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ unlockMinutes }),
      token,
    });
  },

  async approveSession(desktopAppId: string, token: string): Promise<ApprovalSummary> {
    return apiFetch<ApprovalSummary>(`/api/admin/mint/desktops/${desktopAppId}/approve-session`, {
      method: 'POST',
      token,
    });
  },
};

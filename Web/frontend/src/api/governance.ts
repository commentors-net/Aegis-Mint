import { apiFetch } from "./client";

export type AssignedDesktop = {
  desktopAppId: string;
  nameLabel?: string;
  lastSeenAtUtc?: string;
  requiredApprovalsN: number;
  approvalsSoFar: number;
  status: "Pending" | "Active" | "Disabled";
  sessionStatus: "None" | "Pending" | "Unlocked" | "Expired" | "Cancelled";
  unlockedUntilUtc?: string;
  alreadyApproved?: boolean;
  remainingSeconds?: number;
};

export type ApprovalSummary = {
  sessionId: string;
  desktopAppId: string;
  status: AssignedDesktop["sessionStatus"];
  requiredApprovalsSnapshot: number;
  unlockedUntilUtc?: string;
  remainingSeconds?: number;
  approvals: { approverUserId: string; approvedAtUtc: string; approverEmail?: string }[];
};

export function getAssignedDesktops(token: string) {
  return apiFetch<AssignedDesktop[]>("/api/governance/desktops", { token });
}

export function approveDesktop(desktopAppId: string, token: string) {
  return apiFetch<ApprovalSummary>(`/api/governance/desktops/${desktopAppId}/approve`, {
    method: "POST",
    token,
  });
}

export function getDesktopHistory(desktopAppId: string, token: string) {
  return apiFetch<ApprovalSummary | null>(`/api/governance/desktops/${desktopAppId}/history`, { token });
}

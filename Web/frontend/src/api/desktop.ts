import { apiFetch } from "./client";

export type UnlockStatus = {
  desktopStatus: "Pending" | "Active" | "Disabled";
  isUnlocked: boolean;
  unlockedUntilUtc?: string;
  remainingSeconds: number;
  requiredApprovalsN: number;
  approvalsSoFar: number;
  sessionStatus: "None" | "Pending" | "Unlocked" | "Expired" | "Cancelled";
};

export type RegisterResponse = {
  desktopStatus: "Pending" | "Active";
  requiredApprovalsN: number;
  unlockMinutes: number;
};

export function registerDesktop(body: Record<string, unknown>) {
  return apiFetch<RegisterResponse>("/api/desktop/register", { method: "POST", body: JSON.stringify(body) });
}

export function getUnlockStatus(desktopAppId: string) {
  return apiFetch<UnlockStatus>(`/api/desktop/${desktopAppId}/unlock-status`);
}

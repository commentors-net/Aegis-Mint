import { apiFetch } from "./client";

export type User = {
  id: string;
  email: string;
  role: "SuperAdmin" | "GovernanceAuthority";
  phone?: string;
  is_active: boolean;
};

export type Desktop = {
  id: string;  // UUID for unique desktop identification
  desktopAppId: string;
  nameLabel?: string;
  appType?: string;
  status: "Pending" | "Active" | "Disabled";
  requiredApprovalsN: number;
  unlockMinutes: number;
  lastSeenAtUtc?: string;
};

export type AuditEntry = {
  id: string;
  at_utc: string;
  action: string;
  actor_user_id?: string;
  desktop_app_id?: string;
  session_id?: string;
  details?: string;
};

export type AuditPage = {
  items: AuditEntry[];
  total: number;
  page: number;
  pageSize: number;
};

export type SystemSettings = {
  requiredApprovalsDefault: number;
  unlockMinutesDefault: number;
};

const normalizeDesktop = (d: any): Desktop => ({
  id: d.id,
  desktopAppId: d.desktopAppId ?? d.desktop_app_id,
  nameLabel: d.nameLabel ?? d.name_label,
  appType: d.appType ?? d.app_type,
  status: d.status,
  requiredApprovalsN: d.requiredApprovalsN ?? d.required_approvals_n,
  unlockMinutes: d.unlockMinutes ?? d.unlock_minutes,
  lastSeenAtUtc: d.lastSeenAtUtc ?? d.last_seen_at_utc,
});

export function listUsers(token: string) {
  return apiFetch<User[]>("/api/admin/users", { token });
}

export function listDesktops(token: string) {
  return apiFetch<any[]>("/api/admin/desktops", { token }).then((rows) => rows.map(normalizeDesktop));
}

export function createDesktop(token: string, body: { desktopAppId: string; nameLabel?: string; requiredApprovalsN?: number; unlockMinutes?: number }) {
  return apiFetch<any>("/api/admin/desktops", {
    method: "POST",
    token,
    body: JSON.stringify(body),
  }).then(normalizeDesktop);
}

export function updateDesktop(
  token: string,
  desktopAppId: string,
  appType: string,
  body: Partial<{ nameLabel: string; requiredApprovalsN: number; unlockMinutes: number; status: Desktop["status"] }>,
) {
  return apiFetch<any>(`/api/admin/desktops/${desktopAppId}?app_type=${encodeURIComponent(appType)}`, {
    method: "PUT",
    token,
    body: JSON.stringify(body),
  }).then(normalizeDesktop);
}

export function approveDesktop(token: string, desktopAppId: string, appType: string, body?: { requiredApprovalsN?: number; unlockMinutes?: number }) {
  return apiFetch<any>(`/api/admin/desktops/${desktopAppId}/approve?app_type=${appType}`, {
    method: "POST",
    token,
    body: JSON.stringify(body || {}),
  }).then(normalizeDesktop);
}

export function rejectDesktop(token: string, desktopAppId: string, appType: string) {
  return apiFetch<any>(`/api/admin/desktops/${desktopAppId}/reject?app_type=${appType}`, {
    method: "POST",
    token,
  }).then(normalizeDesktop);
}

export function getAuditLogs(token: string, params: { page?: number; pageSize?: number; q?: string } = {}) {
  const search = new URLSearchParams();
  if (params.page) search.set("page", String(params.page));
  if (params.pageSize) search.set("page_size", String(params.pageSize));
  if (params.q) search.set("q", params.q);
  const query = search.toString() ? `?${search.toString()}` : "";
  return apiFetch<AuditPage>(`/api/admin/audit${query}`, { token });
}

export function getSystemSettings(token: string) {
  return apiFetch<any>("/api/admin/settings", { token }).then((s) => ({
    requiredApprovalsDefault: s.requiredApprovalsDefault ?? s.required_approvals_default,
    unlockMinutesDefault: s.unlockMinutesDefault ?? s.unlock_minutes_default,
  }));
}

export function updateSystemSettings(token: string, body: SystemSettings) {
  return apiFetch<any>("/api/admin/settings", {
    method: "PUT",
    token,
    body: JSON.stringify({
      required_approvals_default: body.requiredApprovalsDefault,
      unlock_minutes_default: body.unlockMinutesDefault,
    }),
  }).then((s) => ({
    requiredApprovalsDefault: s.requiredApprovalsDefault ?? s.required_approvals_default,
    unlockMinutesDefault: s.unlockMinutesDefault ?? s.unlock_minutes_default,
  }));
}

export function createUser(token: string, body: { email: string; password: string; role: User["role"]; mfa_secret: string }) {
  return apiFetch<User>("/api/admin/users", {
    method: "POST",
    token,
    body: JSON.stringify(body),
  });
}

export function updateUser(
  token: string,
  userId: string,
  body: Partial<{ email: string; password: string; role: User["role"]; is_active: boolean; mfa_secret: string }>,
) {
  return apiFetch<User>(`/api/admin/users/${userId}`, {
    method: "PUT",
    token,
    body: JSON.stringify(body),
  });
}

export function getUserAssignments(token: string, userId: string) {
  return apiFetch<string[]>(`/api/admin/users/${userId}/assignments`, { token });
}

export function setUserAssignments(token: string, userId: string, desktopAppIds: string[]) {
  return apiFetch<string[]>(`/api/admin/users/${userId}/assignments`, {
    method: "POST",
    token,
    body: JSON.stringify({ desktopAppIds }),
  });
}

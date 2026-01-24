import { apiFetch } from "./client";

export type TokenDeployment = {
  id: string;
  token_name: string;
  token_symbol: string;
  token_decimals: number;
  network: string;
  contract_address: string;
  shares_uploaded: boolean;
  shares_uploaded_at: string | null;
  shares_uploaded_count: number;
  created_at_utc: string;
  gov_shares: number;
  total_shares: number;
};

export type ShareFile = {
  id: string;
  token_deployment_id: string;
  share_number: number;
  file_name: string;
  created_at_utc: string;
  is_assigned: boolean;
  assigned_to?: {
    user_id: string;
    user_email: string;
    assignment_id: string;
    download_allowed: boolean;
    download_count: number;
  };
};

export type ShareAssignment = {
  id: string;
  share_file_id: string;
  share_number: number;
  token_name: string;
  user_id: string;
  user_email: string;
  assigned_by: string;
  assigner_email: string;
  assigned_at_utc: string;
  is_active: boolean;
  download_allowed: boolean;
  download_count: number;
  first_downloaded_at_utc: string | null;
  last_downloaded_at_utc: string | null;
  assignment_notes: string | null;
};

export type ShareOperationLog = {
  id: string;
  operation_type: string;
  share_assignment_id: string;
  performed_by: string;
  performer_email: string;
  performed_at_utc: string;
  details: string | null;
};

export type User = {
  id: string;
  email: string;
  role: "SuperAdmin" | "GovernanceAuthority";
  is_active: boolean;
};

/**
 * Get list of all token deployments with share upload status
 */
export function listTokenDeployments(token: string, params?: { shares_uploaded?: boolean }) {
  const query = new URLSearchParams();
  if (params?.shares_uploaded !== undefined) {
    query.set("shares_uploaded", String(params.shares_uploaded));
  }
  const queryString = query.toString() ? `?${query.toString()}` : "";
  return apiFetch<TokenDeployment[]>(`/api/token-deployments/${queryString}`, { token });
}

/**
 * Get shares for a specific token deployment with assignment status
 */
export function getTokenShares(token: string, tokenDeploymentId: string) {
  return apiFetch<ShareFile[]>(`/api/share-files/token/${tokenDeploymentId}`, { token });
}

/**
 * Get list of users (for assignment dropdown)
 */
export function listUsers(token: string) {
  return apiFetch<User[]>("/api/admin/users", { token });
}

/**
 * Create a new share assignment
 */
export function createShareAssignment(
  token: string,
  body: {
    share_file_id: string;
    user_id: string;
    assignment_notes?: string;
  }
) {
  return apiFetch<ShareAssignment>("/api/admin/share-assignments", {
    method: "POST",
    token,
    body: JSON.stringify(body),
  });
}

/**
 * Get list of share assignments with filters
 */
export function listShareAssignments(
  token: string,
  params?: {
    token_deployment_id?: string;
    user_id?: string;
    share_file_id?: string;
    is_active?: boolean;
  }
) {
  const query = new URLSearchParams();
  if (params?.token_deployment_id) query.set("token_deployment_id", params.token_deployment_id);
  if (params?.user_id) query.set("user_id", params.user_id);
  if (params?.share_file_id) query.set("share_file_id", params.share_file_id);
  if (params?.is_active !== undefined) query.set("is_active", String(params.is_active));
  
  const queryString = query.toString() ? `?${query.toString()}` : "";
  return apiFetch<ShareAssignment[]>(`/api/admin/share-assignments${queryString}`, { token });
}

/**
 * Update a share assignment (re-enable download or update notes)
 */
export function updateShareAssignment(
  token: string,
  assignmentId: string,
  body: {
    download_allowed?: boolean;
    assignment_notes?: string;
    is_active?: boolean;
  }
) {
  return apiFetch<ShareAssignment>(`/api/admin/share-assignments/${assignmentId}`, {
    method: "PATCH",
    token,
    body: JSON.stringify(body),
  });
}

/**
 * Delete (unassign) a share assignment
 */
export function deleteShareAssignment(token: string, assignmentId: string) {
  return apiFetch<{ message: string }>(`/api/admin/share-assignments/${assignmentId}`, {
    method: "DELETE",
    token,
  });
}

/**
 * Get operation history for assignments
 */
export function getShareOperationLogs(
  token: string,
  params?: {
    share_assignment_id?: string;
    user_id?: string;
    limit?: number;
  }
) {
  const query = new URLSearchParams();
  if (params?.share_assignment_id) query.set("share_assignment_id", params.share_assignment_id);
  if (params?.user_id) query.set("user_id", params.user_id);
  if (params?.limit) query.set("limit", String(params.limit));
  
  const queryString = query.toString() ? `?${query.toString()}` : "";
  return apiFetch<ShareOperationLog[]>(`/api/admin/share-operations${queryString}`, { token });
}

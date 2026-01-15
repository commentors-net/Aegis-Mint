import { apiFetch } from "./client";

export type LoginResponse = {
  mfa_required: boolean;
  challenge_id: string;
  mfa_secret_base32?: string;
  otpauth_url?: string;
  mfa_qr_base64?: string;
};

export type VerifyOtpResponse = {
  access_token: string;
  refresh_token?: string;
  token_type: string;
  expires_at: string;
  role: "SuperAdmin" | "GovernanceAuthority";
  user: {
    id: string;
    email: string;
    role: "SuperAdmin" | "GovernanceAuthority";
  };
};

export async function login(email: string, password: string): Promise<LoginResponse> {
  return apiFetch<LoginResponse>("/auth/login", {
    method: "POST",
    body: JSON.stringify({ email, password }),
  });
}

export async function verifyOtp(challengeId: string, otp: string): Promise<VerifyOtpResponse> {
  return apiFetch<VerifyOtpResponse>("/auth/verify-otp", {
    method: "POST",
    body: JSON.stringify({ challenge_id: challengeId, otp }),
  });
}

export async function changePassword(token: string, current_password: string, new_password: string) {
  return apiFetch<{ status: string }>("/auth/change-password", {
    method: "POST",
    token,
    body: JSON.stringify({ current_password, new_password }),
  });
}

export async function refreshToken(refreshToken: string): Promise<VerifyOtpResponse> {
  return apiFetch<VerifyOtpResponse>("/auth/refresh", {
    method: "POST",
    body: JSON.stringify({ refresh_token: refreshToken }),
  });
}

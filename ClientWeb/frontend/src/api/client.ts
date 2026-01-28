import axios from "axios";

const apiBase = (import.meta.env.VITE_API_BASE || "/api").replace(/\/+$/, "");

const api = axios.create({
  baseURL: apiBase,
  headers: {
    "Content-Type": "application/json",
  },
});

// Add token to requests
api.interceptors.request.use((config) => {
  const token = localStorage.getItem("accessToken");
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

// Handle 401 responses
api.interceptors.response.use(
  (response) => response,
  async (error) => {
    if (error.response?.status === 401) {
      // Try to refresh token
      const refreshToken = localStorage.getItem("refreshToken");
      if (refreshToken) {
        try {
          const response = await axios.post(`${apiBase}/auth/refresh`, {
            refresh_token: refreshToken,
          });
          const { access_token, refresh_token } = response.data;
          localStorage.setItem("accessToken", access_token);
          localStorage.setItem("refreshToken", refresh_token);
          
          // Retry original request
          error.config.headers.Authorization = `Bearer ${access_token}`;
          return axios(error.config);
        } catch (refreshError) {
          // Refresh failed, redirect to login
          localStorage.removeItem("accessToken");
          localStorage.removeItem("refreshToken");
          window.location.href = "/login";
        }
      } else {
        // No refresh token, redirect to login
        window.location.href = "/login";
      }
    }
    return Promise.reject(error);
  }
);

export interface LoginRequest {
  email: string;
  password: string;
}

export interface LoginResponse {
  challenge_id: string;
  mfa_secret_base32?: string;
  otpauth_url?: string;
  mfa_qr_base64?: string;
}

export interface VerifyOtpRequest {
  challenge_id: string;
  otp: string;
}

export interface VerifyOtpResponse {
  access_token: string;
  refresh_token: string;
  expires_at: string;
  user_id: string;
  user_email: string;
  user_name: string;
  token_deployment_id: string;
}

export interface ShareItem {
  assignment_id: string;
  share_file_id: string;
  share_number: number;
  token_name: string;
  token_symbol: string;
  contract_address: string;
  download_allowed: boolean;
  download_count: number;
  first_downloaded_at_utc: string | null;
  last_downloaded_at_utc: string | null;
}

export interface DownloadHistoryItem {
  id: string;
  assignment_id: string;
  share_number: number;
  token_name: string;
  downloaded_at_utc: string;
  ip_address: string | null;
  success: boolean;
  failure_reason: string | null;
}

// Auth API
export const login = (data: LoginRequest) =>
  api.post<LoginResponse>("/auth/login", data);

export const verifyOtp = (data: VerifyOtpRequest) =>
  api.post<VerifyOtpResponse>("/auth/verify-otp", data);

export const refreshToken = (refreshToken: string) =>
  api.post<VerifyOtpResponse>("/auth/refresh", { refresh_token: refreshToken });

// Shares API
export const getMyShares = () =>
  api.get<ShareItem[]>("/shares/my-shares");

export const downloadShare = (assignmentId: string) =>
  api.get(`/shares/download/${assignmentId}`, {
    responseType: "blob",
  });

export const getDownloadHistory = () =>
  api.get<DownloadHistoryItem[]>("/shares/history");

export default api;

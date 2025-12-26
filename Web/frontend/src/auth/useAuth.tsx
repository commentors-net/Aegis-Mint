import { createContext, ReactNode, useContext, useState } from "react";

import * as authApi from "../api/auth";

export type Role = "SuperAdmin" | "GovernanceAuthority" | null;

type AuthContextType = {
  token: string | null;
  refreshToken: string | null;
  role: Role;
  user: authApi.VerifyOtpResponse["user"] | null;
  challengeId: string | null;
  enrollmentSecret: string | null;
  otpauthUrl: string | null;
  mfaQrBase64: string | null;
  loading: boolean;
  error: string | null;
  login: (email: string, password: string) => Promise<void>;
  verifyOtp: (otp: string) => Promise<void>;
  logout: () => void;
};

const AuthContext = createContext<AuthContextType | undefined>(undefined);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [token, setToken] = useState<string | null>(null);
  const [refreshToken, setRefreshToken] = useState<string | null>(null);
  const [role, setRole] = useState<Role>(null);
  const [user, setUser] = useState<authApi.VerifyOtpResponse["user"] | null>(null);
  const [challengeId, setChallengeId] = useState<string | null>(null);
  const [enrollmentSecret, setEnrollmentSecret] = useState<string | null>(null);
  const [otpauthUrl, setOtpauthUrl] = useState<string | null>(null);
  const [mfaQrBase64, setMfaQrBase64] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const login = async (email: string, password: string) => {
    setLoading(true);
    setError(null);
    try {
      const res = await authApi.login(email, password);
      setChallengeId(res.challenge_id);
      setEnrollmentSecret(res.mfa_secret_base32 || null);
      setOtpauthUrl(res.otpauth_url || null);
      setMfaQrBase64((res as any).mfa_qr_base64 || (res as any).mfaQrBase64 || null);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Login failed");
      setChallengeId(null);
      setEnrollmentSecret(null);
      setOtpauthUrl(null);
      setMfaQrBase64(null);
    } finally {
      setLoading(false);
    }
  };

  const verifyOtp = async (otp: string) => {
    if (!challengeId) {
      setError("Start login first.");
      return;
    }
    setLoading(true);
    setError(null);
    try {
      const res = await authApi.verifyOtp(challengeId, otp);
      setToken(res.access_token);
      setRefreshToken(res.refresh_token ?? null);
      setRole(res.role);
      setUser(res.user);
      setChallengeId(null);
      setEnrollmentSecret(null);
      setOtpauthUrl(null);
      setMfaQrBase64(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Verification failed");
      setRole(null);
      setToken(null);
    } finally {
      setLoading(false);
    }
  };

  const logout = () => {
    setToken(null);
    setRefreshToken(null);
    setRole(null);
    setUser(null);
    setChallengeId(null);
    setEnrollmentSecret(null);
    setOtpauthUrl(null);
    setMfaQrBase64(null);
    setError(null);
  };

  return (
    <AuthContext.Provider
      value={{
        token,
        refreshToken,
        role,
        user,
        challengeId,
        enrollmentSecret,
        otpauthUrl,
        mfaQrBase64,
        loading,
        error,
        login,
        verifyOtp,
        logout,
      }}
    >
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth must be used within AuthProvider");
  return ctx;
}

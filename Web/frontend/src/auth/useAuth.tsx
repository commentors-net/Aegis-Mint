import { createContext, ReactNode, useContext, useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";

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
  sessionExpiresAt: Date | null;
  timeRemaining: number | null;
  login: (email: string, password: string) => Promise<void>;
  verifyOtp: (otp: string) => Promise<void>;
  logout: () => void;
};

const AuthContext = createContext<AuthContextType | undefined>(undefined);

export function AuthProvider({ children }: { children: ReactNode }) {
  const navigate = useNavigate();
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
  const [sessionExpiresAt, setSessionExpiresAt] = useState<Date | null>(null);
  const [timeRemaining, setTimeRemaining] = useState<number | null>(null);

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
      
      // Set session expiration
      const expiresAt = new Date(res.expires_at);
      setSessionExpiresAt(expiresAt);
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
    setSessionExpiresAt(null);
    setTimeRemaining(null);
  };

  // Auto-refresh token on user activity to extend session
  useEffect(() => {
    if (!token || !refreshToken || !sessionExpiresAt) return;

    let isRefreshing = false;
    let debounceTimer: number | null = null;
    let lastRefreshTime = Date.now();

    const handleActivity = () => {
      // Debounce activity events to avoid too many refresh attempts
      if (debounceTimer) clearTimeout(debounceTimer);
      
      debounceTimer = setTimeout(async () => {
        if (isRefreshing) return; // Prevent concurrent refreshes
        
        const now = Date.now();
        const timeSinceLastRefresh = now - lastRefreshTime;
        
        // Only refresh if at least 30 seconds have passed since last refresh
        // This prevents too many refresh requests
        if (timeSinceLastRefresh < 30000) {
          console.log('[Auth] Activity detected but refresh cooldown active');
          return;
        }
        
        const remaining = Math.floor((sessionExpiresAt.getTime() - now) / 1000);
        
        // Refresh token on any activity (this extends the session to full 15 minutes)
        if (remaining > 0) {
          isRefreshing = true;
          try {
            console.log(`[Auth] Activity detected - refreshing token (${remaining}s remaining)`);
            const res = await authApi.refreshToken(refreshToken);
            setToken(res.access_token);
            setRefreshToken(res.refresh_token ?? null);
            const newExpiresAt = new Date(res.expires_at);
            setSessionExpiresAt(newExpiresAt);
            lastRefreshTime = Date.now();
            console.log(`[Auth] Token refreshed - session extended to: ${newExpiresAt.toLocaleTimeString()}`);
          } catch (err) {
            console.error('[Auth] Token refresh failed:', err);
            // Refresh failed - will timeout naturally
          } finally {
            isRefreshing = false;
          }
        }
      }, 1000); // Debounce for 1 second
    };

    // Listen for user activity
    const events = ['mousedown', 'keydown', 'scroll', 'touchstart'];
    events.forEach(event => window.addEventListener(event, handleActivity, { passive: true }));

    return () => {
      if (debounceTimer) clearTimeout(debounceTimer);
      events.forEach(event => window.removeEventListener(event, handleActivity));
    };
  }, [token, refreshToken, sessionExpiresAt]);

  // Countdown timer and auto-logout on expiration
  useEffect(() => {
    if (!sessionExpiresAt || !token) {
      setTimeRemaining(null);
      return;
    }

    const updateCountdown = () => {
      const now = new Date();
      const remaining = Math.floor((sessionExpiresAt.getTime() - now.getTime()) / 1000);
      
      if (remaining <= 0) {
        // Session expired - logout and redirect
        logout();
        navigate("/login", { replace: true });
        return;
      }
      
      setTimeRemaining(remaining);
    };

    // Update immediately
    updateCountdown();

    // Update every second
    const interval = setInterval(updateCountdown, 1000);

    return () => clearInterval(interval);
  }, [sessionExpiresAt, token, navigate]);

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
        sessionExpiresAt,
        timeRemaining,
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

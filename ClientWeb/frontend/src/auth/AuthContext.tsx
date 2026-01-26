import React, { createContext, useContext, useState, useEffect } from "react";

interface User {
  user_id: string;
  user_email: string;
  user_name: string;
  token_deployment_id: string;
}

interface AuthContextType {
  isAuthenticated: boolean;
  user: User | null;
  challengeId: string | null;
  mfaSetup: { secret: string; qrCode: string } | null;
  availableTokens: Array<{ token_id: string; token_name: string; contract_address: string | null }> | null;
  login: (email: string, password: string) => Promise<void>;
  verifyMfa: (otp: string, selectedTokenId?: string) => Promise<void>;
  logout: () => void;
}

const AuthContext = createContext<AuthContextType | undefined>(undefined);

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [isAuthenticated, setIsAuthenticated] = useState(false);
  const [user, setUser] = useState<User | null>(null);
  const [challengeId, setChallengeId] = useState<string | null>(null);
  const [mfaSetup, setMfaSetup] = useState<{ secret: string; qrCode: string } | null>(null);
  const [availableTokens, setAvailableTokens] = useState<Array<{ token_id: string; token_name: string; contract_address: string | null }> | null>(null);

  useEffect(() => {
    // Check if user is already logged in
    const token = localStorage.getItem("accessToken");
    const userData = localStorage.getItem("user");
    if (token && userData) {
      setIsAuthenticated(true);
      setUser(JSON.parse(userData));
    }
  }, []);

  const login = async (email: string, password: string) => {
    const { default: api } = await import("../api/client");
    const response = await api.post("/auth/login", { email, password });
    const data = response.data;
    
    setChallengeId(data.challenge_id);
    // User has access to multiple tokens; store them for selection.
    if (data.tokens && data.tokens.length > 0) {
      setAvailableTokens(data.tokens);
    } else {
      setAvailableTokens(null);
    }
    
    // If MFA not set up, store secret and QR code
    if (data.mfa_secret_base32 && data.mfa_qr_base64) {
      setMfaSetup({
        secret: data.mfa_secret_base32,
        qrCode: data.mfa_qr_base64,
      });
    }
  };

  const verifyMfa = async (otp: string, selectedTokenId?: string) => {
    if (!challengeId) throw new Error("No challenge ID");
    
    const { default: api } = await import("../api/client");
    const response = await api.post("/auth/verify-otp", {
      challenge_id: challengeId,
      otp,
      selected_token_id: selectedTokenId,
    });
    const data = response.data;
    
    // Store tokens
    localStorage.setItem("accessToken", data.access_token);
    localStorage.setItem("refreshToken", data.refresh_token);
    
    // Store user info
    const userData = {
      user_id: data.user_id,
      user_email: data.user_email,
      user_name: data.user_name,
      token_deployment_id: data.token_deployment_id,
    };
    localStorage.setItem("user", JSON.stringify(userData));
    
    setUser(userData);
    setIsAuthenticated(true);
    setChallengeId(null);
    setMfaSetup(null);
    setAvailableTokens(null);
  };

  const logout = () => {
    localStorage.removeItem("accessToken");
    localStorage.removeItem("refreshToken");
    localStorage.removeItem("user");
    setIsAuthenticated(false);
    setUser(null);
    setChallengeId(null);
    setMfaSetup(null);
    setAvailableTokens(null);
  };

  return (
    <AuthContext.Provider
      value={{
        isAuthenticated,
        user,
        challengeId,
        mfaSetup,
        availableTokens,
        login,
        verifyMfa,
        logout,
      }}
    >
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error("useAuth must be used within AuthProvider");
  }
  return context;
}

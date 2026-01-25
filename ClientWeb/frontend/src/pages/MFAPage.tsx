import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { useAuth } from "../auth/AuthContext";

export default function MFAPage() {
  const [otp, setOtp] = useState("");
  const [selectedToken, setSelectedToken] = useState("");
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);
  const [step, setStep] = useState<"otp" | "token-selection">("otp");
  const navigate = useNavigate();
  const { verifyMfa, mfaSetup, availableTokens } = useAuth();

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError("");
    setLoading(true);

    try {
      // If user has multiple tokens and hasn't selected one yet, go to token selection
      if (availableTokens && availableTokens.length > 0 && step === "otp") {
        await verifyMfa(otp); // Verify OTP first
        setStep("token-selection"); // Then show token selection
        setLoading(false);
        return;
      }
      
      // Otherwise, verify MFA with selected token (or null if single token)
      await verifyMfa(otp, selectedToken || undefined);
      navigate("/dashboard");
    } catch (err: any) {
      setError(err.response?.data?.detail || "Invalid OTP code");
    } finally {
      setLoading(false);
    }
  };
  
  const handleTokenSelect = async () => {
    if (!selectedToken) {
      setError("Please select a token");
      return;
    }
    
    setError("");
    setLoading(true);
    
    try {
      await verifyMfa(otp, selectedToken);
      navigate("/dashboard");
    } catch (err: any) {
      setError(err.response?.data?.detail || "Failed to authenticate");
    } finally {
      setLoading(false);
    }
  };
  
  // Token selection step
  if (step === "token-selection" && availableTokens && availableTokens.length > 0) {
    return (
      <div className="mfa-page">
        <div className="mfa-container">
          <div className="mfa-card">
            <h1>Select Token</h1>
            <p className="text-gray-600 mb-4">
              You have access to multiple tokens. Please select which token you want to access:
            </p>
            
            {error && <div className="error-message">{error}</div>}
            
            <div className="token-list">
              {availableTokens.map((token) => (
                <div
                  key={token.token_id}
                  className={`token-card ${selectedToken === token.token_id ? "selected" : ""}`}
                  onClick={() => setSelectedToken(token.token_id)}
                >
                  <div className="token-info">
                    <h3>{token.token_name}</h3>
                    {token.contract_address && (
                      <p className="text-sm text-gray-500">{token.contract_address}</p>
                    )}
                  </div>
                  <div className="token-radio">
                    <input
                      type="radio"
                      checked={selectedToken === token.token_id}
                      onChange={() => setSelectedToken(token.token_id)}
                    />
                  </div>
                </div>
              ))}
            </div>
            
            <button
              onClick={handleTokenSelect}
              disabled={loading || !selectedToken}
              className="btn-primary mt-4"
            >
              {loading ? "Authenticating..." : "Continue"}
            </button>
            
            <div className="mfa-footer">
              <button
                onClick={() => navigate("/login")}
                className="btn-link"
              >
                Back to Login
              </button>
            </div>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="mfa-page">
      <div className="mfa-container">
        <div className="mfa-card">
          <h1>Two-Factor Authentication</h1>
          
          {mfaSetup && (
            <div className="mfa-setup">
              <h3>First-time Setup</h3>
              <p>Scan this QR code with your authenticator app (e.g., Google Authenticator, Authy):</p>
              
              <div className="qr-code">
                <img
                  src={`data:image/png;base64,${mfaSetup.qrCode}`}
                  alt="MFA QR Code"
                />
              </div>
              
              <div className="mfa-secret">
                <p>Or enter this code manually:</p>
                <code>{mfaSetup.secret}</code>
              </div>
            </div>
          )}
          
          <form onSubmit={handleSubmit}>
            {error && <div className="error-message">{error}</div>}
            
            <div className="form-group">
              <label>Enter 6-digit code from your authenticator app</label>
              <input
                type="text"
                value={otp}
                onChange={(e) => setOtp(e.target.value.replace(/\D/g, ""))}
                required
                disabled={loading}
                placeholder="000000"
                maxLength={6}
                pattern="\d{6}"
                className="otp-input"
              />
            </div>
            
            <button type="submit" disabled={loading || otp.length !== 6} className="btn-primary">
              {loading ? "Verifying..." : "Verify"}
            </button>
          </form>
          
          <div className="mfa-footer">
            <button
              onClick={() => navigate("/login")}
              className="btn-link"
            >
              Back to Login
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}

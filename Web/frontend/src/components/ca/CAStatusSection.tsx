import { useState, useEffect } from "react";
import { caApi, type CAStatus } from "../../api/ca";
import { useAuth } from "../../auth/useAuth";
import Button from "../../components/Button";

export default function CAStatusSection() {
  const [status, setStatus] = useState<CAStatus | null>(null);
  const [loading, setLoading] = useState(true);
  const [generating, setGenerating] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);
  const { token } = useAuth();

  const loadStatus = async () => {
    if (!token) return;
    setLoading(true);
    setError(null);
    try {
      const data = await caApi.getStatus(token);
      setStatus(data);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to load CA status");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadStatus();
  }, [token]);

  const handleGenerate = async () => {
    if (!token) return;
    
    const confirmed = window.confirm(
      "Are you sure you want to generate a new Certificate Authority?\n\n" +
      "This will create a new 5-year CA certificate. This action cannot be undone.\n\n" +
      "Click OK to proceed."
    );

    if (!confirmed) return;

    setGenerating(true);
    setError(null);
    setSuccess(null);

    try {
      const result = await caApi.generate(token);
      setSuccess(result.message);
      await loadStatus();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to generate CA");
    } finally {
      setGenerating(false);
    }
  };

  const handleDownload = async () => {
    if (!token || !status?.ca_certificate) return;
    
    try {
      const result = await caApi.downloadCertificate(token);
      const blob = new Blob([result.ca_certificate], { type: "text/plain" });
      const url = URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = result.filename || "aegismint-ca.pem";
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
      URL.revokeObjectURL(url);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to download certificate");
    }
  };

  if (loading) {
    return (
      <div className="bg-white rounded-lg border border-gray-200 p-6">
        <h2 className="text-xl font-semibold mb-4">Certificate Authority</h2>
        <p className="text-gray-600">Loading...</p>
      </div>
    );
  }

  return (
    <div className="bg-white rounded-lg border border-gray-200 p-6">
      <h2 className="text-xl font-semibold mb-4">Certificate Authority</h2>

      {error && (
        <div className="mb-4 p-4 bg-red-50 border border-red-200 rounded text-red-800">
          {error}
        </div>
      )}

      {success && (
        <div className="mb-4 p-4 bg-green-50 border border-green-200 rounded text-green-800">
          {success}
        </div>
      )}

      {!status?.exists ? (
        <div className="space-y-4">
          <div className="p-4 bg-yellow-50 border border-yellow-200 rounded">
            <div className="flex items-start">
              <span className="text-2xl mr-3">⚠️</span>
              <div>
                <h3 className="font-semibold text-yellow-900 mb-2">No Certificate Authority Found</h3>
                <p className="text-yellow-800 text-sm">
                  Generate a Certificate Authority to enable certificate-based authentication for desktop applications.
                  The CA will be valid for 5 years.
                </p>
              </div>
            </div>
          </div>

          <Button onClick={handleGenerate} disabled={generating}>
            {generating ? "Generating..." : "Generate Certificate Authority"}
          </Button>
        </div>
      ) : (
        <div className="space-y-4">
          <div className="p-4 bg-green-50 border border-green-200 rounded">
            <div className="flex items-start">
              <span className="text-2xl mr-3">✅</span>
              <div className="flex-1">
                <h3 className="font-semibold text-green-900 mb-3">CA Active</h3>
                
                <div className="space-y-2 text-sm">
                  <div className="flex justify-between">
                    <span className="text-gray-600">Created:</span>
                    <span className="font-medium">
                      {status.created_at ? new Date(status.created_at).toLocaleDateString() : "N/A"}
                    </span>
                  </div>
                  
                  <div className="flex justify-between">
                    <span className="text-gray-600">Expires:</span>
                    <span className="font-medium">
                      {status.expires_at ? new Date(status.expires_at).toLocaleDateString() : "N/A"}
                    </span>
                  </div>

                  {status.days_until_expiry !== undefined && (
                    <div className="flex justify-between">
                      <span className="text-gray-600">Days Remaining:</span>
                      <span className={`font-medium ${status.expiring_soon ? "text-orange-600" : ""}`}>
                        {status.days_until_expiry} days
                      </span>
                    </div>
                  )}

                  {status.subject && (
                    <div className="flex justify-between">
                      <span className="text-gray-600">Subject:</span>
                      <span className="font-mono text-xs">{status.subject}</span>
                    </div>
                  )}
                </div>
              </div>
            </div>
          </div>

          {status.expiring_soon && (
            <div className="p-4 bg-orange-50 border border-orange-200 rounded">
              <div className="flex items-start">
                <span className="text-xl mr-3">⚠️</span>
                <div>
                  <h4 className="font-semibold text-orange-900 mb-1">Expiring Soon</h4>
                  <p className="text-sm text-orange-800">
                    CA certificate expires in {status.days_until_expiry} days. Consider generating a new CA before expiration.
                  </p>
                </div>
              </div>
            </div>
          )}

          <div className="flex gap-3">
            <Button onClick={handleDownload} variant="ghost">
              Download CA Certificate
            </Button>
          </div>
        </div>
      )}
    </div>
  );
}

import { useState } from "react";

import Button from "../../components/Button";
import Toast from "../../components/Toast";
import { useAuth } from "../../auth/useAuth";

type RecoveryResult = {
  mnemonic: string;
  token_address?: string;
};

export default function RecoverSharesPage() {
  const { token } = useAuth();
  const [files, setFiles] = useState<File[]>([]);
  const [loading, setLoading] = useState(false);
  const [result, setResult] = useState<RecoveryResult | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [toast, setToast] = useState<{ message: string; type: "success" | "error" } | null>(null);
  const [isDragging, setIsDragging] = useState(false);

  const handleFileSelect = (e: React.ChangeEvent<HTMLInputElement>) => {
    if (e.target.files) {
      const selectedFiles = Array.from(e.target.files);
      
      // Validate JSON files
      const jsonFiles = selectedFiles.filter(f => f.name.endsWith('.json'));
      if (jsonFiles.length !== selectedFiles.length) {
        setError("Only JSON files are allowed");
        return;
      }
      
      if (jsonFiles.length < 2) {
        setError("Please select at least 2 share files");
        return;
      }
      
      if (jsonFiles.length > 3) {
        setError("Maximum 3 share files allowed");
        return;
      }
      
      setFiles(jsonFiles);
      setError(null);
    }
  };

  const handleDragOver = (e: React.DragEvent) => {
    e.preventDefault();
    setIsDragging(true);
  };

  const handleDragLeave = () => {
    setIsDragging(false);
  };

  const handleDrop = (e: React.DragEvent) => {
    e.preventDefault();
    setIsDragging(false);
    
    const droppedFiles = Array.from(e.dataTransfer.files);
    const jsonFiles = droppedFiles.filter(f => f.name.endsWith('.json'));
    
    if (jsonFiles.length !== droppedFiles.length) {
      setError("Only JSON files are allowed");
      return;
    }
    
    if (jsonFiles.length < 2) {
      setError("Please drop at least 2 share files");
      return;
    }
    
    if (jsonFiles.length > 3) {
      setError("Maximum 3 share files allowed");
      return;
    }
    
    setFiles(jsonFiles);
    setError(null);
  };

  const handleRecover = async () => {
    if (!token || files.length < 2) {
      setError("Please select at least 2 share files");
      return;
    }

    setLoading(true);
    setError(null);
    setResult(null);

    try {
      const formData = new FormData();
      files.forEach(file => {
        formData.append('files', file);
      });

      const response = await fetch('/api/admin/shares/recover', {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${token}`,
        },
        body: formData,
      });

      if (!response.ok) {
        const errorData = await response.json();
        throw new Error(errorData.detail || 'Recovery failed');
      }

      const data: RecoveryResult = await response.json();
      setResult(data);
      setToast({ message: "Mnemonic recovered successfully!", type: "success" });
    } catch (err) {
      const errorMsg = err instanceof Error ? err.message : "Failed to recover mnemonic";
      setError(errorMsg);
      setToast({ message: errorMsg, type: "error" });
    } finally {
      setLoading(false);
    }
  };

  const handleCopyMnemonic = () => {
    if (result?.mnemonic) {
      navigator.clipboard.writeText(result.mnemonic);
      setToast({ message: "Mnemonic copied to clipboard", type: "success" });
    }
  };

  const handleReset = () => {
    setFiles([]);
    setResult(null);
    setError(null);
  };

  return (
    <>
      <div className="stack">
        <div>
          <h3>Recover Mnemonic from Shares</h3>
          <p className="muted">
            Upload 2 or 3 share files to recover the encrypted mnemonic. All recovery attempts are logged.
          </p>
        </div>

        {!result ? (
          <>
            {/* File Upload Area */}
            <div
              className={`file-upload-zone ${isDragging ? 'dragging' : ''}`}
              onDragOver={handleDragOver}
              onDragLeave={handleDragLeave}
              onDrop={handleDrop}
            >
              <div className="upload-content">
                <svg width="48" height="48" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                  <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4" />
                  <polyline points="17 8 12 3 7 8" />
                  <line x1="12" y1="3" x2="12" y2="15" />
                </svg>
                <p>
                  <strong>Drag & drop share files here</strong>
                </p>
                <p className="muted">or</p>
                <label className="file-label" onClick={(e) => {
                  const input = e.currentTarget.querySelector('input');
                  if (input) input.click();
                }}>
                  <input
                    type="file"
                    accept=".json"
                    multiple
                    onChange={handleFileSelect}
                    style={{ display: 'none' }}
                  />
                  <span style={{ pointerEvents: 'none' }}>
                    <Button tone="secondary">Browse Files</Button>
                  </span>
                </label>
                <p className="muted" style={{ marginTop: '0.5rem' }}>
                  Select 2-3 JSON share files
                </p>
              </div>
            </div>

            {/* Selected Files List */}
            {files.length > 0 && (
              <div className="card">
                <div className="hd">
                  <h4>Selected Files ({files.length})</h4>
                </div>
                <div className="bd">
                  <ul className="file-list">
                    {files.map((file, idx) => (
                      <li key={idx}>
                        <span>{file.name}</span>
                        <span className="muted">{(file.size / 1024).toFixed(2)} KB</span>
                      </li>
                    ))}
                  </ul>
                </div>
              </div>
            )}

            {error && (
              <div className="alert alert-error">
                {error}
              </div>
            )}

            {/* Actions */}
            <div className="row" style={{ gap: '0.5rem' }}>
              <Button
                tone="primary"
                onClick={handleRecover}
                disabled={loading || files.length < 2}
              >
                {loading ? "Recovering..." : "Recover Mnemonic"}
              </Button>
              {files.length > 0 && (
                <Button tone="secondary" onClick={handleReset}>
                  Clear
                </Button>
              )}
            </div>
          </>
        ) : (
          <>
            {/* Recovery Result */}
            <div className="card success-card">
              <div className="hd">
                <h4>✓ Mnemonic Recovered</h4>
              </div>
              <div className="bd stack">
                {result.token_address && (
                  <div>
                    <label className="label">Token Address:</label>
                    <code className="code-block">{result.token_address}</code>
                  </div>
                )}
                <div>
                  <label className="label">Mnemonic Phrase:</label>
                  <textarea
                    className="mnemonic-display"
                    value={result.mnemonic}
                    readOnly
                    rows={3}
                  />
                </div>
                <div className="row" style={{ gap: '0.5rem' }}>
                  <Button tone="primary" onClick={handleCopyMnemonic}>
                    Copy to Clipboard
                  </Button>
                  <Button tone="secondary" onClick={handleReset}>
                    Recover Another
                  </Button>
                </div>
                <p className="muted" style={{ fontSize: '0.875rem' }}>
                  ⚠️ Keep this mnemonic secure. Do not share it with anyone.
                </p>
              </div>
            </div>
          </>
        )}
      </div>

      {toast && (
        <Toast message={toast.message} type={toast.type} onClose={() => setToast(null)} />
      )}

      <style>{`
        .file-upload-zone {
          border: 2px dashed var(--border);
          border-radius: 8px;
          padding: 3rem 2rem;
          text-align: center;
          transition: all 0.2s;
          background: var(--bg-subtle);
        }

        .file-upload-zone.dragging {
          border-color: var(--primary);
          background: var(--primary-subtle);
        }

        .upload-content {
          display: flex;
          flex-direction: column;
          align-items: center;
          gap: 0.75rem;
        }

        .upload-content svg {
          color: var(--muted);
        }

        .file-label {
          display: inline-block;
        }

        .file-list {
          list-style: none;
          padding: 0;
          margin: 0;
        }

        .file-list li {
          display: flex;
          justify-content: space-between;
          padding: 0.75rem;
          border-bottom: 1px solid var(--border);
        }

        .file-list li:last-child {
          border-bottom: none;
        }

        .mnemonic-display {
          width: 100%;
          padding: 1rem;
          border: 1px solid var(--border);
          border-radius: 6px;
          background: var(--bg);
          font-family: monospace;
          font-size: 0.9rem;
          resize: none;
        }

        .code-block {
          display: block;
          padding: 0.75rem;
          background: var(--bg);
          border: 1px solid var(--border);
          border-radius: 6px;
          font-family: monospace;
          font-size: 0.875rem;
          word-break: break-all;
        }

        .success-card {
          border: 2px solid var(--success);
        }

        .label {
          display: block;
          font-weight: 600;
          margin-bottom: 0.5rem;
          color: var(--text);
        }

        .alert {
          padding: 1rem;
          border-radius: 6px;
          border-left: 4px solid;
        }

        .alert-error {
          background: var(--danger-subtle);
          border-color: var(--danger);
          color: var(--danger);
        }
      `}</style>
    </>
  );
}

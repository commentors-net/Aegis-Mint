import { useEffect, useState } from "react";

type VersionInfo = {
  version: string;
  buildDate: string;
  buildNumber: number;
};

export default function VersionCheck() {
  const [showUpdate, setShowUpdate] = useState(false);
  const [currentVersion, setCurrentVersion] = useState<VersionInfo | null>(null);

  useEffect(() => {
    // Get initial version
    fetch('/governance/version.json?t=' + Date.now())
      .then(res => res.json())
      .then(data => {
        setCurrentVersion(data);
        localStorage.setItem('app-version', JSON.stringify(data));
        // Update document title with version
        document.title = `Aegis Token Control Governance v${data.version}`;
      })
      .catch(() => {});

    // Check for updates every 5 minutes
    const interval = setInterval(() => {
      fetch('/governance/version.json?t=' + Date.now())
        .then(res => res.json())
        .then((data: VersionInfo) => {
          const stored = localStorage.getItem('app-version');
          if (stored) {
            const storedVersion: VersionInfo = JSON.parse(stored);
            // Check if version or build number changed
            if (
              data.version !== storedVersion.version ||
              data.buildNumber !== storedVersion.buildNumber
            ) {
              setShowUpdate(true);
            }
          }
        })
        .catch(() => {});
    }, 5 * 60 * 1000); // 5 minutes

    return () => clearInterval(interval);
  }, []);

  const handleRefresh = () => {
    // Clear all caches and reload
    if ('caches' in window) {
      caches.keys().then(names => {
        names.forEach(name => caches.delete(name));
      });
    }
    window.location.reload();
  };

  if (!showUpdate) return null;

  return (
    <div
      style={{
        position: 'fixed',
        top: '20px',
        left: '50%',
        transform: 'translateX(-50%)',
        zIndex: 10000,
        background: 'rgba(124, 92, 255, 0.95)',
        border: '1px solid rgba(124, 92, 255, 1)',
        borderRadius: '12px',
        padding: '1rem 1.5rem',
        boxShadow: '0 12px 30px rgba(0, 0, 0, 0.3)',
        display: 'flex',
        alignItems: 'center',
        gap: '1rem',
      }}
    >
      <div style={{ flex: 1, color: '#fff', fontWeight: 500, fontSize: '14px' }}>
        🎉 A new version is available! Please refresh to get the latest updates.
      </div>
      <button
        onClick={handleRefresh}
        style={{
          background: '#fff',
          color: 'rgba(124, 92, 255, 1)',
          border: 'none',
          borderRadius: '8px',
          padding: '8px 16px',
          fontWeight: 600,
          cursor: 'pointer',
          fontSize: '14px',
        }}
      >
        Refresh Now
      </button>
      <button
        onClick={() => setShowUpdate(false)}
        style={{
          background: 'transparent',
          border: 'none',
          color: '#fff',
          cursor: 'pointer',
          fontSize: '20px',
          padding: '0',
          lineHeight: 1,
          opacity: 0.8,
        }}
      >
        ×
      </button>
    </div>
  );
}

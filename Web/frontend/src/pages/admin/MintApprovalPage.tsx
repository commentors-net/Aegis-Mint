import { useState, useEffect } from 'react';
import { mintApprovalApi, type MintDesktop } from '../../api/mintApproval';
import Button from '../../components/Button';
import { Table } from '../../components/Table';
import Toast from '../../components/Toast';
import { useAuth } from '../../auth/useAuth';

export default function MintApprovalPage() {
  const { token } = useAuth();
  const [desktops, setDesktops] = useState<MintDesktop[]>([]);
  const [loading, setLoading] = useState(true);
  const [toast, setToast] = useState<{ message: string; type: 'success' | 'error' } | null>(null);

  const loadDesktops = async () => {
    if (!token) return;
    try {
      setLoading(true);
      const data = await mintApprovalApi.listMintDesktops(token);
      setDesktops(data);
    } catch (error) {
      setToast({ message: 'Failed to load Mint desktops', type: 'error' });
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadDesktops();
  }, [token]);

  const handleApprove = async (desktopAppId: string) => {
    if (!token) return;
    try {
      await mintApprovalApi.approveMintDesktop(desktopAppId, 15, token);
      setToast({ message: 'Mint desktop approved successfully', type: 'success' });
      loadDesktops();
    } catch (error) {
      setToast({ message: 'Failed to approve Mint desktop', type: 'error' });
    }
  };

  const handleApproveSession = async (desktopAppId: string) => {
    if (!token) return;
    try {
      await mintApprovalApi.approveSession(desktopAppId, token);
      setToast({ message: 'Unlock session approved successfully', type: 'success' });
      loadDesktops();
    } catch (error) {
      const errorMessage = error instanceof Error ? error.message : 'Failed to approve session';
      if (errorMessage.includes('Already approved') || errorMessage.includes('409')) {
        setToast({ message: 'You have already approved this session', type: 'error' });
      } else {
        setToast({ message: errorMessage, type: 'error' });
      }
    }
  };

  const pendingDesktops = desktops.filter(d => d.status === 'Pending');
  const activeDesktops = desktops.filter(d => d.status === 'Active');

  return (
    <>
      <div className="hd">
        <div>
          <h2>Mint Approval</h2>
          <p className="muted">
            Approve Mint desktops for token minting operations. Each Mint desktop requires 1 admin approval with 15-minute unlock window.
          </p>
        </div>
        <Button onClick={loadDesktops} disabled={loading}>
          {loading ? 'Loading...' : 'Refresh'}
        </Button>
      </div>

      <div className="bd">
        {pendingDesktops.length > 0 && (
          <div style={{ marginBottom: '32px' }}>
            <h3 style={{ marginBottom: '16px', fontSize: '16px' }}>Pending Approval</h3>
            <Table>
              <thead>
                <tr>
                  <th>Desktop ID</th>
                  <th>Name</th>
                  <th>Machine Name</th>
                  <th>OS User</th>
                  <th>Version</th>
                  <th>Created</th>
                  <th>Action</th>
                </tr>
              </thead>
              <tbody>
                {pendingDesktops.map((desktop) => (
                  <tr key={desktop.id}>
                    <td>
                      <code style={{ fontSize: '12px' }}>{desktop.desktop_app_id}</code>
                    </td>
                    <td>{desktop.name_label || '—'}</td>
                    <td>{desktop.machine_name || '—'}</td>
                    <td>{desktop.os_user || '—'}</td>
                    <td>{desktop.token_control_version || '—'}</td>
                    <td>{new Date(desktop.created_at_utc).toLocaleString()}</td>
                    <td>
                      <Button
                        onClick={() => handleApprove(desktop.desktop_app_id)}
                        variant="primary"
                        size="sm"
                      >
                        Approve
                      </Button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </Table>
          </div>
        )}

        {activeDesktops.length > 0 && (
          <div>
            <h3 style={{ marginBottom: '16px', fontSize: '16px' }}>Active Mint Desktops</h3>
            <Table>
              <thead>
                <tr>
                  <th>Desktop ID</th>
                  <th>Name</th>
                  <th>Machine Name</th>
                  <th>OS User</th>
                  <th>Status</th>
                  <th>Unlock Minutes</th>
                  <th>Last Seen</th>
                  <th>Action</th>
                </tr>
              </thead>
              <tbody>
                {activeDesktops.map((desktop) => (
                  <tr key={desktop.id}>
                    <td>
                      <code style={{ fontSize: '12px' }}>{desktop.desktop_app_id}</code>
                    </td>
                    <td>{desktop.name_label || '—'}</td>
                    <td>{desktop.machine_name || '—'}</td>
                    <td>{desktop.os_user || '—'}</td>
                    <td>
                      <span className="pill pill-good">{desktop.status}</span>
                    </td>
                    <td>{desktop.unlock_minutes} min</td>
                    <td>
                      {desktop.last_seen_at_utc
                        ? new Date(desktop.last_seen_at_utc).toLocaleString()
                        : '—'}
                    </td>
                    <td>
                      <Button
                        onClick={() => handleApproveSession(desktop.desktop_app_id)}
                        variant="primary"
                        size="sm"
                      >
                        Approve Unlock
                      </Button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </Table>
          </div>
        )}

        {desktops.length === 0 && !loading && (
          <p className="muted" style={{ textAlign: 'center', padding: '40px 0' }}>
            No Mint desktops registered yet.
          </p>
        )}
      </div>

      {toast && (
        <Toast
          message={toast.message}
          type={toast.type}
          onClose={() => setToast(null)}
        />
      )}
    </>
  );
}

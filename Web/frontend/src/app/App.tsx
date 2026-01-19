import { Navigate, Route, Routes } from "react-router-dom";

import ProtectedRoute from "../auth/ProtectedRoute";
import LoginPage from "../auth/LoginPage";
import MFAPage from "../auth/MFAPage";
import { useAuth } from "../auth/useAuth";
import AdminLayout from "../layouts/AdminLayout";
import GovLayout from "../layouts/GovLayout";
import AuthoritiesPage from "../pages/admin/AuthoritiesPage";
import DesktopsPage from "../pages/admin/DesktopsPage";
import AssignDesktopsPage from "../pages/admin/AssignDesktopsPage";
import AuditLogsPage from "../pages/admin/AuditLogsPage";
import SystemSettingsPage from "../pages/admin/SystemSettingsPage";
import CertificateAuthorityPage from "../pages/admin/CertificateAuthorityPage";
import RecoverSharesPage from "../pages/admin/RecoverSharesPage";
import AssignedDesktopsPage from "../pages/governance/AssignedDesktopsPage";
import DesktopDetailPage from "../pages/governance/DesktopDetailPage";
import VersionCheck from "../components/VersionCheck";

export default function App() {
  const { role } = useAuth();

  const redirectForRole = role === "SuperAdmin" ? "/admin" : "/gov";

  return (
    <>
      <VersionCheck />
      <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route path="/mfa" element={<MFAPage />} />

      <Route
        path="/admin"
        element={
          <ProtectedRoute role="SuperAdmin">
            <AdminLayout />
          </ProtectedRoute>
        }
      >
        <Route index element={<Navigate to="authorities" replace />} />
        <Route path="authorities" element={<AuthoritiesPage />} />
        <Route path="desktops" element={<DesktopsPage />} />
        <Route path="assign-desktops" element={<AssignDesktopsPage />} />
        <Route path="audit" element={<AuditLogsPage />} />
        <Route path="settings" element={<SystemSettingsPage />} />
        <Route path="certificates" element={<CertificateAuthorityPage />} />
        <Route path="recover-shares" element={<RecoverSharesPage />} />
      </Route>

      <Route
        path="/gov"
        element={
          <ProtectedRoute role="GovernanceAuthority">
            <GovLayout />
          </ProtectedRoute>
        }
      >
        <Route index element={<Navigate to="assigned" replace />} />
        <Route path="assigned" element={<AssignedDesktopsPage />} />
        <Route path="desktops/:desktopAppId/:appType" element={<DesktopDetailPage />} />
      </Route>

      <Route path="*" element={<Navigate to={role ? redirectForRole : "/login"} replace />} />
      </Routes>
    </>
  );
}

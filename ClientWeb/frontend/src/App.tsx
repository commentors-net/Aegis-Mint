import { Navigate, Route, Routes } from "react-router-dom";
import { useAuth } from "./auth/AuthContext";
import LoginPage from "./pages/LoginPage";
import MFAPage from "./pages/MFAPage";
import DashboardPage from "./pages/DashboardPage";
import ProtectedRoute from "./auth/ProtectedRoute";

export default function App() {
  const { isAuthenticated } = useAuth();

  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route path="/mfa" element={<MFAPage />} />
      
      <Route
        path="/dashboard"
        element={
          <ProtectedRoute>
            <DashboardPage />
          </ProtectedRoute>
        }
      />
      
      <Route
        path="*"
        element={<Navigate to={isAuthenticated ? "/dashboard" : "/login"} replace />}
      />
    </Routes>
  );
}

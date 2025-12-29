import { Navigate, useLocation } from "react-router-dom";
import { ReactNode } from "react";

import { Role, useAuth } from "./useAuth";

export default function ProtectedRoute({ role, children }: { role?: Role; children: ReactNode }) {
  const { token, role: currentRole } = useAuth();
  const location = useLocation();

  if (!token) {
    return <Navigate to="/login" replace state={{ from: location.pathname }} />;
  }

  if (role && currentRole !== role) {
    return <Navigate to="/login" replace />;
  }

  return <>{children}</>;
}

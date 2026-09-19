import React from "react";
import ReactDOM from "react-dom/client";
import { BrowserRouter, Navigate, Route, Routes } from "react-router-dom";
import { GoogleOAuthProvider } from "@react-oauth/google";
import { AuthProvider } from "./context/AuthContext";
import { LoginPage } from "./pages/LoginPage";
import { AdminLoginPage } from "./pages/AdminLoginPage";
import { DashboardPage } from "./pages/DashboardPage";
import { ProtectedRoute } from "./routes/ProtectedRoute";

import { AccountPage } from "./pages/AccountPage";
import { AdminDashboardPage } from "./pages/AdminDashboardPage";
import { SearchPage } from "./pages/SearchPage";
import { ProviderDetailPage } from "./pages/ProviderDetailPage";
import { OrdersPage } from "./pages/OrdersPage";
import { TrustPage } from "./pages/TrustPage";
import { ProviderWizardPage } from "./pages/admin/ProviderWizardPage";
import { AgencyPage } from "./pages/admin/AgencyPage";
import { CatalogPage } from "./pages/admin/CatalogPage";
import { AdminOrdersPage } from "./pages/admin/AdminOrdersPage";
import { AuditPage } from "./pages/admin/AuditPage";
import "./styles/tokens.css";
import "./styles.css";
const configured = import.meta.env.VITE_API_BASE_URL;
ReactDOM.createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    {!configured ? (
      <main className="loading">
        <h1>Konfigurasi belum lengkap</h1>
        <p>Isi VITE_API_BASE_URL pada .env lalu jalankan ulang frontend.</p>
      </main>
    ) : (
      <GoogleOAuthProvider clientId={import.meta.env.VITE_GOOGLE_CLIENT_ID}>
        <BrowserRouter>
          <AuthProvider>
            <Routes>
              <Route path="/login" element={<LoginPage />} />
              <Route path="/admin/login" element={<AdminLoginPage />} />
              <Route path="/" element={<DashboardPage />} />
              <Route path="/dashboard" element={<Navigate to="/" replace />} />
              <Route
                path="/categories"
                element={<Navigate to="/search" replace />}
              />
              <Route path="/search" element={<SearchPage />} />
              <Route path="/providers/:id" element={<ProviderDetailPage />} />
              <Route path="/orders" element={<OrdersPage />} />
              <Route path="/account" element={<AccountPage />} />
              <Route path="/trust" element={<TrustPage />} />
              <Route
                path="/onboarding/*"
                element={<Navigate to="/" replace />}
              />
              <Route element={<ProtectedRoute />}>
                <Route path="/admin" element={<AdminDashboardPage />} />
                <Route
                  path="/admin/providers/:id"
                  element={<ProviderWizardPage />}
                />
                <Route path="/admin/orders" element={<AdminOrdersPage />} />
              </Route>
              <Route element={<ProtectedRoute platform />}>
                <Route path="/admin/agencies" element={<AgencyPage />} />
                <Route path="/admin/catalog" element={<CatalogPage />} />
                <Route path="/admin/audit" element={<AuditPage />} />
              </Route>
              <Route path="*" element={<Navigate to="/" replace />} />
            </Routes>
          </AuthProvider>
        </BrowserRouter>
      </GoogleOAuthProvider>
    )}
  </React.StrictMode>,
);

import React, {
  Suspense,
  lazy,
  type ComponentType,
} from "react";
import ReactDOM from "react-dom/client";
import { BrowserRouter, Navigate, Route, Routes } from "react-router-dom";
import { GoogleOAuthProvider } from "@react-oauth/google";
import { AuthProvider } from "./context/AuthContext";
import { LoginPage } from "./pages/LoginPage";
import { ProviderLoginPage } from "./pages/ProviderLoginPage";
import { ProviderRegisterPage } from "./pages/ProviderRegisterPage";
import { DashboardPage } from "./pages/DashboardPage";
import { ProtectedRoute } from "./routes/ProtectedRoute";

import { AccountPage } from "./pages/AccountPage";
import { SearchPage } from "./pages/SearchPage";
import { ProviderDetailPage } from "./pages/ProviderDetailPage";
import { OrdersPage } from "./pages/OrdersPage";
import { TrustPage } from "./pages/TrustPage";
import { ProviderPanelPage } from "./pages/ProviderPanelPage";
import { ProviderOnboardingPage } from "./pages/ProviderOnboardingPage";
import { AdminRoute, PlatformAdminRoute } from "./routes/AdminRoute";
import { AdminForbiddenPage } from "./pages/admin/AdminForbiddenPage";
import { AdminNotFoundPage } from "./pages/admin/AdminNotFoundPage";
import { Spinner } from "./components/ui";
import "./styles/tokens.css";
import "./styles.css";

const adminLogin = () => import("./pages/AdminLoginPage").then((m) => ({
  default: m.AdminLoginPage as ComponentType,
}));
const adminDashboard = () => import("./pages/AdminDashboardPage").then((m) => ({
  default: m.AdminDashboardPage as ComponentType,
}));
const providerWizard = () =>
  import("./pages/admin/ProviderWizardPage").then((m) => ({
    default: m.ProviderWizardPage as ComponentType,
  }));
const adminOrders = () => import("./pages/admin/AdminOrdersPage").then((m) => ({
  default: m.AdminOrdersPage as ComponentType,
}));
const reviews = () => import("./pages/admin/ReviewsPage").then((m) => ({
  default: m.ReviewsPage as ComponentType,
}));
const agencies = () => import("./pages/admin/AgencyPage").then((m) => ({
  default: m.AgencyPage as ComponentType,
}));
const catalog = () => import("./pages/admin/CatalogPage").then((m) => ({
  default: m.CatalogPage as ComponentType,
}));
const audit = () => import("./pages/admin/AuditPage").then((m) => ({
  default: m.AuditPage as ComponentType,
}));

const AdminLoginPage = lazy(adminLogin);
const AdminDashboardPage = lazy(adminDashboard);
const ProviderWizardPage = lazy(providerWizard);
const AdminOrdersPage = lazy(adminOrders);
const ReviewsPage = lazy(reviews);
const AgencyPage = lazy(agencies);
const CatalogPage = lazy(catalog);
const AuditPage = lazy(audit);

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
            <Suspense
              fallback={
                <main className="loading">
                  <Spinner /> Memuat…
                </main>
              }
            >
              <Routes>
                <Route path="/login" element={<LoginPage />} />
                <Route path="/provider/login" element={<ProviderLoginPage />} />
                <Route
                  path="/provider/register"
                  element={<ProviderRegisterPage />}
                />
                <Route path="/" element={<DashboardPage />} />
                <Route path="/dashboard" element={<Navigate to="/" replace />} />
                <Route
                  path="/categories"
                  element={<Navigate to="/search" replace />}
                />
                <Route path="/search" element={<SearchPage />} />
                <Route
                  path="/providers/:id"
                  element={<ProviderDetailPage />}
                />
                <Route path="/orders" element={<OrdersPage />} />
                <Route path="/account" element={<AccountPage />} />
                <Route path="/trust" element={<TrustPage />} />
                <Route
                  path="/onboarding/*"
                  element={<Navigate to="/" replace />}
                />
                <Route path="/admin/login" element={<AdminLoginPage />} />
                <Route
                  path="/admin"
                  element={<Navigate to="/admin/dashboard" replace />}
                />
                <Route element={<AdminRoute />}>
                  <Route
                    path="/admin/dashboard"
                    element={<AdminDashboardPage />}
                  />
                  <Route
                    path="/admin/providers/:id"
                    element={<ProviderWizardPage />}
                  />
                  <Route path="/admin/orders" element={<AdminOrdersPage />} />
                  <Route path="/admin/reviews" element={<ReviewsPage />} />
                </Route>
                <Route element={<PlatformAdminRoute />}>
                  <Route path="/admin/agencies" element={<AgencyPage />} />
                  <Route path="/admin/catalog" element={<CatalogPage />} />
                  <Route path="/admin/content" element={<CatalogPage />} />
                  <Route path="/admin/audit" element={<AuditPage />} />
                </Route>
                <Route element={<AdminRoute />}>
                  <Route path="/admin/*" element={<AdminNotFoundPage />} />
                </Route>
                <Route element={<ProtectedRoute role="Provider" />}>
                  <Route
                    path="/provider"
                    element={<Navigate to="/provider/dashboard" replace />}
                  />
                  <Route
                    path="/provider/dashboard"
                    element={<ProviderPanelPage />}
                  />
                  <Route
                    path="/provider/onboarding"
                    element={<ProviderOnboardingPage />}
                  />
                </Route>
                <Route path="*" element={<Navigate to="/" replace />} />
              </Routes>
            </Suspense>
          </AuthProvider>
        </BrowserRouter>
      </GoogleOAuthProvider>
    )}
  </React.StrictMode>,
);
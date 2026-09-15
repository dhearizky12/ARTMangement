import React from "react";
import ReactDOM from "react-dom/client";
import { BrowserRouter, Navigate, Route, Routes } from "react-router-dom";
import { GoogleOAuthProvider } from "@react-oauth/google";
import { AuthProvider } from "./context/AuthContext";
import { LoginPage } from "./pages/LoginPage";
import { AdminLoginPage } from "./pages/AdminLoginPage";
import { DashboardPage } from "./pages/DashboardPage";
import { ProtectedRoute } from "./routes/ProtectedRoute";
import { ProfileBoundary } from "./routes/ProfileBoundary";
import { CategoriesPage } from "./pages/CategoriesPage";
import { AccountPage } from "./pages/AccountPage";
import { AdminDashboardPage } from "./pages/AdminDashboardPage";
import { PersonalInfoStep } from "./pages/wizard/PersonalInfoStep";
import { AddressStep } from "./pages/wizard/AddressStep";
import { DocumentsStep } from "./pages/wizard/DocumentsStep";
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
              <Route element={<ProtectedRoute role="User" />}>
                <Route element={<ProfileBoundary />}>
                  <Route path="/dashboard" element={<DashboardPage />} />
                  <Route path="/categories" element={<CategoriesPage />} />
                  <Route path="/account" element={<AccountPage />} />
                  <Route
                    path="/onboarding/personal-info"
                    element={<PersonalInfoStep />}
                  />
                  <Route path="/onboarding/address" element={<AddressStep />} />
                  <Route
                    path="/onboarding/documents"
                    element={<DocumentsStep />}
                  />
                </Route>
              </Route>
              <Route element={<ProtectedRoute role="Admin" />}>
                <Route path="/admin" element={<AdminDashboardPage />} />
              </Route>
              <Route path="*" element={<Navigate to="/login" replace />} />
            </Routes>
          </AuthProvider>
        </BrowserRouter>
      </GoogleOAuthProvider>
    )}
  </React.StrictMode>,
);

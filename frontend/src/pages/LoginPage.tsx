import { ShieldCheck, ArrowUpRight } from "lucide-react";
import { Badge } from "../components/ui";
import { useState } from "react";
import { GoogleLogin } from "@react-oauth/google";
import { Link, Navigate } from "react-router-dom";
import { authApi } from "../api/authApi";
import { useAuth } from "../context/AuthContext";
import { AuthLayout } from "../components/AuthLayout";
export function LoginPage() {
  const { session, loading, error: connectionError } = useAuth();
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  if (session)
    return (
      <Navigate
        to={session.user.role === "Admin" ? "/admin" : "/dashboard"}
        replace
      />
    );
  return (
    <AuthLayout>
      <div className="form-card">
        <Badge tone="success">SELAMAT DATANG</Badge>
        <h2>Senang bertemu Anda.</h2>
        <p className="subtitle">
          Masuk untuk memulai perjalanan baik Anda
          <br className="desktop" /> bersama Bantu-Bantu.
        </p>
        <div className="google-box" aria-busy={busy || loading}>
          {!import.meta.env.VITE_GOOGLE_CLIENT_ID ? (
            <p>Login Google belum dikonfigurasi.</p>
          ) : loading ? (
            <p>Memeriksa sesi…</p>
          ) : busy ? (
            <p>Menyiapkan akun Anda…</p>
          ) : (
            <GoogleLogin
              onSuccess={async ({ credential }) => {
                if (!credential) {
                  setError("Google tidak memberikan token login.");
                  return;
                }
                setBusy(true);
                setError("");
                try {
                  await authApi.google(credential);
                } catch (e) {
                  setError(e instanceof Error ? e.message : "Login gagal.");
                } finally {
                  setBusy(false);
                }
              }}
              onError={() =>
                setError("Login Google dibatalkan atau gagal. Coba kembali.")
              }
              text="continue_with"
              shape="rectangular"
              locale="id"
            />
          )}
        </div>
        {(error || connectionError) && (
          <p role="alert" className="error">
            {error || connectionError}
          </p>
        )}
        <div className="privacy">
          <ShieldCheck aria-hidden="true" />
          <p>
            Akun Google Anda digunakan untuk masuk.
            <br />
            Kami tidak pernah meminta kata sandi Google Anda.
          </p>
        </div>
        <div className="divider" />
        <p className="admin-link">
          Mengelola platform?{" "}
          <Link to="/admin/login">
            Masuk sebagai admin <ArrowUpRight aria-hidden="true" />
          </Link>
        </p>
      </div>
    </AuthLayout>
  );
}

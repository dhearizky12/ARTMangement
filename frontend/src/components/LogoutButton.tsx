import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { authApi } from "../api/authApi";
import { Button, Spinner } from "./ui";

export function LogoutButton({
  redirectTo = "/login",
  variant = "button",
  className = "",
}: {
  redirectTo?: string;
  variant?: "button" | "text";
  className?: string;
}) {
  const navigate = useNavigate();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  async function logout() {
    setBusy(true);
    setError("");
    try {
      await authApi.logout();
      navigate(redirectTo, { replace: true });
    } catch (e) {
      setError(e instanceof Error ? e.message : "Gagal keluar.");
    } finally {
      setBusy(false);
    }
  }
  if (variant === "text")
    return (
      <>
        <button
          type="button"
          className="text-link"
          disabled={busy}
          onClick={() => void logout()}
        >
          {busy ? "Keluar…" : "Keluar"}
        </button>
        {error && (
          <span className="error" role="alert">
            {error}
          </span>
        )}
      </>
    );
  return (
    <>
      <Button
        variant="secondary"
        className={className}
        disabled={busy}
        onClick={() => void logout()}
      >
        {busy && <Spinner />}
        Keluar
      </Button>
      {error && (
        <p className="error" role="alert">
          {error}
        </p>
      )}
    </>
  );
}
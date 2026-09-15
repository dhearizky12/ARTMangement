import {
  createContext,
  useContext,
  useEffect,
  useState,
  type ReactNode,
} from "react";
import { authApi, type Session } from "../api/authApi";
const AuthContext = createContext<{
  session: Session | null;
  loading: boolean;
  error: string;
}>({ session: null, loading: true, error: "" });
export function AuthProvider({ children }: { children: ReactNode }) {
  const [session, setSession] = useState<Session | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  useEffect(() => {
    authApi.onSession = setSession;
    authApi
      .refresh()
      .catch((e) => {
        if (e.status !== 401)
          setError(
            "Tidak dapat terhubung ke server. Periksa koneksi lalu muat ulang.",
          );
      })
      .finally(() => setLoading(false));
    return () => {
      authApi.onSession = () => {};
    };
  }, []);
  useEffect(() => {
    if (!session) return;
    const timer = window.setTimeout(
      () => {
        authApi.refresh().catch(() => {});
      },
      Math.max(1000, Date.parse(session.expiresAt) - Date.now() - 30000),
    );
    return () => clearTimeout(timer);
  }, [session]);
  return (
    <AuthContext.Provider value={{ session, loading, error }}>
      {children}
    </AuthContext.Provider>
  );
}
export const useAuth = () => useContext(AuthContext);

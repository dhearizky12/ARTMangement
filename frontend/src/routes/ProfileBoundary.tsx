import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useRef,
  useState,
} from "react";
import { Navigate, Outlet, useLocation } from "react-router-dom";
import { authApi } from "../api/authApi";
import {
  profileApi,
  profileRoutes,
  type ProfileStatus,
} from "../api/profileApi";
import { useAuth } from "../context/AuthContext";
import { Button, Card } from "../components/ui";
const ProfileContext = createContext<{
  status: ProfileStatus;
  sync: () => Promise<ProfileStatus>;
} | null>(null);
export function useProfile() {
  const context = useContext(ProfileContext);
  if (!context) throw new Error("Profile provider missing");
  return context;
}
export function ProfileBoundary() {
  const { session } = useAuth();
  const { pathname } = useLocation();
  const [result, setResult] = useState<{
    path: string;
    status: ProfileStatus;
  } | null>(null);
  const [error, setError] = useState("");
  const sequence = useRef(0);
  const userId = session!.user.id;
  const sync = useCallback(async () => {
    const request = ++sequence.current;
    setError("");
    try {
      const status = await profileApi.status();
      if (request === sequence.current) {
        setResult({ path: pathname, status });
        authApi.updateUser({
          profileCompleted: status.profileCompleted,
          profileStep: status.profileStep,
          ...(status.personalInfo
            ? { fullName: status.personalInfo.fullName }
            : {}),
        });
      }
      return status;
    } catch (e) {
      if (request === sequence.current)
        setError(e instanceof Error ? e.message : "Gagal memeriksa profil.");
      throw e;
    }
  }, [pathname, userId]);
  useEffect(() => {
    sync().catch(() => {});
    const reload = () => {
      setResult(null);
      sync().catch(() => {});
    };
    window.addEventListener("profile-required", reload);
    return () => {
      sequence.current++;
      window.removeEventListener("profile-required", reload);
    };
  }, [sync]);
  if (error)
    return (
      <main className="page narrow">
        <Card>
          <h1>Profil belum dapat dimuat</h1>
          <p role="alert">{error}</p>
          <Button onClick={() => sync().catch(() => {})}>Coba lagi</Button>
        </Card>
      </main>
    );
  if (!result || result.path !== pathname)
    return (
      <main className="loading" role="status">
        Memeriksa kelengkapan profil…
      </main>
    );
  const status = result.status;
  if (
    !status.profileCompleted &&
    pathname !== profileRoutes[status.profileStep]
  )
    return <Navigate to={profileRoutes[status.profileStep]} replace />;
  if (status.profileCompleted && pathname.startsWith("/onboarding"))
    return <Navigate to="/dashboard" replace />;
  return (
    <ProfileContext.Provider value={{ status, sync }}>
      <Outlet />
    </ProfileContext.Provider>
  );
}

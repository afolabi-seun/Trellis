import {
  createContext,
  useContext,
  useEffect,
  useState,
  useSyncExternalStore,
  type ReactNode,
} from "react";
import { bootstrapSession } from "../api/auth";
import { getAccessToken, setAccessToken, subscribeToAccessToken } from "../api/client";
import { decodeAccessToken, type TrellisTokenClaims } from "./tokenClaims";

interface AuthState {
  /** True while the mount-time silent-refresh attempt is still in flight (frontend_architecture.md step 3). */
  isBootstrapping: boolean;
  isAuthenticated: boolean;
  claims: TrellisTokenClaims | null;
  logout: () => void;
}

const AuthContext = createContext<AuthState | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [isBootstrapping, setIsBootstrapping] = useState(true);
  const token = useSyncExternalStore(subscribeToAccessToken, getAccessToken);

  useEffect(() => {
    bootstrapSession().finally(() => setIsBootstrapping(false));
  }, []);

  const claims = token ? decodeAccessToken(token) : null;

  const value: AuthState = {
    isBootstrapping,
    isAuthenticated: claims !== null,
    claims,
    logout: () => setAccessToken(null),
  };

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthState {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error("useAuth() must be called within an AuthProvider.");
  }
  return context;
}

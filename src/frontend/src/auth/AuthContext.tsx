import { useEffect, useState, useSyncExternalStore, type ReactNode } from "react";
import { bootstrapSession } from "../api/auth";
import { getAccessToken, setAccessToken, subscribeToAccessToken } from "../api/client";
import { decodeAccessToken } from "./tokenClaims";
import { AuthContext, type AuthState } from "./useAuth";

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

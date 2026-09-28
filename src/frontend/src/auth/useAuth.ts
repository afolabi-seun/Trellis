import { createContext, useContext } from "react";
import type { TrellisTokenClaims } from "./tokenClaims";

export interface AuthState {
  /** True while the mount-time silent-refresh attempt is still in flight (frontend_architecture.md step 3). */
  isBootstrapping: boolean;
  isAuthenticated: boolean;
  claims: TrellisTokenClaims | null;
  logout: () => void;
}

// Separate from AuthProvider's file so that file exports only a component, which React Fast
// Refresh needs to hot-reload it without a full remount.
export const AuthContext = createContext<AuthState | null>(null);

export function useAuth(): AuthState {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error("useAuth() must be called within an AuthProvider.");
  }
  return context;
}

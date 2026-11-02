/**
 * Example Expo/React Native integration.
 * Install: npx expo install expo-secure-store
 * Build and install SDK: npm install ../clients/typescript (adjust relative path).
 * Replace the sample HTTPS host with your API. Never use a service API key in a mobile app.
 */
import * as SecureStore from "expo-secure-store";
import { MkxApiClient } from "@mkx/templates-api-client";
import type { LocalTokens, SessionStore } from "@mkx/templates-api-client";

const STORAGE_KEY = "mkx.local.jwt-session";
const sessionStore: SessionStore = {
  async read(): Promise<LocalTokens | null> {
    const raw = await SecureStore.getItemAsync(STORAGE_KEY);
    if (!raw) return null;
    try {
      const value: unknown = JSON.parse(raw);
      if (typeof value !== "object" || value === null) return null;
      const credentials = value as Record<string, unknown>;
      return typeof credentials.accessToken === "string" &&
        typeof credentials.refreshToken === "string"
        ? { accessToken: credentials.accessToken, refreshToken: credentials.refreshToken }
        : null;
    } catch {
      return null;
    }
  },
  async write(next): Promise<void> {
    if (next) await SecureStore.setItemAsync(STORAGE_KEY, JSON.stringify(next));
    else await SecureStore.deleteItemAsync(STORAGE_KEY);
  }
};

export const mkx = new MkxApiClient({
  baseUrl: "https://api.example.com",
  sessionStore
});

// The caller supplies the optional six-digit TOTP code if local MFA is enabled.
export async function signIn(username: string, password: string, authenticatorCode?: string) {
  return mkx.authLogin({ username, password, authenticatorCode });
}

export async function myProfile() {
  // Reads the secure tokens and automatically rotates a refresh token on a
  // LocalJwt HTTP 401 once per simultaneous burst of requests.
  return mkx.authMe();
}

export async function clearSession() {
  const current = await sessionStore.read();
  if (current) {
    try { await mkx.authLogout({ refreshToken: current.refreshToken }); }
    finally { await sessionStore.write(null); }
  }
}

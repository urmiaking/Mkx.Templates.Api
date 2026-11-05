/** Optional real HTTP + disposable SQL Server smoke; called only by scripts/smoke-sql.ps1. */
import assert from "node:assert/strict";
import { MkxApiClient } from "../dist/index.js";

const baseUrl = process.env.MKX_SMOKE_BASE_URL;
const username = process.env.MKX_SMOKE_USERNAME;
const password = process.env.MKX_SMOKE_PASSWORD;
if (!baseUrl || !username || !password)
  throw new Error("This script requires disposable MKX_SMOKE_* environment variables");

let session = null;
const sessionStore = {
  async read() { return session; },
  async write(next) { session = next; }
};

const api = new MkxApiClient({ baseUrl, sessionStore });
const token = await api.authLogin({ username, password });
assert.ok(token.accessToken && token.refreshToken);
const initial = await api.authMe();
assert.equal(initial.username, username);

// A bad access token forces a real SQL-backed refresh; no bearer is ever
// attached to POST /api/auth/refresh by the SDK.
await sessionStore.write({ accessToken: "invalid-access-token", refreshToken: token.refreshToken });
const restored = await api.authMe();
assert.equal(restored.username, username);
const rotated = await sessionStore.read();
assert.ok(rotated?.accessToken && rotated.accessToken !== "invalid-access-token");
assert.ok(rotated.refreshToken !== token.refreshToken);

// Admin authorization is still enforced by the API; this uses a temporary
// bootstrap administrator created only in the disposable smoke database.
const users = await api.usersList(1, 1);
assert.ok(users.total >= 1);
await api.authLogout({ refreshToken: rotated.refreshToken });
await sessionStore.write(null);
console.log("TYPESCRIPT_SDK_REAL_HTTP_SQL=PASS");

import assert from "node:assert/strict";
import test from "node:test";
import { MkxApiClient, ApiHttpError, ApiCredentialsRequired } from "../dist/index.js";

const ok = (data, status = 200) => new Response(
  status === 204 ? null : JSON.stringify(data),
  { status, headers: { "Content-Type": "application/json" } }
);
const mockStore = (initial = null) => {
  let tokens = initial;
  return {
    async read() { return tokens; },
    async write(next) { tokens = next; }
  };
};

test("login uses username (never sends stale bearer) and saves only into host store", async () => {
  const seen = [];
  const store = mockStore({ accessToken: "stale", refreshToken: "old" });
  const client = new MkxApiClient({
    baseUrl: "https://api.example.test",
    sessionStore: store,
    fetcher: async (url, init) => {
      seen.push({ url, init });
      return ok({
        accessToken: "new-access", refreshToken: "new-refresh",
        accessTokenExpiresAt: new Date().toISOString(),
        refreshTokenExpiresAt: new Date().toISOString()
      });
    }
  });
  const reply = await client.authLogin({ username: "msd", password: "secret" });
  assert.equal(reply.accessToken, "new-access");
  assert.equal(seen.length, 1);
  assert.equal(seen[0].init.headers.Authorization, undefined);
  assert.equal(JSON.parse(seen[0].init.body).username, "msd");
  assert.deepEqual(await store.read(), { accessToken: "new-access", refreshToken: "new-refresh" });
});

test("parallel 401 responses refresh only once; replay requests with new local JWT", async () => {
  let refresh = 0;
  const sent = [];
  const store = mockStore({ accessToken: "expired", refreshToken: "refresh-one" });
  const client = new MkxApiClient({
    baseUrl: "https://api.example.test",
    sessionStore: store,
    fetcher: async (url, init) => {
      sent.push({ url, init });
      if (url.endsWith("/api/auth/refresh")) {
        refresh++;
        assert.equal(init.headers.Authorization, undefined);
        assert.equal(JSON.parse(init.body).refreshToken, "refresh-one");
        await new Promise(resolve => setTimeout(resolve, 12));
        return ok({ accessToken: "fresh", refreshToken: "refresh-two" });
      }
      assert.ok(url.endsWith("/api/auth/me"));
      if (init.headers.Authorization === "Bearer expired") return ok({ title: "Unauthorized" }, 401);
      assert.equal(init.headers.Authorization, "Bearer fresh");
      return ok({ id: "u", username: "msd", displayName: "User", roles: [] });
    }
  });
  const [a, b] = await Promise.all([client.authMe(), client.authMe()]);
  assert.equal(a.username, "msd");
  assert.equal(b.id, "u");
  assert.equal(refresh, 1);
  assert.deepEqual(await store.read(), { accessToken: "fresh", refreshToken: "refresh-two" });
});

test("external, opaque and API-key schemes are strictly isolated", async () => {
  const seen = [];
  const client = new MkxApiClient({
    baseUrl: "https://api.example.test",
    sessionStore: mockStore({ accessToken: "LOCAL", refreshToken: "REFRESH" }),
    externalAccessToken: async () => "EXTERNAL",
    opaqueAccessToken: () => "OPAQUE",
    apiKey: () => "SERVICE",
    fetcher: async (url, init) => {
      seen.push({ url, headers: init.headers });
      return ok({ status: "ok" });
    }
  });
  await client.externalIdentityMe();
  await client.opaqueIdentityMe();
  await client.integrationsPing();
  assert.equal(seen[0].headers.Authorization, "Bearer EXTERNAL");
  assert.equal(seen[1].headers.Authorization, "Bearer OPAQUE");
  assert.equal(seen[2].headers["X-API-Key"], "SERVICE");
  assert.equal(seen[2].headers.Authorization, undefined);
  assert.ok(seen.every(row => !JSON.stringify(row).includes("LOCAL")));
});

test("missing distinct scheme credential throws without fallback or network call", async () => {
  let calls = 0;
  const client = new MkxApiClient({
    baseUrl: "https://api.example.test",
    sessionStore: mockStore({ accessToken: "LOCAL", refreshToken: "secret" }),
    fetcher: async () => { calls++; return ok({}); }
  });
  await assert.rejects(client.externalIdentityMe(), ApiCredentialsRequired);
  await assert.rejects(client.integrationsPing(), ApiCredentialsRequired);
  assert.equal(calls, 0);
});

test("path parameters are encoded and query params do not leak into auth headers", async () => {
  const paths = [];
  const client = new MkxApiClient({
    baseUrl: "https://api.example.test",
    sessionStore: mockStore({ accessToken: "L", refreshToken: "R" }),
    fetcher: async (url, init) => {
      paths.push({ url, init });
      return url.includes("api-keys") ? ok(undefined, 204) : ok({ items: [], page: 1, pageSize: 5, total: 0 });
    }
  });
  await client.apiKeysRevoke("id/unsafe");
  await client.usersList(1, 5);
  assert.ok(paths[0].url.includes("/api/api-keys/id%2Funsafe"));
  assert.ok(paths[1].url.includes("page=1&pageSize=5"));
});

test("429 never refreshes and errors preserve HTTP status", async () => {
  let calls = 0;
  const client = new MkxApiClient({
    baseUrl: "https://api.example.test",
    sessionStore: mockStore({ accessToken: "L", refreshToken: "R" }),
    fetcher: async () => { calls++; return ok({ title: "Too many" }, 429); }
  });
  await assert.rejects(client.authMe(), err => err instanceof ApiHttpError && err.status === 429);
  assert.equal(calls, 1);
});

test("invalid origin or embedded URL credentials are refused", () => {
  for (const baseUrl of ["file:///etc/passwd", "https://user:password@api.example.test", "https://example.test/path"]) {
    assert.throws(() => new MkxApiClient({ baseUrl }));
  }
});

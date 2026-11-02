/** Protocol-aware transport. Credentials are supplied by the host app; NEVER stored here. */
export type Security = "LocalJwt" | "ExternalOidc" | "OpaqueToken" | "ApiKey" | null;
export interface LocalTokens { accessToken: string; refreshToken: string; }
export interface SessionStore {
  read(): Promise<LocalTokens | null>;
  write(tokens: LocalTokens | null): Promise<void>;
}
export type CredentialProvider = () => string | null | Promise<string | null>;
export interface ClientOptions {
  baseUrl: string;
  fetcher?: typeof fetch;
  sessionStore?: SessionStore;
  externalAccessToken?: CredentialProvider;
  opaqueAccessToken?: CredentialProvider;
  apiKey?: CredentialProvider;
}
export class ApiHttpError extends Error {
  constructor(public readonly status: number, public readonly responseText: string) {
    super("HTTP " + status);
    this.name = "ApiHttpError";
  }
}
export class ApiCredentialsRequired extends Error {
  constructor(public readonly scheme: Exclude<Security, null>) {
    super("Credential provider for " + scheme + " is unavailable");
  }
}
export class ApiTransport {
  private readonly base: string;
  private readonly fetcher: typeof fetch;
  private readonly options: ClientOptions;
  private refreshInFlight: Promise<boolean> | undefined;

  constructor(options: ClientOptions) {
    const url = new URL(options.baseUrl);
    if ((!["http:", "https:"].includes(url.protocol)) ||
        (url.protocol === "http:" && !["localhost", "127.0.0.1"].includes(url.hostname)) ||
        url.username || url.password ||
        url.search || url.hash || url.pathname !== "/") {
      throw new Error("baseUrl must be an HTTP(S) origin without credentials, path, query or fragment");
    }
    this.base = url.origin;
    this.options = options;
    this.fetcher = options.fetcher ?? fetch;
  }

  protected async persistTokens(tokens: LocalTokens): Promise<void> {
    await this.options.sessionStore?.write(tokens);
  }

  protected async request<T>(
    method: string, path: string, scheme: Security, body?: unknown,
    query?: Record<string, string | number | boolean | undefined>,
    retry = true,
  ): Promise<T> {
    const url = new URL(path, this.base);
    for (const [key, value] of Object.entries(query ?? {})) {
      if (value !== undefined) url.searchParams.set(key, String(value));
    }
    const headers: Record<string, string> = { Accept: "application/json" };
    let usedLocal: string | undefined;
    if (scheme === "LocalJwt") {
      usedLocal = (await this.options.sessionStore?.read())?.accessToken;
      if (!usedLocal) throw new ApiCredentialsRequired("LocalJwt");
      headers.Authorization = "Bearer " + usedLocal;
    } else if (scheme) {
      const provider = scheme === "ExternalOidc" ? this.options.externalAccessToken
        : scheme === "OpaqueToken" ? this.options.opaqueAccessToken : this.options.apiKey;
      const credential = await provider?.();
      if (!credential) throw new ApiCredentialsRequired(scheme);
      if (scheme === "ApiKey") headers["X-API-Key"] = credential;
      else headers.Authorization = "Bearer " + credential;
    }
    if (body !== undefined) headers["Content-Type"] = "application/json";
    const response = await this.fetcher(url.toString(), {
      method, headers, body: body === undefined ? undefined : JSON.stringify(body),
      redirect: "error",
    });
    if (response.status === 401 && scheme === "LocalJwt" && retry && this.options.sessionStore) {
      // When a parallel request has already refreshed, retry with the newer token.
      const newest = (await this.options.sessionStore.read())?.accessToken;
      if (newest && newest !== usedLocal)
        return this.request<T>(method, path, scheme, body, query, false);
      if (await this.refreshLocalOnce())
        return this.request<T>(method, path, scheme, body, query, false);
    }
    if (!response.ok) throw new ApiHttpError(response.status, (await response.text()).slice(0, 2048));
    if (response.status === 204) return undefined as T;
    const text = await response.text();
    if (!text) return undefined as T;
    try { return JSON.parse(text) as T; }
    catch { return text as T; } // Health live emits plain text.
  }

  private refreshLocalOnce(): Promise<boolean> {
    if (this.refreshInFlight) return this.refreshInFlight;
    this.refreshInFlight = this.doRefresh().finally(() => { this.refreshInFlight = undefined; });
    return this.refreshInFlight;
  }

  private async doRefresh(): Promise<boolean> {
    const store = this.options.sessionStore;
    const prior = await store?.read();
    if (!store || !prior?.refreshToken) return false;
    try {
      // Anonymous refresh is deliberately sent WITHOUT a bearer or API key.
      const response = await this.fetcher(this.base + "/api/auth/refresh", {
        method: "POST",
        headers: { "Content-Type": "application/json", Accept: "application/json" },
        body: JSON.stringify({ refreshToken: prior.refreshToken }),
        redirect: "error",
      });
      if (!response.ok) {
        await store.write(null);
        return false;
      }
      const data: unknown = await response.json();
      if (!data || typeof data !== "object" ||
          typeof (data as Record<string, unknown>).accessToken !== "string" ||
          typeof (data as Record<string, unknown>).refreshToken !== "string")
        throw new Error("Malformed refresh response");
      const next = data as LocalTokens;
      await store.write({ accessToken: next.accessToken, refreshToken: next.refreshToken });
      return true;
    } catch {
      await store.write(null);
      return false;
    }
  }
}

function resolveApiBase() {
  if (import.meta.env.VITE_API_BASE_URL) {
    return import.meta.env.VITE_API_BASE_URL;
  }

  const apiPort = import.meta.env.VITE_API_PORT ?? "5000";
  const { protocol, hostname } = window.location;
  return `${protocol}//${hostname}:${apiPort}`;
}

const API_BASE = resolveApiBase();

function joinUrl(path) {
  return `${API_BASE}${path}`;
}

export function loadSession() {
  try {
    const raw = localStorage.getItem("expense-session");
    return raw ? JSON.parse(raw) : null;
  } catch {
    return null;
  }
}

export function saveSession(session) {
  localStorage.setItem("expense-session", JSON.stringify(session));
}

export function clearSession() {
  localStorage.removeItem("expense-session");
}

async function readError(response) {
  const text = await response.text();
  return text || `Request failed with status ${response.status}.`;
}

export async function authRequest(path, payload) {
  const response = await fetch(joinUrl(path), {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
    },
    body: JSON.stringify(payload),
  });

  if (!response.ok) {
    throw new Error(await readError(response));
  }

  return response.json();
}

export function createApiClient(getSession, setSession, onUnauthorized) {
  let refreshPromise = null;

  async function refreshAccessToken() {
    const session = getSession();
    if (!session?.refreshToken) {
      onUnauthorized();
      throw new Error("No refresh token available.");
    }

    const refreshed = await authRequest("/api/v1/auth/refresh", {
      refreshToken: session.refreshToken,
    });

    const nextSession = {
      accessToken: refreshed.accessToken,
      refreshToken: refreshed.refreshToken,
      expiresAtUtc: refreshed.expiresAtUtc,
      email: session.email,
    };

    setSession(nextSession);
    return nextSession;
  }

  async function authorizedFetch(path, options = {}, retry = true) {
    const session = getSession();
    const headers = new Headers(options.headers ?? {});

    if (session?.accessToken) {
      headers.set("Authorization", `Bearer ${session.accessToken}`);
    }

    if (options.body && !headers.has("Content-Type") && !(options.body instanceof FormData)) {
      headers.set("Content-Type", "application/json");
    }

    const response = await fetch(joinUrl(path), {
      ...options,
      headers,
    });

    if (response.status === 401 && retry) {
      try {
        refreshPromise ??= refreshAccessToken().finally(() => {
          refreshPromise = null;
        });
        await refreshPromise;
        return authorizedFetch(path, options, false);
      } catch {
        onUnauthorized();
        throw new Error("Session expired. Please log in again.");
      }
    }

    if (!response.ok) {
      throw new Error(await readError(response));
    }

    if (response.status === 204) {
      return null;
    }

    const contentType = response.headers.get("Content-Type") ?? "";
    if (contentType.includes("application/json")) {
      return response.json();
    }

    return response.text();
  }

  return {
    get: (path) => authorizedFetch(path),
    post: (path, payload) =>
      authorizedFetch(path, {
        method: "POST",
        body: payload instanceof FormData ? payload : JSON.stringify(payload),
      }),
    put: (path, payload) =>
      authorizedFetch(path, {
        method: "PUT",
        body: JSON.stringify(payload),
      }),
    delete: (path) =>
      authorizedFetch(path, {
        method: "DELETE",
      }),
  };
}

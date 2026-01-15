const API_BASE = import.meta.env.VITE_API_BASE || "/api";

type Options = RequestInit & { token?: string };

export async function apiFetch<T>(path: string, options: Options = {}): Promise<T> {
  const headers: Record<string, string> = {
    "Content-Type": "application/json",
    ...(options.headers as Record<string, string> | undefined),
  };
  if (options.token) {
    headers.Authorization = `Bearer ${options.token}`;
  }

  // Add cache-busting headers to prevent browser caching
  const fetchOptions: RequestInit = {
    ...options,
    headers,
    cache: "no-store",
  };

  const res = await fetch(`${API_BASE}${path}`, fetchOptions);
  if (!res.ok) {
    const message = await res.text();
    throw new Error(message || res.statusText);
  }
  return (await res.json()) as T;
}

export { API_BASE };

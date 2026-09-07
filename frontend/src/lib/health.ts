import { apiBaseUrl } from "./config";

export interface BackendHealth {
  status: string;
  checkedAtUtc: string;
  version: string;
}

export async function getBackendHealth(): Promise<BackendHealth | null> {
  try {
    const response = await fetch(`${apiBaseUrl}/health`, { cache: "no-store" });
    if (!response.ok) {
      return null;
    }
    return (await response.json()) as BackendHealth;
  } catch {
    return null;
  }
}

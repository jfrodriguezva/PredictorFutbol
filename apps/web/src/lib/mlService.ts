const ML_SERVICE_URL = process.env.ML_SERVICE_URL ?? "http://localhost:8000";

export type Team = { id: number; name: string; logo_url: string | null };
export type League = { id: number; name: string; country: string; season: number };

export type FixtureSummary = {
  fixture_id: number;
  league: League;
  home_team: Team;
  away_team: Team;
  kickoff_at: string;
  status: string;
  home_goals: number | null;
  away_goals: number | null;
};

export type FixtureDetail = FixtureSummary & {
  venue: string | null;
  referee: string | null;
};

export type PredictResponse = {
  fixture_id: number;
  model_version: string;
  probabilities: { home: number; draw: number; away: number };
  features_summary: Record<string, number | string | null>;
};

async function mlFetch<T>(path: string, init?: RequestInit): Promise<T> {
  const res = await fetch(`${ML_SERVICE_URL}${path}`, {
    ...init,
    headers: { "Content-Type": "application/json", ...(init?.headers ?? {}) },
    cache: "no-store",
  });
  if (!res.ok) {
    const detail = await res.text();
    throw new Error(`ml-service ${path} -> ${res.status}: ${detail}`);
  }
  return res.json() as Promise<T>;
}

export function searchFixtures(params: {
  team?: string;
  league_id?: number;
  status?: string;
  limit?: number;
}): Promise<FixtureSummary[]> {
  const qs = new URLSearchParams();
  if (params.team) qs.set("team", params.team);
  if (params.league_id) qs.set("league_id", String(params.league_id));
  if (params.status) qs.set("status", params.status);
  qs.set("limit", String(params.limit ?? 30));
  return mlFetch(`/fixtures?${qs.toString()}`);
}

export function getFixture(fixtureId: number): Promise<FixtureDetail> {
  return mlFetch(`/fixtures/${fixtureId}`);
}

export function predictFixture(fixtureId: number): Promise<PredictResponse> {
  return mlFetch(`/predict`, {
    method: "POST",
    body: JSON.stringify({ fixture_id: fixtureId }),
  });
}

export function listLeagues(): Promise<League[]> {
  return mlFetch(`/leagues`);
}

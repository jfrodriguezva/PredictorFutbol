"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { apiGet, apiPost, ApiError } from "@/lib/api";
import type { TrackedCompetition } from "@/lib/types";

export default function TrackedCompetitionsPage() {
  const [items, setItems] = useState<TrackedCompetition[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [leagueId, setLeagueId] = useState("");
  const [season, setSeason] = useState("");
  const [busy, setBusy] = useState(false);
  const [syncingId, setSyncingId] = useState<string | null>(null);
  const [syncMessage, setSyncMessage] = useState<string | null>(null);

  async function load() {
    try {
      setItems(await apiGet<TrackedCompetition[]>("/api/tracked-competitions"));
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Could not reach the backend.");
    }
  }

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect -- intentional fetch-on-mount
    load();
  }, []);

  async function handleCreate(e: React.FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      await apiPost("/api/tracked-competitions", {
        externalLeagueId: Number(leagueId),
        season: Number(season),
      });
      setLeagueId("");
      setSeason("");
      await load();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Failed to create.");
    } finally {
      setBusy(false);
    }
  }

  async function handleSync(id: string, action: "sync-teams" | "sync-fixtures" | "sync-standings") {
    setSyncingId(id + action);
    setSyncMessage(null);
    try {
      const result = await apiPost<Record<string, unknown>>(`/api/tracked-competitions/${id}/${action}`);
      setSyncMessage(`${action}: ${JSON.stringify(result)}`);
    } catch (err) {
      setSyncMessage(err instanceof ApiError ? `${action} failed: ${err.message}` : `${action} failed.`);
    } finally {
      setSyncingId(null);
    }
  }

  return (
    <main className="mx-auto max-w-5xl flex-1 p-6">
      <h1 className="text-2xl font-semibold">Tracked Competitions</h1>
      <p className="mt-1 text-sm text-neutral-500">
        Add a competition by its API-Football league id, then sync teams/fixtures/standings for it.
      </p>

      <form onSubmit={handleCreate} className="mt-6 flex flex-wrap items-end gap-3 rounded-lg border border-neutral-200 p-4 dark:border-neutral-800">
        <label className="flex flex-col gap-1 text-sm">
          League id (API-Football)
          <input
            required
            type="number"
            value={leagueId}
            onChange={(e) => setLeagueId(e.target.value)}
            className="rounded border border-neutral-300 px-2 py-1 dark:border-neutral-700 dark:bg-neutral-900"
          />
        </label>
        <label className="flex flex-col gap-1 text-sm">
          Season (year)
          <input
            required
            type="number"
            value={season}
            onChange={(e) => setSeason(e.target.value)}
            className="rounded border border-neutral-300 px-2 py-1 dark:border-neutral-700 dark:bg-neutral-900"
          />
        </label>
        <button
          type="submit"
          disabled={busy}
          className="rounded bg-neutral-900 px-4 py-1.5 text-sm text-white disabled:opacity-50 dark:bg-neutral-100 dark:text-neutral-900"
        >
          {busy ? "Adding…" : "Track competition"}
        </button>
      </form>

      {error && <p className="mt-4 text-sm text-red-600">{error}</p>}
      {syncMessage && <p className="mt-4 text-sm text-neutral-600 dark:text-neutral-400">{syncMessage}</p>}

      <div className="mt-6">
        {items === null ? (
          <p className="text-sm text-neutral-500">Loading…</p>
        ) : items.length === 0 ? (
          <p className="text-sm text-neutral-500">No data available — track a competition above.</p>
        ) : (
          <table className="w-full border-collapse text-sm">
            <thead>
              <tr className="border-b border-neutral-200 text-left dark:border-neutral-800">
                <th className="py-2">Competition</th>
                <th>Season</th>
                <th>Enabled</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              {items.map((item) => (
                <tr key={item.id} className="border-b border-neutral-100 dark:border-neutral-900">
                  <td className="py-2">
                    <Link href={`/tracked-competitions/${item.id}`} className="text-blue-600 hover:underline dark:text-blue-400">
                      {item.competitionName || `League ${item.externalLeagueId}`}
                    </Link>
                  </td>
                  <td>{item.season}</td>
                  <td>{item.enabled ? "Yes" : "No"}</td>
                  <td className="flex flex-wrap gap-2 py-2">
                    <button
                      onClick={() => handleSync(item.id, "sync-teams")}
                      disabled={syncingId === item.id + "sync-teams"}
                      className="rounded border border-neutral-300 px-2 py-1 text-xs disabled:opacity-50 dark:border-neutral-700"
                    >
                      Sync teams
                    </button>
                    <button
                      onClick={() => handleSync(item.id, "sync-fixtures")}
                      disabled={syncingId === item.id + "sync-fixtures"}
                      className="rounded border border-neutral-300 px-2 py-1 text-xs disabled:opacity-50 dark:border-neutral-700"
                    >
                      Sync fixtures
                    </button>
                    <button
                      onClick={() => handleSync(item.id, "sync-standings")}
                      disabled={syncingId === item.id + "sync-standings"}
                      className="rounded border border-neutral-300 px-2 py-1 text-xs disabled:opacity-50 dark:border-neutral-700"
                    >
                      Sync standings
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </main>
  );
}

"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { use } from "react";
import { apiGet, ApiError } from "@/lib/api";
import type { Match } from "@/lib/types";

export default function TrackedCompetitionMatchesPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const [matches, setMatches] = useState<Match[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    apiGet<Match[]>(`/api/tracked-competitions/${id}/matches`)
      .then(setMatches)
      .catch((err) => setError(err instanceof ApiError ? err.message : "Could not reach the backend."));
  }, [id]);

  return (
    <main className="mx-auto max-w-5xl flex-1 p-6">
      <Link href="/tracked-competitions" className="text-sm text-blue-600 hover:underline dark:text-blue-400">
        ← Competitions
      </Link>
      <h1 className="mt-2 text-2xl font-semibold">Matches</h1>

      {error && <p className="mt-4 text-sm text-red-600">{error}</p>}

      <div className="mt-6">
        {matches === null ? (
          <p className="text-sm text-neutral-500">Loading…</p>
        ) : matches.length === 0 ? (
          <p className="text-sm text-neutral-500">
            No data available — run &quot;Sync fixtures&quot; from the competitions page first.
          </p>
        ) : (
          <table className="w-full border-collapse text-sm">
            <thead>
              <tr className="border-b border-neutral-200 text-left dark:border-neutral-800">
                <th className="py-2">Match</th>
                <th>Date (UTC)</th>
                <th>Status</th>
                <th>Score</th>
              </tr>
            </thead>
            <tbody>
              {matches.map((m) => (
                <tr key={m.id} className="border-b border-neutral-100 dark:border-neutral-900">
                  <td className="py-2">
                    <Link href={`/matches/${m.id}?trackedCompetitionId=${id}`} className="text-blue-600 hover:underline dark:text-blue-400">
                      {m.homeTeamName} vs {m.awayTeamName}
                    </Link>
                  </td>
                  <td>{new Date(m.matchDateUtc).toLocaleString()}</td>
                  <td>{m.status}</td>
                  <td>{m.homeScore ?? "–"} : {m.awayScore ?? "–"}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </main>
  );
}

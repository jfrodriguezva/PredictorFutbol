"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { apiGet, ApiError } from "@/lib/api";
import type { Prediction } from "@/lib/types";

export default function ValueBetsPage() {
  const [predictions, setPredictions] = useState<Prediction[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    apiGet<Prediction[]>("/api/predictions/recommended")
      .then(setPredictions)
      .catch((err) => setError(err instanceof ApiError ? err.message : "Could not reach the backend."));
  }, []);

  return (
    <main className="mx-auto max-w-5xl flex-1 p-6">
      <h1 className="text-2xl font-semibold">Value Bets</h1>
      <p className="mt-1 text-sm text-neutral-500">
        Selections with positive Expected Value against known odds (CLAUDE.md section 25) — never recommended
        purely because a team is the favorite.
      </p>

      {error && <p className="mt-4 text-sm text-red-600">{error}</p>}

      <div className="mt-6">
        {predictions === null ? (
          <p className="text-sm text-neutral-500">Loading…</p>
        ) : predictions.length === 0 ? (
          <p className="text-sm text-neutral-500">No data available — generate predictions for matches with known odds.</p>
        ) : (
          <table className="w-full border-collapse text-sm">
            <thead>
              <tr className="border-b border-neutral-200 text-left dark:border-neutral-800">
                <th className="py-2">Match</th>
                <th>Selection</th>
                <th>Probability</th>
                <th>EV</th>
                <th>Category</th>
              </tr>
            </thead>
            <tbody>
              {predictions.map((p) => (
                <tr key={p.id} className="border-b border-neutral-100 dark:border-neutral-900">
                  <td className="py-2">
                    <Link href={`/matches/${p.matchId}`} className="text-blue-600 hover:underline dark:text-blue-400">
                      View match
                    </Link>
                  </td>
                  <td>{p.selection}</td>
                  <td>{(p.probability * 100).toFixed(1)}%</td>
                  <td>{p.expectedValue !== null ? `${(p.expectedValue * 100).toFixed(1)}%` : "–"}</td>
                  <td>{p.valueCategory}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </main>
  );
}

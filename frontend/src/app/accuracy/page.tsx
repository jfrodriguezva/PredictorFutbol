"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { apiGet, ApiError } from "@/lib/api";
import type { AccuracySummary } from "@/lib/types";

export default function AccuracyPage() {
  const [summary, setSummary] = useState<AccuracySummary | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    apiGet<AccuracySummary>("/api/predictions/accuracy")
      .then(setSummary)
      .catch((err) => setError(err instanceof ApiError ? err.message : "Could not reach the backend."));
  }, []);

  return (
    <main className="mx-auto max-w-5xl flex-1 p-6">
      <h1 className="text-2xl font-semibold">Accuracy</h1>
      <p className="mt-1 text-sm text-neutral-500">
        Post-game analysis: how often the model has actually been right so far. Click &quot;Evaluate Result&quot; on
        a finished match&apos;s page to record it here — nothing here happens automatically yet.
      </p>

      {error && <p className="mt-4 text-sm text-red-600">{error}</p>}

      {summary === null ? (
        <p className="mt-6 text-sm text-neutral-500">Loading…</p>
      ) : summary.totalEvaluated === 0 ? (
        <p className="mt-6 text-sm text-neutral-500">
          No data available — no predictions have been evaluated against a real result yet.
        </p>
      ) : (
        <>
          <div className="mt-6 grid grid-cols-2 gap-4 md:grid-cols-4">
            <div className="rounded-lg border border-neutral-200 p-4 dark:border-neutral-800">
              <p className="text-xs uppercase text-neutral-500">Overall accuracy</p>
              <p className="mt-1 text-2xl font-semibold">{(summary.accuracy * 100).toFixed(1)}%</p>
              <p className="text-xs text-neutral-500">{summary.correctCount} / {summary.totalEvaluated} evaluated</p>
            </div>
            <div className="rounded-lg border border-neutral-200 p-4 dark:border-neutral-800">
              <p className="text-xs uppercase text-neutral-500">Recommended-bet accuracy</p>
              <p className="mt-1 text-2xl font-semibold">
                {summary.recommendedTotal > 0 ? `${(summary.recommendedAccuracy * 100).toFixed(1)}%` : "–"}
              </p>
              <p className="text-xs text-neutral-500">{summary.recommendedCorrect} / {summary.recommendedTotal} recommended</p>
            </div>
          </div>

          <div className="mt-8">
            <h2 className="text-lg font-medium">Recent misses (recommended bets that were wrong)</h2>
            {summary.recentMisses.length === 0 ? (
              <p className="mt-2 text-sm text-neutral-500">No data available — no recommended bet has missed yet.</p>
            ) : (
              <table className="mt-3 w-full border-collapse text-sm">
                <thead>
                  <tr className="border-b border-neutral-200 text-left dark:border-neutral-800">
                    <th className="py-2">Match</th>
                    <th>Picked</th>
                    <th>Confidence</th>
                    <th>Actual result</th>
                    <th>Predicted on</th>
                  </tr>
                </thead>
                <tbody>
                  {summary.recentMisses.map((miss, i) => (
                    <tr key={i} className="border-b border-neutral-100 dark:border-neutral-900">
                      <td className="py-2">
                        <Link href={`/matches/${miss.matchId}`} className="text-blue-600 hover:underline dark:text-blue-400">
                          View match
                        </Link>
                      </td>
                      <td>{miss.selection}</td>
                      <td>{(miss.probability * 100).toFixed(1)}%</td>
                      <td>{miss.actualOutcome}</td>
                      <td>{new Date(miss.predictionDate).toLocaleString()}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </div>
        </>
      )}
    </main>
  );
}

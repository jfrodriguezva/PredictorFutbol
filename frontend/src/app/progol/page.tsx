"use client";

import { useState } from "react";
import { apiPost, ApiError } from "@/lib/api";

interface Pick {
  matchLabel: string;
  selections: string[];
  hitProbability: number;
}

interface Scenario {
  name: string;
  picks: Pick[];
  totalCombinations: number;
  estimatedFullHitProbability: number;
}

const DEFAULT_ROWS = Array.from({ length: 14 }, (_, i) => ({
  matchLabel: `Match ${i + 1}`,
  homeProbability: 0.4,
  drawProbability: 0.3,
  awayProbability: 0.3,
}));

export default function ProgolPage() {
  const [rows, setRows] = useState(DEFAULT_ROWS);
  const [budget, setBudget] = useState(8);
  const [maxDoubles, setMaxDoubles] = useState(3);
  const [maxTriples, setMaxTriples] = useState(1);
  const [scenarios, setScenarios] = useState<Scenario[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  function updateRow(index: number, field: "homeProbability" | "drawProbability" | "awayProbability", value: number) {
    setRows((prev) => prev.map((r, i) => (i === index ? { ...r, [field]: value } : r)));
  }

  async function handleOptimize() {
    setBusy(true);
    setError(null);
    try {
      const result = await apiPost<{ scenarios: Scenario[] }>("/api/progol/optimize", {
        matches: rows.map((r) => ({
          matchLabel: r.matchLabel,
          homeProbability: r.homeProbability,
          drawProbability: r.drawProbability,
          awayProbability: r.awayProbability,
        })),
        budget,
        maxDoubles,
        maxTriples,
      });
      setScenarios(result.scenarios);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Optimization failed.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <main className="mx-auto max-w-5xl flex-1 p-6">
      <h1 className="text-2xl font-semibold">Progol Optimizer</h1>
      <p className="mt-1 text-sm text-neutral-500">
        Enter L/E/V probabilities for each match (they don&apos;t need to sum to 1 exactly), set your budget and
        allowed doubles/triples, and get Safe/Balanced/Aggressive/Contrarian scenarios (CLAUDE.md section 30).
      </p>

      <div className="mt-6 flex flex-wrap gap-4 rounded-lg border border-neutral-200 p-4 dark:border-neutral-800">
        <label className="flex flex-col gap-1 text-sm">
          Budget (max combinations)
          <input
            type="number"
            value={budget}
            onChange={(e) => setBudget(Number(e.target.value))}
            className="w-32 rounded border border-neutral-300 px-2 py-1 dark:border-neutral-700 dark:bg-neutral-900"
          />
        </label>
        <label className="flex flex-col gap-1 text-sm">
          Max doubles
          <input
            type="number"
            value={maxDoubles}
            onChange={(e) => setMaxDoubles(Number(e.target.value))}
            className="w-32 rounded border border-neutral-300 px-2 py-1 dark:border-neutral-700 dark:bg-neutral-900"
          />
        </label>
        <label className="flex flex-col gap-1 text-sm">
          Max triples
          <input
            type="number"
            value={maxTriples}
            onChange={(e) => setMaxTriples(Number(e.target.value))}
            className="w-32 rounded border border-neutral-300 px-2 py-1 dark:border-neutral-700 dark:bg-neutral-900"
          />
        </label>
      </div>

      <div className="mt-6 overflow-x-auto">
        <table className="w-full border-collapse text-sm">
          <thead>
            <tr className="border-b border-neutral-200 text-left dark:border-neutral-800">
              <th className="py-2">Match</th>
              <th>Home (L)</th>
              <th>Draw (E)</th>
              <th>Away (V)</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((row, i) => (
              <tr key={row.matchLabel} className="border-b border-neutral-100 dark:border-neutral-900">
                <td className="py-1">{row.matchLabel}</td>
                {(["homeProbability", "drawProbability", "awayProbability"] as const).map((field) => (
                  <td key={field}>
                    <input
                      type="number"
                      step="0.01"
                      min="0"
                      max="1"
                      value={row[field]}
                      onChange={(e) => updateRow(i, field, Number(e.target.value))}
                      className="w-20 rounded border border-neutral-300 px-1 py-0.5 dark:border-neutral-700 dark:bg-neutral-900"
                    />
                  </td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <button
        onClick={handleOptimize}
        disabled={busy}
        className="mt-6 rounded bg-neutral-900 px-4 py-2 text-sm text-white disabled:opacity-50 dark:bg-neutral-100 dark:text-neutral-900"
      >
        {busy ? "Optimizing…" : "Optimize"}
      </button>

      {error && <p className="mt-4 text-sm text-red-600">{error}</p>}

      {scenarios && (
        <div className="mt-8 grid gap-4 md:grid-cols-2">
          {scenarios.map((scenario) => (
            <div key={scenario.name} className="rounded-lg border border-neutral-200 p-4 dark:border-neutral-800">
              <h2 className="font-medium">{scenario.name}</h2>
              <p className="text-xs text-neutral-500">
                {scenario.totalCombinations} combinations · estimated full-hit probability{" "}
                {(scenario.estimatedFullHitProbability * 100).toFixed(4)}%
              </p>
              <div className="mt-2 flex flex-wrap gap-1 text-xs">
                {scenario.picks.map((pick) => (
                  <span
                    key={pick.matchLabel}
                    className="rounded border border-neutral-300 px-1.5 py-0.5 dark:border-neutral-700"
                    title={pick.matchLabel}
                  >
                    {pick.selections.join("/")}
                  </span>
                ))}
              </div>
            </div>
          ))}
        </div>
      )}
    </main>
  );
}

"use client";

import { useEffect, useState } from "react";
import { apiGet, apiPost, ApiError } from "@/lib/api";
import type { ModelVersion, TrackedCompetition } from "@/lib/types";

export default function ModelsPage() {
  const [models, setModels] = useState<ModelVersion[] | null>(null);
  const [competitions, setCompetitions] = useState<TrackedCompetition[]>([]);
  const [selectedCompetition, setSelectedCompetition] = useState("");
  const [training, setTraining] = useState(false);
  const [activatingId, setActivatingId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function loadModels() {
    try {
      setModels(await apiGet<ModelVersion[]>("/api/model-versions"));
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Could not reach the backend.");
    }
  }

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect -- intentional fetch-on-mount
    loadModels();
    apiGet<TrackedCompetition[]>("/api/tracked-competitions")
      .then(setCompetitions)
      .catch(() => {});
  }, []);

  async function handleTrain() {
    if (!selectedCompetition) return;
    setTraining(true);
    setError(null);
    try {
      await apiPost(`/api/model-versions/train-football-1x2/${selectedCompetition}`);
      await loadModels();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Training failed.");
    } finally {
      setTraining(false);
    }
  }

  async function handleActivate(id: string) {
    setActivatingId(id);
    setError(null);
    try {
      await apiPost(`/api/model-versions/${id}/activate`);
      await loadModels();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Failed to activate this model version.");
    } finally {
      setActivatingId(null);
    }
  }

  return (
    <main className="mx-auto max-w-5xl flex-1 p-6">
      <h1 className="text-2xl font-semibold">Models</h1>
      <p className="mt-1 text-sm text-neutral-500">
        Trains and compares Logistic Regression / XGBoost / LightGBM on the 1X2 target (CLAUDE.md section 20)
        and registers the winner — never overwrites a previous version.
      </p>

      <div className="mt-6 flex flex-wrap items-end gap-3 rounded-lg border border-neutral-200 p-4 dark:border-neutral-800">
        <label className="flex flex-col gap-1 text-sm">
          Tracked competition
          <select
            value={selectedCompetition}
            onChange={(e) => setSelectedCompetition(e.target.value)}
            className="rounded border border-neutral-300 px-2 py-1 dark:border-neutral-700 dark:bg-neutral-900"
          >
            <option value="">Select…</option>
            {competitions.map((c) => (
              <option key={c.id} value={c.id}>
                {c.competitionName} ({c.season})
              </option>
            ))}
          </select>
        </label>
        <button
          onClick={handleTrain}
          disabled={training || !selectedCompetition}
          className="rounded bg-neutral-900 px-4 py-1.5 text-sm text-white disabled:opacity-50 dark:bg-neutral-100 dark:text-neutral-900"
        >
          {training ? "Training…" : "Train football_1x2"}
        </button>
      </div>

      {error && <p className="mt-4 text-sm text-red-600">{error}</p>}

      <div className="mt-6">
        {models === null ? (
          <p className="text-sm text-neutral-500">Loading…</p>
        ) : models.length === 0 ? (
          <p className="text-sm text-neutral-500">No data available — train a model above.</p>
        ) : (
          <table className="w-full border-collapse text-sm">
            <thead>
              <tr className="border-b border-neutral-200 text-left dark:border-neutral-800">
                <th className="py-2">Version</th>
                <th>Algorithm</th>
                <th>Brier</th>
                <th>Log Loss</th>
                <th>Trained</th>
                <th>Active</th>
              </tr>
            </thead>
            <tbody>
              {models.map((m) => (
                <tr key={m.id} className="border-b border-neutral-100 dark:border-neutral-900">
                  <td className="py-2">{m.version}</td>
                  <td>{m.algorithm}</td>
                  <td>{m.brierScore?.toFixed(4) ?? "–"}</td>
                  <td>{m.logLoss?.toFixed(4) ?? "–"}</td>
                  <td>{new Date(m.trainedAt).toLocaleString()}</td>
                  <td>
                    {m.active ? (
                      <span className="text-xs font-medium text-green-700 dark:text-green-400">✓ Active</span>
                    ) : (
                      <button
                        onClick={() => handleActivate(m.id)}
                        disabled={activatingId === m.id}
                        className="rounded border border-neutral-300 px-2 py-1 text-xs disabled:opacity-50 dark:border-neutral-700"
                      >
                        {activatingId === m.id ? "Activating…" : "Activate"}
                      </button>
                    )}
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

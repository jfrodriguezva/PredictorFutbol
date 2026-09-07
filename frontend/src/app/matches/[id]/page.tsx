"use client";

import Link from "next/link";
import { use, useEffect, useState } from "react";
import { useSearchParams } from "next/navigation";
import { apiGet, apiPost, ApiError } from "@/lib/api";
import type { GeneratePredictionResult, LearnFromMatchResult, Match, Prediction, PredictionExplanation } from "@/lib/types";

function pct(value: number): string {
  return `${(value * 100).toFixed(1)}%`;
}

export default function MatchDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const searchParams = useSearchParams();
  const trackedCompetitionId = searchParams.get("trackedCompetitionId");

  const [match, setMatch] = useState<Match | null>(null);
  const [predictions, setPredictions] = useState<Prediction[] | null>(null);
  const [analysis, setAnalysis] = useState<PredictionExplanation | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [generating, setGenerating] = useState(false);
  const [evaluating, setEvaluating] = useState(false);
  const [learning, setLearning] = useState(false);
  const [analyzing, setAnalyzing] = useState(false);

  async function loadMatch() {
    try {
      setMatch(await apiGet<Match>(`/api/matches/${id}`));
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Could not reach the backend.");
    }
  }

  async function loadPredictions() {
    try {
      setPredictions(await apiGet<Prediction[]>(`/api/predictions/match/${id}`));
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Could not reach the backend.");
    }
  }

  async function loadAnalysis() {
    try {
      setAnalysis(await apiGet<PredictionExplanation>(`/api/predictions/${id}/analysis`));
    } catch (err) {
      if (err instanceof ApiError && err.status === 404) {
        setAnalysis(null);
      } else {
        setError(err instanceof ApiError ? err.message : "Could not reach the backend.");
      }
    }
  }

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect -- intentional fetch-on-mount
    loadMatch();
    loadPredictions();
    loadAnalysis();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id]);

  async function handleGenerate() {
    setGenerating(true);
    setError(null);
    setNotice(null);
    try {
      await apiPost<GeneratePredictionResult>(`/api/predictions/generate/${id}`);
      await loadPredictions();
    } catch (err) {
      setError(
        err instanceof ApiError
          ? err.status === 409
            ? "No trained model exists yet — train one from the Models page first."
            : err.message
          : "Failed to generate a prediction.",
      );
    } finally {
      setGenerating(false);
    }
  }

  async function handleEvaluate() {
    setEvaluating(true);
    setError(null);
    setNotice(null);
    try {
      await apiPost(`/api/predictions/evaluate/${id}`);
      await loadPredictions();
      setNotice("Recorded the real result against every prediction made for this match.");
    } catch (err) {
      setError(
        err instanceof ApiError
          ? err.status === 409
            ? "This match hasn't finished yet — sync fixtures again once it's played."
            : err.message
          : "Failed to evaluate this match.",
      );
    } finally {
      setEvaluating(false);
    }
  }

  async function handleLearn() {
    if (!trackedCompetitionId) return;
    setLearning(true);
    setError(null);
    setNotice(null);
    try {
      const result = await apiPost<LearnFromMatchResult>(
        `/api/predictions/learn-from-match/${id}?trackedCompetitionId=${trackedCompetitionId}`,
      );
      await loadPredictions();
      setNotice(
        `Learned from this match: recorded "${result.evaluation.actualOutcome}" as the result, rebuilt features, ` +
          `and trained a new model version (${result.newModelVersion.version}, ${result.newModelVersion.algorithm}).`,
      );
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Failed to learn from this match.");
    } finally {
      setLearning(false);
    }
  }

  async function handleAnalyze() {
    setAnalyzing(true);
    setError(null);
    setNotice(null);
    try {
      const result = await apiPost<PredictionExplanation>(`/api/predictions/${id}/analyze`);
      setAnalysis(result);
    } catch (err) {
      setError(
        err instanceof ApiError
          ? err.status === 409
            ? "No trained model exists yet — train one from the Models page first."
            : err.message
          : "Failed to analyze this match.",
      );
    } finally {
      setAnalyzing(false);
    }
  }

  const latestBatch = predictions?.length
    ? predictions.filter((p) => p.predictionDate === predictions[0].predictionDate)
    : [];
  const isFinished = match?.status === "Finished";
  const alreadyEvaluated = latestBatch.some((p) => p.isCorrect !== null);

  return (
    <main className="mx-auto max-w-3xl flex-1 p-6">
      <Link
        href={trackedCompetitionId ? `/tracked-competitions/${trackedCompetitionId}` : "/tracked-competitions"}
        className="text-sm text-blue-600 hover:underline dark:text-blue-400"
      >
        ← Matches
      </Link>

      {match ? (
        <>
          <h1 className="mt-2 text-2xl font-semibold">
            {match.homeTeamName} vs {match.awayTeamName}
          </h1>
          <p className="mt-1 text-sm text-neutral-500">
            {new Date(match.matchDateUtc).toLocaleString()} · {match.status}
            {match.homeScore !== null && ` · ${match.homeScore}:${match.awayScore}`}
          </p>
        </>
      ) : (
        <p className="mt-4 text-sm text-neutral-500">Loading…</p>
      )}

      <div className="mt-6 flex flex-wrap gap-3">
        <button
          onClick={handleGenerate}
          disabled={generating}
          className="rounded bg-neutral-900 px-4 py-2 text-sm text-white disabled:opacity-50 dark:bg-neutral-100 dark:text-neutral-900"
        >
          {generating ? "Generating…" : "Generate Prediction"}
        </button>

        {isFinished && (
          <button
            onClick={handleEvaluate}
            disabled={evaluating || latestBatch.length === 0}
            title={latestBatch.length === 0 ? "Generate a prediction first" : undefined}
            className="rounded border border-neutral-300 px-4 py-2 text-sm disabled:opacity-50 dark:border-neutral-700"
          >
            {evaluating ? "Evaluating…" : "Evaluate Result"}
          </button>
        )}

        {isFinished && trackedCompetitionId && (
          <button
            onClick={handleLearn}
            disabled={learning || latestBatch.length === 0}
            title={latestBatch.length === 0 ? "Generate a prediction first" : "Records the result, rebuilds features, and retrains"}
            className="rounded border border-blue-400 px-4 py-2 text-sm text-blue-700 disabled:opacity-50 dark:border-blue-700 dark:text-blue-400"
          >
            {learning ? "Learning…" : "Learn from this match"}
          </button>
        )}

        <button
          onClick={handleAnalyze}
          disabled={analyzing}
          title="SHAP feature attribution, Kelly-Criterion stake sizing, and a narrative on top of the raw prediction"
          className="rounded border border-purple-400 px-4 py-2 text-sm text-purple-700 disabled:opacity-50 dark:border-purple-700 dark:text-purple-400"
        >
          {analyzing ? "Analyzing…" : "Analizar"}
        </button>
      </div>

      {error && <p className="mt-4 text-sm text-red-600">{error}</p>}
      {notice && <p className="mt-4 text-sm text-green-700 dark:text-green-400">{notice}</p>}

      <div className="mt-8">
        <h2 className="text-lg font-medium">Latest prediction</h2>
        {latestBatch.length === 0 ? (
          <p className="mt-2 text-sm text-neutral-500">No data available — click &quot;Generate Prediction&quot; above.</p>
        ) : (
          <div className="mt-3 grid grid-cols-3 gap-3">
            {latestBatch.map((p) => (
              <div
                key={p.id}
                className={`rounded-lg border p-4 text-center ${
                  p.isCorrect === true
                    ? "border-green-500 bg-green-50 dark:bg-green-950/30"
                    : p.isCorrect === false
                      ? "border-red-400 bg-red-50 dark:bg-red-950/20"
                      : p.recommended
                        ? "border-green-500 bg-green-50 dark:bg-green-950/30"
                        : "border-neutral-200 dark:border-neutral-800"
                }`}
              >
                <p className="text-xs uppercase text-neutral-500">{p.selection}</p>
                <p className="mt-1 text-2xl font-semibold">{pct(p.probability)}</p>
                {p.expectedValue !== null && (
                  <p className="mt-1 text-xs text-neutral-500">
                    EV {p.expectedValue >= 0 ? "+" : ""}
                    {(p.expectedValue * 100).toFixed(1)}% · {p.valueCategory}
                  </p>
                )}
                {p.recommended && <p className="mt-1 text-xs font-medium text-green-700 dark:text-green-400">Recommended</p>}
                {p.isCorrect === true && <p className="mt-1 text-xs font-medium text-green-700 dark:text-green-400">✓ Correct</p>}
                {p.isCorrect === false && <p className="mt-1 text-xs font-medium text-red-600 dark:text-red-400">✗ Wrong</p>}
              </div>
            ))}
          </div>
        )}
        {isFinished && !alreadyEvaluated && latestBatch.length > 0 && (
          <p className="mt-2 text-xs text-neutral-500">
            This match has finished — click &quot;Evaluate Result&quot; to see which selection was actually right.
          </p>
        )}
      </div>

      {analysis && (
        <div className="mt-8 rounded-lg border border-purple-200 p-4 dark:border-purple-900">
          <h2 className="text-lg font-medium">Análisis</h2>
          <p className="mt-1 text-xs text-neutral-500">
            Generado {new Date(analysis.generatedAt).toLocaleString()} · H {pct(analysis.home)} / D {pct(analysis.draw)} / A{" "}
            {pct(analysis.away)}
          </p>

          <h3 className="mt-4 text-sm font-medium text-neutral-700 dark:text-neutral-300">Features más relevantes (SHAP)</h3>
          <ul className="mt-2 space-y-1 text-sm">
            {analysis.shapTopFeatures.map((f) => (
              <li key={f.feature} className="flex justify-between border-b border-neutral-100 py-1 dark:border-neutral-900">
                <span className="text-neutral-600 dark:text-neutral-400">{f.feature}</span>
                <span className={f.impact >= 0 ? "text-green-700 dark:text-green-400" : "text-red-600 dark:text-red-400"}>
                  {f.impact >= 0 ? "+" : ""}
                  {f.impact.toFixed(3)}
                </span>
              </li>
            ))}
          </ul>

          {analysis.stakes && (
            <>
              <h3 className="mt-4 text-sm font-medium text-neutral-700 dark:text-neutral-300">Value bets y stake sugerido</h3>
              <div className="mt-2 grid grid-cols-3 gap-3">
                {(["home", "draw", "away"] as const).map((outcome) => {
                  const s = analysis.stakes![outcome];
                  return (
                    <div
                      key={outcome}
                      className={`rounded-lg border p-3 text-center text-sm ${
                        s.isValueBet
                          ? "border-green-500 bg-green-50 dark:bg-green-950/30"
                          : "border-neutral-200 dark:border-neutral-800"
                      }`}
                    >
                      <p className="text-xs uppercase text-neutral-500">{s.label}</p>
                      <p className="mt-1 font-semibold">{s.decimalOdds.toFixed(2)}</p>
                      <p className="mt-1 text-xs text-neutral-500">
                        edge {s.edge >= 0 ? "+" : ""}
                        {(s.edge * 100).toFixed(1)}pp
                      </p>
                      {s.isValueBet && (
                        <p className="mt-1 text-xs font-medium text-green-700 dark:text-green-400">
                          stake {(s.suggestedStakePctBankroll * 100).toFixed(1)}%
                        </p>
                      )}
                    </div>
                  );
                })}
              </div>
            </>
          )}

          <h3 className="mt-4 text-sm font-medium text-neutral-700 dark:text-neutral-300">Narrativa</h3>
          <div className="mt-2 whitespace-pre-wrap text-sm text-neutral-700 dark:text-neutral-300">{analysis.narrative}</div>
        </div>
      )}

      {predictions && predictions.length > latestBatch.length && (
        <div className="mt-8">
          <h2 className="text-lg font-medium">Prediction history</h2>
          <p className="mt-1 text-xs text-neutral-500">
            Every generation is saved as a new snapshot — past predictions are never edited (CLAUDE.md section 29).
          </p>
          <table className="mt-3 w-full border-collapse text-sm">
            <thead>
              <tr className="border-b border-neutral-200 text-left dark:border-neutral-800">
                <th className="py-2">Date</th>
                <th>Selection</th>
                <th>Prob.</th>
                <th>EV</th>
                <th>Result</th>
              </tr>
            </thead>
            <tbody>
              {predictions.map((p) => (
                <tr key={p.id} className="border-b border-neutral-100 dark:border-neutral-900">
                  <td className="py-2">{new Date(p.predictionDate).toLocaleString()}</td>
                  <td>{p.selection}</td>
                  <td>{pct(p.probability)}</td>
                  <td>{p.expectedValue !== null ? `${(p.expectedValue * 100).toFixed(1)}%` : "–"}</td>
                  <td>{p.isCorrect === true ? "✓" : p.isCorrect === false ? "✗" : "–"}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </main>
  );
}

import { notFound } from "next/navigation";
import ReactMarkdown from "react-markdown";

import { getAnalysis, type StakeRecommendation } from "@/lib/mlService";

function ProbBar({ label, value }: { label: string; value: number }) {
  const pct = Math.round(value * 100);
  return (
    <div>
      <div className="flex justify-between text-sm">
        <span>{label}</span>
        <span className="font-medium">{pct}%</span>
      </div>
      <div className="mt-1 h-2 w-full rounded-full bg-neutral-200 dark:bg-neutral-800">
        <div className="h-2 rounded-full bg-emerald-600" style={{ width: `${pct}%` }} />
      </div>
    </div>
  );
}

function StakeRow({ stake }: { stake: StakeRecommendation }) {
  return (
    <tr className="border-t border-neutral-200 dark:border-neutral-800">
      <td className="py-2 pr-4">{stake.label}</td>
      <td className="py-2 pr-4">{stake.decimal_odds.toFixed(2)}</td>
      <td className={`py-2 pr-4 ${stake.edge > 0 ? "text-emerald-600" : "text-neutral-500"}`}>
        {(stake.edge * 100).toFixed(1)}pp
      </td>
      <td className="py-2 pr-4">
        {stake.is_value_bet ? (
          <span className="rounded bg-emerald-100 px-2 py-0.5 text-xs font-medium text-emerald-700 dark:bg-emerald-900/40 dark:text-emerald-400">
            value bet
          </span>
        ) : (
          <span className="text-xs text-neutral-400">sin valor</span>
        )}
      </td>
      <td className="py-2">{(stake.suggested_stake_pct_bankroll * 100).toFixed(1)}% bankroll</td>
    </tr>
  );
}

export default async function PartidoPage({
  params,
}: {
  params: Promise<{ fixtureId: string }>;
}) {
  const { fixtureId } = await params;
  const id = Number(fixtureId);
  if (!Number.isFinite(id)) notFound();

  const analysis = await getAnalysis(id).catch(() => null);
  if (!analysis) notFound();

  const { fixture, prediction, shap_top_features, odds, stakes, narrative } = analysis;

  return (
    <main className="mx-auto max-w-3xl px-4 py-12">
      <p className="text-sm text-neutral-500">
        {fixture.league_name} · {fixture.country} · Temporada {fixture.season}
      </p>
      <h1 className="mt-1 text-2xl font-bold tracking-tight">
        {fixture.home_team} vs {fixture.away_team}
      </h1>
      <p className="mt-1 text-sm text-neutral-500">
        {new Date(fixture.kickoff_at).toLocaleString("es-MX")} ·{" "}
        {fixture.venue ?? "Sede por confirmar"}
      </p>

      <section className="mt-8 space-y-3 rounded-lg border border-neutral-200 p-5 dark:border-neutral-800">
        <h2 className="text-sm font-semibold uppercase tracking-wide text-neutral-500">
          Probabilidades del modelo ({prediction.model_version})
        </h2>
        <ProbBar label="Local" value={prediction.probabilities.home} />
        <ProbBar label="Empate" value={prediction.probabilities.draw} />
        <ProbBar label="Visitante" value={prediction.probabilities.away} />
      </section>

      <section className="mt-6 rounded-lg border border-neutral-200 p-5 dark:border-neutral-800">
        <h2 className="text-sm font-semibold uppercase tracking-wide text-neutral-500">
          Features que más pesaron (SHAP)
        </h2>
        <ul className="mt-3 space-y-1 text-sm">
          {shap_top_features.map((item) => (
            <li key={item.feature} className="flex justify-between">
              <span className="text-neutral-600 dark:text-neutral-400">{item.feature}</span>
              <span className={item.impact > 0 ? "text-emerald-600" : "text-red-500"}>
                {item.impact > 0 ? "+" : ""}
                {item.impact.toFixed(3)}
              </span>
            </li>
          ))}
        </ul>
      </section>

      {odds && stakes && (
        <section className="mt-6 rounded-lg border border-neutral-200 p-5 dark:border-neutral-800">
          <h2 className="text-sm font-semibold uppercase tracking-wide text-neutral-500">
            Cuotas de mercado y stake sugerido (Kelly)
          </h2>
          <div className="mt-3 overflow-x-auto">
            <table className="w-full text-left text-sm">
              <thead>
                <tr className="text-neutral-500">
                  <th className="pb-2 pr-4 font-normal">Resultado</th>
                  <th className="pb-2 pr-4 font-normal">Cuota</th>
                  <th className="pb-2 pr-4 font-normal">Edge</th>
                  <th className="pb-2 pr-4 font-normal"></th>
                  <th className="pb-2 font-normal">Stake sugerido</th>
                </tr>
              </thead>
              <tbody>
                <StakeRow stake={stakes.home} />
                <StakeRow stake={stakes.draw} />
                <StakeRow stake={stakes.away} />
              </tbody>
            </table>
          </div>
        </section>
      )}

      <article className="analysis mt-8">
        <ReactMarkdown>{narrative}</ReactMarkdown>
      </article>

      <footer className="mt-12 border-t border-neutral-200 pt-4 text-xs text-neutral-400 dark:border-neutral-800">
        Herramienta informativa basada en un modelo estadístico. No garantiza resultados. Apostar
        implica riesgo — juega con responsabilidad. Si el juego deja de ser diversión, busca ayuda.
      </footer>
    </main>
  );
}

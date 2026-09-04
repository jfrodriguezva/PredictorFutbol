import { notFound } from "next/navigation";
import ReactMarkdown from "react-markdown";

import { generateAnalysis } from "@/lib/analysis";
import { getFixture, predictFixture } from "@/lib/mlService";

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

export default async function PartidoPage({
  params,
}: {
  params: Promise<{ fixtureId: string }>;
}) {
  const { fixtureId } = await params;
  const id = Number(fixtureId);
  if (!Number.isFinite(id)) notFound();

  const fixture = await getFixture(id).catch(() => null);
  if (!fixture) notFound();

  const prediction = await predictFixture(id);
  const analysis = await generateAnalysis(fixture, prediction);

  return (
    <main className="mx-auto max-w-3xl px-4 py-12">
      <p className="text-sm text-neutral-500">
        {fixture.league.name} · {fixture.league.country} · Temporada {fixture.league.season}
      </p>
      <h1 className="mt-1 text-2xl font-bold tracking-tight">
        {fixture.home_team.name} vs {fixture.away_team.name}
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

      <article className="analysis mt-8">
        <ReactMarkdown>{analysis}</ReactMarkdown>
      </article>

      <footer className="mt-12 border-t border-neutral-200 pt-4 text-xs text-neutral-400 dark:border-neutral-800">
        Herramienta informativa basada en un modelo estadístico. No garantiza resultados. Apostar
        implica riesgo — juega con responsabilidad. Si el juego deja de ser diversión, busca ayuda.
      </footer>
    </main>
  );
}

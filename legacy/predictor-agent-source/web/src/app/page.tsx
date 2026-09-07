"use client";

import Link from "next/link";
import { useState, type FormEvent } from "react";

import type { FixtureSummary } from "@/lib/mlService";

export default function Home() {
  const [query, setQuery] = useState("");
  const [results, setResults] = useState<FixtureSummary[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [searched, setSearched] = useState(false);

  async function handleSearch(event: FormEvent) {
    event.preventDefault();
    setLoading(true);
    setError(null);
    setSearched(true);
    try {
      const res = await fetch(`/api/fixtures?team=${encodeURIComponent(query)}`);
      if (!res.ok) throw new Error("No se pudo buscar partidos");
      setResults(await res.json());
    } catch (err) {
      setError(err instanceof Error ? err.message : "Error desconocido");
    } finally {
      setLoading(false);
    }
  }

  return (
    <main className="mx-auto max-w-3xl px-4 py-12">
      <h1 className="text-3xl font-bold tracking-tight">Predictor 1X2</h1>
      <p className="mt-2 text-sm text-neutral-500">
        Busca un equipo para analizar su partido con nuestro modelo de predicción y recibir un
        análisis ultra detallado estilo experto en apuestas.
      </p>

      <form onSubmit={handleSearch} className="mt-6 flex gap-2">
        <input
          value={query}
          onChange={(event) => setQuery(event.target.value)}
          placeholder="Ej. Chelsea, América, Cruz Azul, Liverpool..."
          className="flex-1 rounded-md border border-neutral-300 px-3 py-2 text-sm outline-none focus:border-neutral-500 dark:border-neutral-700 dark:bg-neutral-900"
        />
        <button
          type="submit"
          className="rounded-md bg-emerald-600 px-4 py-2 text-sm font-medium text-white hover:bg-emerald-700 disabled:opacity-50"
          disabled={loading || query.trim().length < 2}
        >
          {loading ? "Buscando..." : "Buscar"}
        </button>
      </form>

      {error && <p className="mt-4 text-sm text-red-600">{error}</p>}

      {searched && !loading && results.length === 0 && !error && (
        <p className="mt-6 text-sm text-neutral-500">Sin resultados. Prueba con otro equipo.</p>
      )}

      <ul className="mt-6 divide-y divide-neutral-200 dark:divide-neutral-800">
        {results.map((fx) => (
          <li key={fx.fixture_id}>
            <Link
              href={`/partido/${fx.fixture_id}`}
              className="flex items-center justify-between py-3 text-sm hover:underline"
            >
              <span>
                {fx.home_team.name} vs {fx.away_team.name}
              </span>
              <span className="text-neutral-500">
                {fx.league.name} · {new Date(fx.kickoff_at).toLocaleDateString("es-MX")}
              </span>
            </Link>
          </li>
        ))}
      </ul>

      <footer className="mt-16 border-t border-neutral-200 pt-4 text-xs text-neutral-400 dark:border-neutral-800">
        Herramienta informativa. No garantiza resultados. Apostar implica riesgo — juega con
        responsabilidad.
      </footer>
    </main>
  );
}

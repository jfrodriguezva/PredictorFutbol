import { getBackendHealth } from "@/lib/health";

export default async function Home() {
  const health = await getBackendHealth();

  return (
    <main className="flex flex-1 flex-col items-center justify-center gap-6 p-8">
      <div className="text-center">
        <h1 className="text-3xl font-semibold">Sports Predictor</h1>
        <p className="mt-2 text-sm text-neutral-500">
          Local sports analysis and prediction platform &mdash; Phase 1 skeleton.
        </p>
      </div>

      <div className="rounded-lg border border-neutral-200 px-6 py-4 text-sm dark:border-neutral-800">
        <p className="font-medium">Backend status</p>
        {health ? (
          <ul className="mt-2 space-y-1 text-neutral-600 dark:text-neutral-400">
            <li>Status: {health.status}</li>
            <li>Version: {health.version}</li>
            <li>Checked at (UTC): {health.checkedAtUtc}</li>
          </ul>
        ) : (
          <p className="mt-2 text-neutral-600 dark:text-neutral-400">
            No data available &mdash; backend API is not reachable.
          </p>
        )}
      </div>
    </main>
  );
}

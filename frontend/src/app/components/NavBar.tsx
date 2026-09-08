"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { useEffect, useState } from "react";
import { apiGet, apiPost } from "@/lib/api";
import type { ValueBetNotification } from "@/lib/types";

const links = [
  { href: "/", label: "Health" },
  { href: "/tracked-competitions", label: "Competitions" },
  { href: "/models", label: "Models" },
  { href: "/value-bets", label: "Value Bets" },
  { href: "/accuracy", label: "Accuracy" },
  { href: "/progol", label: "Progol" },
];

const POLL_INTERVAL_MS = 60_000;

function pct(value: number): string {
  return `${(value * 100).toFixed(1)}%`;
}

function NotificationBell() {
  const [notifications, setNotifications] = useState<ValueBetNotification[]>([]);
  const [open, setOpen] = useState(false);

  async function load() {
    try {
      setNotifications(await apiGet<ValueBetNotification[]>("/api/notifications/value-bets?unreadOnly=true"));
    } catch {
      // Silent — a failed poll just means the badge stays as it was.
    }
  }

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect -- intentional fetch-on-mount
    load();
    const interval = setInterval(load, POLL_INTERVAL_MS);
    return () => clearInterval(interval);
  }, []);

  async function markRead(id: string) {
    try {
      await apiPost(`/api/notifications/value-bets/${id}/read`);
      setNotifications((prev) => prev.filter((n) => n.id !== id));
    } catch {
      // Ignore — worst case it reappears on the next poll.
    }
  }

  return (
    <div className="relative ml-auto">
      <button
        onClick={() => setOpen((v) => !v)}
        className="relative rounded p-1.5 text-neutral-500 hover:text-neutral-900 dark:hover:text-neutral-100"
        aria-label="Value bet notifications"
      >
        🔔
        {notifications.length > 0 && (
          <span className="absolute -right-1 -top-1 flex h-4 min-w-4 items-center justify-center rounded-full bg-red-600 px-1 text-[10px] font-medium text-white">
            {notifications.length}
          </span>
        )}
      </button>

      {open && (
        <div className="absolute right-0 z-10 mt-2 w-80 rounded-lg border border-neutral-200 bg-white p-2 shadow-lg dark:border-neutral-800 dark:bg-neutral-950">
          <p className="px-2 py-1 text-xs font-medium text-neutral-500">Value bets detectados</p>
          {notifications.length === 0 ? (
            <p className="px-2 py-2 text-sm text-neutral-500">Nada nuevo por ahora.</p>
          ) : (
            <ul className="max-h-80 space-y-1 overflow-y-auto">
              {notifications.map((n) => (
                <li key={n.id} className="rounded p-2 text-sm hover:bg-neutral-50 dark:hover:bg-neutral-900">
                  <div className="flex items-center justify-between gap-2">
                    <Link href={`/matches/${n.matchId}`} className="text-blue-600 hover:underline dark:text-blue-400" onClick={() => setOpen(false)}>
                      {n.selection} — EV {n.expectedValue >= 0 ? "+" : ""}
                      {pct(n.expectedValue)}
                    </Link>
                    <button
                      onClick={() => markRead(n.id)}
                      className="shrink-0 text-xs text-neutral-400 hover:text-neutral-700 dark:hover:text-neutral-200"
                    >
                      marcar leída
                    </button>
                  </div>
                  <p className="mt-0.5 text-xs text-neutral-500">{new Date(n.detectedAt).toLocaleString()}</p>
                </li>
              ))}
            </ul>
          )}
        </div>
      )}
    </div>
  );
}

export function NavBar() {
  const pathname = usePathname();

  return (
    <nav className="border-b border-neutral-200 dark:border-neutral-800">
      <div className="mx-auto flex max-w-5xl items-center gap-6 px-6 py-3">
        <span className="font-semibold">Sports Predictor</span>
        <ul className="flex gap-4 text-sm">
          {links.map((link) => (
            <li key={link.href}>
              <Link
                href={link.href}
                className={
                  pathname === link.href
                    ? "font-medium text-neutral-900 dark:text-neutral-100"
                    : "text-neutral-500 hover:text-neutral-900 dark:hover:text-neutral-100"
                }
              >
                {link.label}
              </Link>
            </li>
          ))}
        </ul>
        <NotificationBell />
      </div>
    </nav>
  );
}

"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";

const links = [
  { href: "/", label: "Health" },
  { href: "/tracked-competitions", label: "Competitions" },
  { href: "/models", label: "Models" },
  { href: "/value-bets", label: "Value Bets" },
  { href: "/accuracy", label: "Accuracy" },
  { href: "/progol", label: "Progol" },
];

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
      </div>
    </nav>
  );
}

# SportsPredictor Frontend

Next.js + TypeScript + Tailwind CSS dashboard for the SportsPredictor platform.

The frontend never calls API-FOOTBALL directly; it only talks to the SportsPredictor.Api backend.

## Development

```bash
npm install
npm run dev
```

Copy `.env.example` to `.env.local` and set `NEXT_PUBLIC_API_BASE_URL` to point at the backend
(default `http://localhost:5000`).

## Build

```bash
npm run build
npm run start
```

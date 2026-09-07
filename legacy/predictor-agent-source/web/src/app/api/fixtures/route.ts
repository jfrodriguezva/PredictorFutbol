import { NextRequest, NextResponse } from "next/server";

import { searchFixtures } from "@/lib/mlService";

export async function GET(request: NextRequest) {
  const { searchParams } = new URL(request.url);
  const team = searchParams.get("team") ?? undefined;
  const leagueId = searchParams.get("league_id");
  const status = searchParams.get("status") ?? undefined;

  try {
    const fixtures = await searchFixtures({
      team,
      league_id: leagueId ? Number(leagueId) : undefined,
      status,
      limit: 30,
    });
    return NextResponse.json(fixtures);
  } catch (error) {
    return NextResponse.json(
      { error: error instanceof Error ? error.message : "Error desconocido" },
      { status: 502 },
    );
  }
}

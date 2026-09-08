export interface TrackedCompetition {
  id: string;
  competitionId: string;
  competitionName: string;
  externalLeagueId: number;
  season: number;
  enabled: boolean;
  priority: number;
}

export interface Match {
  id: string;
  homeTeamName: string;
  homeTeamId: string;
  awayTeamName: string;
  awayTeamId: string;
  matchDateUtc: string;
  status: string;
  homeScore: number | null;
  awayScore: number | null;
}

export interface ModelVersion {
  id: string;
  modelName: string;
  version: string;
  algorithm: string;
  active: boolean;
  trainedAt: string;
  brierScore: number | null;
  logLoss: number | null;
}

export interface Prediction {
  id: string;
  matchId: string;
  modelVersionId: string;
  predictionDate: string;
  market: string;
  selection: string;
  probability: number;
  expectedValue: number | null;
  valueCategory: string | null;
  recommended: boolean;
  actualOutcome: string | null;
  isCorrect: boolean | null;
}

export interface GeneratePredictionResult {
  matchId: string;
  modelVersionId: string;
  predictionDate: string;
  selections: Prediction[];
}

export interface EvaluateMatchResult {
  matchId: string;
  actualOutcome: string;
  predictions: Prediction[];
}

export interface LearnFromMatchResult {
  evaluation: EvaluateMatchResult;
  newModelVersion: ModelVersion;
}

export interface ShapFeature {
  feature: string;
  impact: number;
}

export interface Stake {
  label: string;
  decimalOdds: number;
  impliedProbability: number;
  modelProbability: number;
  edge: number;
  isValueBet: boolean;
  kellyFractionFull: number;
  suggestedStakePctBankroll: number;
}

export interface PredictionExplanation {
  id: string;
  matchId: string;
  modelVersionId: string;
  generatedAt: string;
  home: number;
  draw: number;
  away: number;
  shapTopFeatures: ShapFeature[];
  stakes: Record<"home" | "draw" | "away", Stake> | null;
  narrative: string;
}

export interface ValueBetNotification {
  id: string;
  matchId: string;
  predictionId: string;
  selection: string;
  expectedValue: number;
  detectedAt: string;
  read: boolean;
}

export interface AccuracySummary {
  totalEvaluated: number;
  correctCount: number;
  accuracy: number;
  recommendedTotal: number;
  recommendedCorrect: number;
  recommendedAccuracy: number;
  recentMisses: {
    matchId: string;
    market: string;
    selection: string;
    probability: number;
    actualOutcome: string;
    predictionDate: string;
  }[];
}

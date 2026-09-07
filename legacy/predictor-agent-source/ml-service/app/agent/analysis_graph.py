"""Agente LangGraph que orquesta el análisis ultra detallado de un partido:

    predict -> explain (SHAP) -> odds -> [stake si hay odds] -> narrative

`odds` decide con un edge condicional si el grafo pasa por `stake` (cuando sí hay
cuotas de mercado) o salta directo a `narrative` (cuando no hay ODDS_API_KEY o el
partido no tiene cuotas disponibles) — la única razón real para usar un grafo aquí
en vez de una función lineal es esa ramificación explícita según qué datos existen.
"""

from __future__ import annotations

from typing import Any, TypedDict

from langgraph.graph import END, START, StateGraph

from app.decision.staking import recommend_stakes
from app.features.explain import explain_prediction
from app.inference import compute_prediction, load_model
from app.ingestion.odds_api import find_odds_for_fixture
from app.narrative import generate_narrative


class AnalysisState(TypedDict, total=False):
    fixture_id: int
    fixture: dict[str, Any]
    prediction: dict[str, Any]
    raw_features: dict[str, Any]
    predicted_label_index: int
    shap_top_features: list[dict[str, Any]]
    odds: dict[str, float] | None
    stakes: dict[str, Any] | None
    narrative: str


def node_predict(state: AnalysisState) -> dict:
    result = compute_prediction(state["fixture_id"])
    return {
        "fixture": result["fixture_summary"],
        "prediction": result["response"],
        "raw_features": result["raw_features"],
        "predicted_label_index": result["predicted_label_index"],
    }


def node_explain(state: AnalysisState) -> dict:
    bundle = load_model()
    top_features = explain_prediction(bundle, state["raw_features"], state["predicted_label_index"])
    return {"shap_top_features": top_features}


def node_odds(state: AnalysisState) -> dict:
    fixture = state["fixture"]
    odds = find_odds_for_fixture(fixture["league_id"], fixture["home_team"], fixture["away_team"])
    return {"odds": odds}


def _route_after_odds(state: AnalysisState) -> str:
    return "stake" if state.get("odds") else "narrative"


def node_stake(state: AnalysisState) -> dict:
    stakes = recommend_stakes(state["prediction"]["probabilities"], state["odds"])
    return {"stakes": stakes}


def node_narrative(state: AnalysisState) -> dict:
    narrative = generate_narrative(
        fixture=state["fixture"],
        prediction=state["prediction"],
        shap_top_features=state["shap_top_features"],
        odds=state.get("odds"),
        stakes=state.get("stakes"),
    )
    return {"narrative": narrative}


def build_graph():
    graph = StateGraph(AnalysisState)
    graph.add_node("predict", node_predict)
    graph.add_node("explain", node_explain)
    graph.add_node("odds", node_odds)
    graph.add_node("stake", node_stake)
    graph.add_node("narrative", node_narrative)

    graph.add_edge(START, "predict")
    graph.add_edge("predict", "explain")
    graph.add_edge("explain", "odds")
    graph.add_conditional_edges("odds", _route_after_odds, {"stake": "stake", "narrative": "narrative"})
    graph.add_edge("stake", "narrative")
    graph.add_edge("narrative", END)
    return graph.compile()


_compiled_graph = None


def run_analysis(fixture_id: int) -> dict:
    global _compiled_graph
    if _compiled_graph is None:
        _compiled_graph = build_graph()

    final_state = _compiled_graph.invoke({"fixture_id": fixture_id})
    return {
        "fixture": final_state["fixture"],
        "prediction": final_state["prediction"],
        "shap_top_features": final_state["shap_top_features"],
        "odds": final_state.get("odds"),
        "stakes": final_state.get("stakes"),
        "narrative": final_state["narrative"],
    }

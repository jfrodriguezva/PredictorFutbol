from fastapi import FastAPI

from app.api import fixtures, predict

app = FastAPI(title="Predictor Futbol - ML Service")
app.include_router(fixtures.router)
app.include_router(predict.router)


@app.get("/health")
def health():
    return {"status": "ok"}

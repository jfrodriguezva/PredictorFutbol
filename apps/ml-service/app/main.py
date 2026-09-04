from fastapi import FastAPI

app = FastAPI(title="Predictor Futbol - ML Service")


@app.get("/health")
def health():
    return {"status": "ok"}

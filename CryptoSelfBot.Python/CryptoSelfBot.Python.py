from fastapi import FastAPI
import uvicorn

app = FastAPI(title="CryptoSelfBot NLP Service")

@app.get("/health")
async def health():
    return {"status": "ok", "service": "CryptoSelfBot NLP"}

@app.post("/api/nlp/sentiment")
async def analyze_sentiment(request: list[str]):
    if not request:
        return {"score": 0.0, "detail": "No headlines provided"}
    return {"score": 0.0, "detail": f"Received {len(request)} headlines (FinBERT pending)"}

if __name__ == "__main__":
    uvicorn.run(app, host="127.0.0.1", port=5000)
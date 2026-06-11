AI Service integration for CryptoSelfBot

This folder contains a minimal FastAPI scaffold that will attempt to:

- Detect existing OpenClaude-Portable installation under IAS/ and proxy requests to it.
- Fall back to a tinyllama-based inference if a usable model is present under IAS/tinyllama/latest.

Usage (Windows PowerShell):

1. Create and activate a virtual env:

   python -m venv .venv
   .\.venv\Scripts\Activate.ps1

2. Install dependencies:

   pip install fastapi uvicorn httpx

3. Run the service:

   uvicorn app:app --host 127.0.0.1 --port 8000

Notes:
- This scaffold is lightweight: it proxies to OpenClaude if available, otherwise returns a placeholder response.
- To fully enable local model inference you must place compatible model files (ggml/transformers) in IAS/tinyllama/latest and adapt the code to load them.

from fastapi import FastAPI, HTTPException
from pydantic import BaseModel
import os
import subprocess
import httpx
import json

app = FastAPI()

ROOT = os.path.dirname(os.path.dirname(__file__))
IAS_ROOT = os.path.join(ROOT, 'IAS')
TOOLS_DIR = os.path.join(os.path.dirname(__file__), 'tools')

# Load models configuration
MODELS_CFG_PATH = os.path.join(os.path.dirname(__file__), 'models.json')
if os.path.exists(MODELS_CFG_PATH):
    with open(MODELS_CFG_PATH, 'r', encoding='utf-8') as f:
        MODELS_CFG = json.load(f)
else:
    MODELS_CFG = { 'models_dir': os.path.join(IAS_ROOT, 'data', 'models'), 'default_model': None, 'aliases': {} }


class InferRequest(BaseModel):
    prompt: str


def find_model_path(name_or_alias: str):
    models_dir = MODELS_CFG.get('models_dir')
    alias_map = MODELS_CFG.get('aliases', {})
    name = alias_map.get(name_or_alias, name_or_alias)
    candidate = os.path.join(models_dir, name)
    if os.path.exists(candidate):
        return candidate
    # fallback: if name_or_alias is a filename present
    direct = os.path.join(models_dir, name_or_alias)
    if os.path.exists(direct):
        return direct
    return None


@app.get('/models')
def list_models():
    models_dir = MODELS_CFG.get('models_dir')
    files = []
    if os.path.exists(models_dir):
        for f in os.listdir(models_dir):
            if f.lower().endswith('.gguf'):
                files.append(f)
    return {'models_dir': models_dir, 'models': files, 'default_model': MODELS_CFG.get('default_model')}


@app.post('/switch_model')
def switch_model(body: dict):
    name = body.get('model')
    if not name:
        raise HTTPException(status_code=400, detail='model required')
    path = find_model_path(name)
    if not path:
        raise HTTPException(status_code=404, detail='model not found')
    MODELS_CFG['default_model'] = os.path.basename(path)
    with open(MODELS_CFG_PATH, 'w', encoding='utf-8') as f:
        json.dump(MODELS_CFG, f, indent=2)
    return {'status': 'ok', 'default_model': MODELS_CFG['default_model']}


@app.post('/infer')
async def infer(req: InferRequest):
    # prefer direct llama.cpp executable invocation
    model_file = find_model_path(MODELS_CFG.get('default_model') or '')
    if not model_file:
        # no configured default, attempt tinyllama
        tiny = find_model_path('TinyLLama-v0.Q8_0.gguf')
        if tiny:
            model_file = tiny

    if not model_file:
        raise HTTPException(status_code=503, detail='No GGUF model configured or found')

    # Locate llama.cpp native executable in tools dir
    runners = [
        os.path.join(TOOLS_DIR, 'main.exe'),
        os.path.join(TOOLS_DIR, 'llama.exe'),
        os.path.join(TOOLS_DIR, 'bin', 'main.exe')
    ]

    exe = None
    for r in runners:
        if os.path.exists(r):
            exe = r
            break

    if not exe:
        raise HTTPException(status_code=503, detail='llama.cpp executable not found in tools; place precompiled binary as main.exe or llama.exe')

    # Build command to run the binary with model and prompt
    # Use simple invocation: main.exe -m model.gguf -p "prompt" --n_predict 128
    cmd = [exe, '-m', model_file, '-p', req.prompt, '--n_predict', '128']
    try:
        proc = subprocess.run(cmd, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True, timeout=60)
        if proc.returncode != 0:
            raise HTTPException(status_code=500, detail=f"Execution failed: {proc.stderr}")
        # Parse output: return entire stdout
        return {'result': proc.stdout}
    except subprocess.TimeoutExpired:
        raise HTTPException(status_code=504, detail='llama.cpp execution timed out')


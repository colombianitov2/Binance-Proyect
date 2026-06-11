from fastapi import FastAPI, HTTPException
from pydantic import BaseModel
from fastapi.responses import JSONResponse
import httpx

app = FastAPI()


class ConvertRequest(BaseModel):
    from_symbol: str
    to_symbol: str
    amount: float


@app.get("/")
async def health():
    return {"status": "ok"}


async def _get_price(symbol: str) -> float | None:
    """Consulta el precio ticker en Binance para el símbolo dado. Devuelve None si no existe."""
    url = f"https://api.binance.com/api/v3/ticker/price?symbol={symbol}"
    async with httpx.AsyncClient(timeout=5.0) as client:
        try:
            r = await client.get(url)
            if r.status_code != 200:
                return None
            data = r.json()
            price = float(data.get("price", 0))
            return price
        except Exception:
            return None


@app.post("/convert")
async def convert(req: ConvertRequest):
    from_sym = req.from_symbol.strip().upper()
    to_sym = req.to_symbol.strip().upper()
    amount = float(req.amount)

    if from_sym == to_sym:
        return JSONResponse({
            "from_symbol": from_sym,
            "to_symbol": to_sym,
            "amount": amount,
            "converted_amount": amount,
            "route": [from_sym],
            "note": "Same symbol, no conversion needed"
        })

    # Try direct pair FROM+TO
    direct = from_sym + to_sym
    price = await _get_price(direct)
    if price is not None and price > 0:
        converted = amount * price
        return JSONResponse({
            "from_symbol": from_sym,
            "to_symbol": to_sym,
            "amount": amount,
            "converted_amount": converted,
            "route": [direct],
            "note": "Direct pair used (price is quote per base)"
        })

    # Try reversed pair TO+FROM -> invert
    rev = to_sym + from_sym
    price_rev = await _get_price(rev)
    if price_rev is not None and price_rev > 0:
        converted = amount / price_rev
        return JSONResponse({
            "from_symbol": from_sym,
            "to_symbol": to_sym,
            "amount": amount,
            "converted_amount": converted,
            "route": [rev],
            "note": "Reversed pair used (inverted price)"
        })

    # Fallback: try via USDT as common quote
    intermediate = "USDT"
    # from -> USDT
    f_usdt = await _get_price(from_sym + intermediate)
    if f_usdt is None:
        f_usdt_rev = await _get_price(intermediate + from_sym)
        if f_usdt_rev is None:
            f_usdt = None
        else:
            f_usdt = 1 / f_usdt_rev if f_usdt_rev > 0 else None

    # to -> USDT
    t_usdt = await _get_price(to_sym + intermediate)
    if t_usdt is None:
        t_usdt_rev = await _get_price(intermediate + to_sym)
        if t_usdt_rev is None:
            t_usdt = None
        else:
            t_usdt = 1 / t_usdt_rev if t_usdt_rev > 0 else None

    if f_usdt is not None and t_usdt is not None and f_usdt > 0 and t_usdt > 0:
        # amount in FROM -> multiply by f_usdt to get USDT, then divide by t_usdt to get TO
        amount_usdt = amount * f_usdt
        converted = amount_usdt / t_usdt
        return JSONResponse({
            "from_symbol": from_sym,
            "to_symbol": to_sym,
            "amount": amount,
            "converted_amount": converted,
            "route": [from_sym + intermediate, to_sym + intermediate],
            "note": "Conversion via USDT"
        })

    raise HTTPException(status_code=400, detail="No se encontró ruta de conversión para los símbolos proporcionados")

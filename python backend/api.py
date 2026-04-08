from fastapi import FastAPI, UploadFile, File, HTTPException
from pydantic import BaseModel, Field
from starlette.responses import PlainTextResponse

from nmt_service import NllbTranslator
from subtitle_srt import translate_srt
from rag_store import RagStore

# Quel sera l'usage (interne ? Micro service backend ?)
# SRT mais faudra-il gérer les batch de documents/fichier ?
# Volumétrie ? (Combien de requête attends-on par jour ?)
# Implémentation du RAG + intégration
# Comment s'assurer de la fidélité des traductions ?
# Ajoute d'un listing de langue supportée différent (from Teams)
# Doit tourner sur GPU

app = FastAPI(
    title="NLLB Translator API",
    version="0.2.0",
    description="API de traduction texte/fichier/SRT avec fallback automatique pour gros textes, + RAG v1 (retrieval only)."
)

translator = NllbTranslator()
rag = RagStore()

MAX_TEXT_INPUT_CHARS = 200_000
MAX_UPLOAD_BYTES = 25 * 1024 * 1024
DIRECT_TRANSLATE_THRESHOLD_CHARS = 1_800
LONG_TEXT_CHUNK_CHARS = 900

# ---------- Models ----------
class TranslateRequest(BaseModel):
    text: str = Field(..., min_length=1)
    sourceLanguage: str = "fr"
    targetLanguage: str = "en"
    maxNewTokens: int = 512
    numBeams: int = 5


def _translate_with_large_text_fallback(
    text: str,
    source_language: str,
    target_language: str,
    max_new_tokens: int,
    num_beams: int,
) -> dict:
    source = (text or "").strip()
    if len(source) > MAX_TEXT_INPUT_CHARS:
        raise HTTPException(
            status_code=413,
            detail=f"Input text too large. Limit is {MAX_TEXT_INPUT_CHARS} characters."
        )

    if len(source) <= DIRECT_TRANSLATE_THRESHOLD_CHARS:
        res = translator.translate(
            source,
            source_language,
            target_language,
            max_new_tokens=max_new_tokens,
            num_beams=num_beams,
        )
        return {
            "translatedText": res.translated_txt,
            "device": res.device,
            "durationMs": res.duration_ms,
            "chunkCount": 1,
            "chunks": None,
        }

    # Fallback pour texte long: on repasse en mode chunking contrôlé pour garantir une traduction complète.
    return translator.translate_long(
        source,
        source_language,
        target_language,
        max_new_tokens=max_new_tokens,
        max_chars=LONG_TEXT_CHUNK_CHARS,
        num_beams=num_beams,
        preserve_paragraphs=True,
        debug=False,
    )


class TranslateResponse(BaseModel):
    translatedText: str
    device: str
    durationMs: int
    chunkCount: int | None = None
    chunks: list[dict] | None = None


class RagAddResponse(BaseModel):
    docId: str
    addedChunks: int
    totalChunks: int | None = None
    indexSize: int | None = None


class RagQueryRequest(BaseModel):
    query: str = Field(..., min_length=1)
    topK: int = Field(5, ge=1, le=20)


class RagQueryResponse(BaseModel):
    results: list[dict]
    stats: dict


# ---------- Basic ----------
@app.get("/")
def root():
    return {
        "message": "NLLB Translator API is running",
        "docs": "/docs",
        "health": "/health",
        "translate": "/translate",
        "translateFile": "/translate/file",
        "translateFileJson": "/translate/file/json",
        "translateSrt": "/translate/srt",
        "ragDocuments": "/rag/documents",
        "ragQuery": "/rag/query",
    }

@app.get("/health")
def health():
    return {
        "status": "ok",
        "device": translator.device,
        "startupMs": translator.startup_ms,
        "model": translator.model_name,
        "rag": rag.stats(),
    }

# ---------- A) Translate text ----------
@app.post("/translate", response_model=TranslateResponse)
def translate(req: TranslateRequest):
    return _translate_with_large_text_fallback(
        req.text,
        req.sourceLanguage,
        req.targetLanguage,
        req.maxNewTokens,
        req.numBeams,
    )

# ---------- A) Translate file (plain text response) ----------
@app.post("/translate/file", response_class=PlainTextResponse)
async def translate_file(
    file: UploadFile = File(...),
    sourceLanguage: str = "fr",
    targetLanguage: str = "en",
    maxNewTokens: int = 512,
    numBeams: int = 5,
):
    if file.size is not None and file.size > MAX_UPLOAD_BYTES:
        raise HTTPException(status_code=413, detail=f"File too large. Limit is {MAX_UPLOAD_BYTES} bytes.")

    content = await file.read()
    if len(content) > MAX_UPLOAD_BYTES:
        raise HTTPException(status_code=413, detail=f"File too large. Limit is {MAX_UPLOAD_BYTES} bytes.")

    text = content.decode("utf-8", errors="replace")

    out = _translate_with_large_text_fallback(
        text,
        sourceLanguage,
        targetLanguage,
        maxNewTokens,
        numBeams,
    )
    return out["translatedText"]

# ---------- A) Translate file (JSON response) ----------
@app.post("/translate/file/json")
async def translate_file_json(
    file: UploadFile = File(...),
    sourceLanguage: str = "fr",
    targetLanguage: str = "en",
    maxNewTokens: int = 512,
    numBeams: int = 5,
):
    if file.size is not None and file.size > MAX_UPLOAD_BYTES:
        raise HTTPException(status_code=413, detail=f"File too large. Limit is {MAX_UPLOAD_BYTES} bytes.")

    content = await file.read()
    if len(content) > MAX_UPLOAD_BYTES:
        raise HTTPException(status_code=413, detail=f"File too large. Limit is {MAX_UPLOAD_BYTES} bytes.")

    text = content.decode("utf-8", errors="replace")

    return _translate_with_large_text_fallback(
        text,
        sourceLanguage,
        targetLanguage,
        maxNewTokens,
        numBeams,
    )

# ---------- A) Translate SRT ----------
@app.post("/translate/srt", response_class=PlainTextResponse)
async def translate_srt_file(
    file: UploadFile = File(...),
    sourceLanguage: str = "fr",
    targetLanguage: str = "en",
    maxNewTokens: int = 512,
    numBeams: int = 5,
):
    if file.size is not None and file.size > MAX_UPLOAD_BYTES:
        raise HTTPException(status_code=413, detail=f"File too large. Limit is {MAX_UPLOAD_BYTES} bytes.")

    content = await file.read()
    if len(content) > MAX_UPLOAD_BYTES:
        raise HTTPException(status_code=413, detail=f"File too large. Limit is {MAX_UPLOAD_BYTES} bytes.")

    srt_text = content.decode("utf-8", errors="replace")

    out_srt = translate_srt(
        srt_text,
        translator=translator,
        source_language=sourceLanguage,
        target_language=targetLanguage,
        max_new_tokens=maxNewTokens,
        num_beams=numBeams,
    )
    return out_srt

# ---------- B) RAG: add documents ----------
@app.post("/rag/documents")
async def rag_add_document(
    file: UploadFile = File(...),
    docId: str | None = None,
    maxCharsPerChunk: int = 900,
):
    if file.size is not None and file.size > MAX_UPLOAD_BYTES:
        raise HTTPException(status_code=413, detail=f"File too large. Limit is {MAX_UPLOAD_BYTES} bytes.")

    content = await file.read()
    if len(content) > MAX_UPLOAD_BYTES:
        raise HTTPException(status_code=413, detail=f"File too large. Limit is {MAX_UPLOAD_BYTES} bytes.")

    text = content.decode("utf-8", errors="replace")

    res = rag.add_document(
        text=text,
        doc_id=docId,
        max_chars_per_chunk=maxCharsPerChunk,
        metadata={"filename": file.filename},
    )
    return res

# ---------- B) RAG: query ----------
@app.post("/rag/query", response_model=RagQueryResponse)
def rag_query(req: RagQueryRequest):
    results = rag.query(req.query, top_k=req.topK)
    return {"results": results, "stats": rag.stats()}

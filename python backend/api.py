from fastapi import FastAPI, UploadFile, File
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
    description="API pour traduire du texte (court/long via chunking), traduire des fichiers, SRT, + RAG v1 (retrieval only)."
)

translator = NllbTranslator()
rag = RagStore()

# ---------- Models ----------
class TranslateRequest(BaseModel):
    text: str = Field(..., min_length=1)
    sourceLanguage: str = "fr"
    targetLanguage: str = "en"
    maxNewTokens: int = 512
    numBeams: int = 5


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
    res = translator.translate(
        req.text,
        req.sourceLanguage,
        req.targetLanguage,
        max_new_tokens=req.maxNewTokens,
        num_beams=req.numBeams,
    )
    return {
        "translatedText": res.translated_txt,
        "device": res.device,
        "durationMs": res.duration_ms,
        "chunkCount": 1,
        "chunks": None,
    }

# ---------- A) Translate file (plain text response) ----------
@app.post("/translate/file", response_class=PlainTextResponse)
async def translate_file(
    file: UploadFile = File(...),
    sourceLanguage: str = "fr",
    targetLanguage: str = "en",
    maxNewTokens: int = 512,
    numBeams: int = 5,
):
    content = await file.read()
    text = content.decode("utf-8", errors="replace")

    res = translator.translate(
        text,
        sourceLanguage,
        targetLanguage,
        max_new_tokens=maxNewTokens,
        num_beams=numBeams,
    )
    return res.translated_txt

# ---------- A) Translate file (JSON response) ----------
@app.post("/translate/file/json")
async def translate_file_json(
    file: UploadFile = File(...),
    sourceLanguage: str = "fr",
    targetLanguage: str = "en",
    maxNewTokens: int = 512,
    numBeams: int = 5,
):
    content = await file.read()
    text = content.decode("utf-8", errors="replace")

    res = translator.translate(
        text,
        sourceLanguage,
        targetLanguage,
        max_new_tokens=maxNewTokens,
        num_beams=numBeams,
    )
    return {
        "translatedText": res.translated_txt,
        "device": res.device,
        "durationMs": res.duration_ms,
        "chunkCount": 1,
        "chunks": None,
    }

# ---------- A) Translate SRT ----------
@app.post("/translate/srt", response_class=PlainTextResponse)
async def translate_srt_file(
    file: UploadFile = File(...),
    sourceLanguage: str = "fr",
    targetLanguage: str = "en",
    maxNewTokens: int = 512,
    numBeams: int = 5,
):
    content = await file.read()
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
    content = await file.read()
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

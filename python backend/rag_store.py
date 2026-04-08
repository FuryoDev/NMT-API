from __future__ import annotations

from dataclasses import dataclass
from typing import Any, Dict, List, Optional, Tuple
import numpy as np

from sentence_transformers import SentenceTransformer
import faiss

from chunking import chunk_text

@dataclass
class RagChunk:
    chunk_id: str
    doc_id: str
    text: str
    metadata: Dict[str, Any]

class RagStore:
    """
    RAG v1:
    - In-memory store + FAISS index
    - Embeddings via sentence-transformers
    - Retrieval top-k
    """
    def __init__(self, embedding_model: str = "sentence-transformers/all-MiniLM-L6-v2") -> None:
        self.embedding_model_name = embedding_model
        self.embedder = SentenceTransformer(embedding_model)

        # dimension embeddings
        test_vec = self.embedder.encode(["test"], normalize_embeddings=True)
        self.dim = int(test_vec.shape[1])

        # FAISS index (cosine similarity via inner product sur vecteurs normalisés)
        self.index = faiss.IndexFlatIP(self.dim)

        self._chunks: List[RagChunk] = []
        self._doc_counter = 0

    def _embed(self, texts: List[str]) -> np.ndarray:
        vecs = self.embedder.encode(texts, normalize_embeddings=True)
        if not isinstance(vecs, np.ndarray):
            vecs = np.array(vecs)
        return vecs.astype("float32")

    def add_document(
        self,
        text: str,
        doc_id: Optional[str] = None,
        max_chars_per_chunk: int = 900,
        metadata: Optional[Dict[str, Any]] = None,
    ) -> Dict[str, Any]:
        text = (text or "").strip()
        if not text:
            return {"docId": doc_id or "doc_empty", "addedChunks": 0}

        self._doc_counter += 1
        doc_id = doc_id or f"doc_{self._doc_counter}"
        metadata = metadata or {}

        chunks = chunk_text(text, max_chars=max_chars_per_chunk)
        chunk_texts = [c.text for c in chunks]
        if not chunk_texts:
            return {"docId": doc_id, "addedChunks": 0}

        vecs = self._embed(chunk_texts)
        self.index.add(vecs)

        start_idx = len(self._chunks)
        for i, c in enumerate(chunks):
            self._chunks.append(
                RagChunk(
                    chunk_id=f"{doc_id}_chunk_{i}",
                    doc_id=doc_id,
                    text=c.text,
                    metadata={**metadata, "chunkIndex": i},
                )
            )

        return {
            "docId": doc_id,
            "addedChunks": len(chunks),
            "totalChunks": len(self._chunks),
            "indexSize": self.index.ntotal,
            "startIndex": start_idx,
        }

    def query(self, query_text: str, top_k: int = 5) -> List[Dict[str, Any]]:
        query_text = (query_text or "").strip()
        if not query_text or self.index.ntotal == 0:
            return []

        q = self._embed([query_text])
        scores, idxs = self.index.search(q, top_k)

        results: List[Dict[str, Any]] = []
        for score, idx in zip(scores[0].tolist(), idxs[0].tolist()):
            if idx < 0 or idx >= len(self._chunks):
                continue
            ch = self._chunks[idx]
            results.append(
                {
                    "score": float(score),
                    "docId": ch.doc_id,
                    "chunkId": ch.chunk_id,
                    "text": ch.text,
                    "metadata": ch.metadata,
                }
            )
        return results

    def stats(self) -> Dict[str, Any]:
        return {
            "embeddingModel": self.embedding_model_name,
            "dimension": self.dim,
            "totalChunks": len(self._chunks),
            "indexSize": int(self.index.ntotal),
        }

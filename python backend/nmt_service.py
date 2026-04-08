from __future__ import annotations

import time
from dataclasses import dataclass
from typing import Any, Dict, List, Optional

import torch
from transformers import AutoTokenizer, AutoModelForSeq2SeqLM

from chunking import chunk_text, Chunk
from preprocessing import normalize_text

# -------------------------------------------------------------------
# CONFIGURATION PRINCIPALE DU MODÈLE
# -------------------------------------------------------------------
# Logique business :
# Ce projet expose un moteur de traduction automatique via API.
# Le "coeur" de cette traduction est un modèle NMT déjà entraîné,
# ici un modèle NLLB de Meta disponible sur Hugging Face.
#
# Pourquoi cette constante ?
# - centraliser le nom du modèle utilisé,
# - faciliter un futur remplacement,
# - rendre le service configurable plus tard.
MODEL_NAME = "facebook/nllb-200-distilled-600M"

LANG_MAP = {
    "fr": "fra_Latn",
    "en": "eng_Latn",
    "nl": "nld_Latn",
    "ar": "arb_Arab",
    "de": "deu_Latn",
    "es": "spa_Latn",
    "it": "ita_Latn",
}


@dataclass
class TranslationResult:
    """
    Représente le résultat d’une traduction simple + qq données pour le monitoring.
    """
    translated_txt: str
    source_lang: str
    target_lang: str
    device: str
    duration_ms: int


class NllbTranslator:

    def __init__(self, model_name: str = MODEL_NAME, device: Optional[str] = None) -> None:
        """
        Initialise le service de traduction.

        Étapes :
        1) choisir le modèle,
        2) déterminer si on utilise le GPU ou le CPU,
        3) charger le tokenizer,
        4) charger le modèle,
        5) mesurer le temps de démarrage.

        device :
        - si explicitement fourni -> on l’utilise,
        - sinon -> "cuda" si disponible, sinon "cpu".
        """
        self.model_name = model_name
        self.device = device or ("cuda" if torch.cuda.is_available() else "cpu")

        # Combien coûte le boot de l’application.
        t0 = time.time()

        # Le tokenizer transforme le texte en tokens exploitables par le modèle.
        self.tokenizer = AutoTokenizer.from_pretrained(self.model_name)

        # Le modèle seq2seq = le moteur qui effectue réellement la traduction.
        self.model = AutoModelForSeq2SeqLM.from_pretrained(self.model_name).to(self.device)

        # Temps total nécessaire pour que le service devienne opérationnel.
        self.startup_ms = int((time.time() - t0) * 1000)

    def _normalize_lang(self, lang: str) -> str:
        """
        Convertit un code langue API en code langue compatible NLLB.
        ex: "fr" -> "fra_Latn"
        """
        lang = (lang or "").strip()
        return LANG_MAP.get(lang, lang)

    def translate(
            self,
            text: str,
            source_language: str,
            target_language: str,
            max_new_tokens: int = 256,
            num_beams: int = 4,
            repetition_penalty: float = 1.15,
            no_repeat_ngram_size: int = 3,
    ) -> TranslationResult:
        """
        Paramètres importants :
        - max_new_tokens : limite de longueur de sortie générée
        - num_beams : beam search -> plus haut = souvent plus qualitatif mais plus lent
        - repetition_penalty / no_repeat_ngram_size :

        Pipeline technique et business :
        1) normaliser et valider l’entrée,
        2) convertir les langues,
        3) configurer le tokenizer,
        4) tokeniser le texte,
        5) demander au modèle de générer la traduction,
        6) décoder les tokens de sortie en texte lisible,
        7) retourner le résultat enrichi avec métadonnées d’exécution.
        """
        t0 = time.time()
        text = (text or "").strip()

        src = self._normalize_lang(source_language)
        tgt = self._normalize_lang(target_language)

        if not text:
            return TranslationResult(
                translated_txt="",
                source_lang=src,
                target_lang=tgt,
                device=self.device,
                duration_ms=int((time.time() - t0) * 1000),
            )

        # Pour NLLB, il faut préciser explicitement la langue source au tokenizer.
        self.tokenizer.src_lang = src

        # Le tokenizer transforme le texte brut en tenseurs PyTorch.
        # truncation=False : ici on n’accepte pas de couper silencieusement le texte ;
        inputs = self.tokenizer(text, return_tensors="pt", truncation=False).to(self.device)

        # NLLB a besoin qu’on lui impose le token de début correspondant à la langue cible, sinon la génération peut partir dans une mauvaise langue.
        forced_bos_token_id = self.tokenizer.convert_tokens_to_ids(tgt)

        # On désactive les gradients car on est en inférence, pas en entraînement.
        # Business :
        # - moins de mémoire utilisée,
        # - plus rapide,
        # - comportement normal pour un service de prédiction
        with torch.no_grad():
            out_tokens = self.model.generate(
                **inputs,
                forced_bos_token_id=forced_bos_token_id,
                max_new_tokens=max_new_tokens,
                num_beams=num_beams,
                repetition_penalty=repetition_penalty,
                no_repeat_ngram_size=no_repeat_ngram_size,
            )

        # Convertit les tokens de sortie en texte humain lisible
        translated = self.tokenizer.batch_decode(out_tokens, skip_special_tokens=True)[0]

        # Retourne un objet métier propre
        return TranslationResult(
            translated_txt=translated,
            source_lang=src,
            target_lang=tgt,
            device=self.device,
            duration_ms=int((time.time() - t0) * 1000),
        )

    def translate_long(
            self,
            text: str,
            source_language: str,
            target_language: str,
            max_new_tokens: int = 256,
            max_chars: int = 900,
            num_beams: int = 4,
            preserve_paragraphs: bool = True,
            debug: bool = True,
            preprocess: bool = True,
    ) -> Dict[str, Any]:
        """
        Traduit un texte long en plusieurs étapes.

        C’est la méthode "pipeline long texte".

        Logique business :
        Un texte long ne doit pas être envoyé d’un bloc au modèle,
        car cela peut :
        - dépasser la capacité mémoire,
        - être lent,
        - produire une moins bonne traduction,
        - devenir difficile à diagnostiquer.

        On applique donc une chaîne de traitement :
        1) récupérer le texte brut,
        2) optionnellement le normaliser,
        3) le découper en chunks cohérents,
        4) traduire chaque chunk individuellement,
        5) reconstruire le texte final dans le bon ordre,
        6) retourner des métadonnées de debug.

        Paramètres métier :
        - max_chars : taille maximale d’un chunk
        - preserve_paragraphs : influence la manière de recoller les morceaux
        - debug : inclut des infos de diagnostic par chunk
        - preprocess : active la normalisation textuelle avant traduction
        """
        t0 = time.time()

        original_text = (text or "").strip()
        text = original_text

        # Cette liste enregistrera les règles de normalisation appliquées.
        applied_rules: List[str] = []

        if preprocess:
            text, applied_rules = normalize_text(text)

        # Découpage du texte en chunks.
        chunks: List[Chunk] = chunk_text(text, max_chars=max_chars)
        translated_parts: List[str] = []

        # debug_chunks contiendra les métadonnées de chaque chunk si le mode debug est activé.
        debug_chunks: List[Dict[str, Any]] = []

        # Traduction chunk par chunk.
        for ch in chunks:
            t_ch = time.time()
            res = self.translate(
                ch.text,
                source_language=source_language,
                target_language=target_language,
                max_new_tokens=max_new_tokens,
                num_beams=num_beams,
            )

            translated_parts.append(res.translated_txt)

            # Debug
            # - combien de chunks ont été produits,
            # - combien de temps chacun prend,
            # - quelle taille ils ont,
            # - quelle portion du texte source ils représentent.
            if debug:
                debug_chunks.append(
                    {
                        "index": ch.index,
                        "durationMs": int((time.time() - t_ch) * 1000),
                        "sourceChars": len(ch.text),
                        "sourcePreview": ch.text[:120] + ("..." if len(ch.text) > 120 else ""),
                    }
                )

        # Reconstruction du texte final.
        joiner = "\n\n" if preserve_paragraphs else " "
        final_text = joiner.join(translated_parts)

        # Étape 5 : construction de la réponse métier.
        out: Dict[str, Any] = {
            "translatedText": final_text,
            "chunkCount": len(chunks),
            "durationMs": int((time.time() - t0) * 1000),
            "device": self.device,
            "startupMs": self.startup_ms,
        }

        # Ajout des champs debugs
        if debug:
            out["chunks"] = debug_chunks
            out["preprocessing"] = applied_rules

        return out

import re
from dataclasses import dataclass
from typing import List

# -------------------------------------------------------------------
# REGEX DE DÉCOUPAGE
# -------------------------------------------------------------------
# Objectif business :
# Quand on traduit un texte long avec un modèle NMT, on ne veut pas envoyer
# tout le texte en une seule fois, car :
# - cela peut être trop lourd pour le modèle,
# - cela peut coûter plus de mémoire,
# - cela peut diminuer la qualité de traduction,
# - cela peut créer des lenteurs ou des limites de taille.
#
# On va donc découper le texte intelligemment :
# 1) d'abord par paragraphes,
# 2) puis si un paragraphe est encore trop long, par phrases,
# 3) et en dernier recours par découpe "brute".
#
# Cette regex coupe à chaque espace qui suit une ponctuation de fin de phrase.
# Exemple :
# "Bonjour. Comment vas-tu ?" -> ["Bonjour.", "Comment vas-tu ?"]
_SENTENCE_SPLIT_RE = re.compile(r"(?<=[.!?…])\s+")

# Cette regex coupe les paragraphes sur les lignes vides.
# Exemple :
# "A\n\nB" -> ["A", "B"]
_PARAGRAPH_SPLIT_RE = re.compile(r"\n\s*\n")


@dataclass(frozen=True)
class Chunk:
    index: int
    text: str


def split_paragraphs(text: str) -> List[str]:
    parts = _PARAGRAPH_SPLIT_RE.split(text.strip())
    return [p.strip() for p in parts if p.strip()]


def split_sentences(text: str) -> List[str]:
    """
    Découpe un bloc de texte en phrases.
    Attention :
    - Cette logique est simple et pratique, mais elle n’est pas parfaite
      pour toute les langues + cas linguistique particuliers.
    """
    sents = _SENTENCE_SPLIT_RE.split(text.strip())
    return [s.strip() for s in sents if s.strip()]


def _hard_split_long_unit(unit: str, max_chars: int) -> List[str]:
    """
    Découpe brutalement une unité trop longue en plusieurs sous-parties.
    Ce n’est pas la solution idéale d’un point de vue qualité,
    mais c’est une sécurité pour garantir que le pipeline continue.
    """
    unit = unit.strip()

    # Si l’unité est déjà assez petite, on la retourne telle quelle.
    if len(unit) <= max_chars:
        return [unit]

    # Découpe brute par tranche de max_chars.
    return [
        unit[i:i + max_chars].strip()
        for i in range(0, len(unit), max_chars)
        if unit[i:i + max_chars].strip()
    ]


def pack_units_to_chunks(units: List[str], max_chars: int) -> List[str]:
    """
    Fonctionnement :
    ex : units = ["Bonjour.", "Comment vas-tu ?", "Je vais bien."]
    alors on crée un premier chunk avec les 2 premiers units si max_char est dépassé,
    puis un second chunk avec la 3e.
    """
    chunks: List[str] = []
    buffer: List[str] = []
    buffer_len = 0

    for u in units:
        u = u.strip()
        if not u:
            continue

        # Si une seule unité dépasse max_chars, on ne peut pas la mettre telle quelle.
        # On vide d’abord le buffer actuel, puis on coupe cette unité "en dur".
        if len(u) > max_chars:
            if buffer:
                chunks.append(" ".join(buffer))
                buffer = []
                buffer_len = 0

            chunks.extend(_hard_split_long_unit(u, max_chars))
            continue

        # Si le buffer n'est pas vide, on doit compter un espace de séparation.
        extra = len(u) + (1 if buffer else 0)

        # Si ajouter cette unité dépasse la taille max,
        # on finalise le chunk courant et on repart avec un nouveau buffer.
        if buffer and buffer_len + extra > max_chars:
            chunks.append(" ".join(buffer))
            buffer = [u]
            buffer_len = len(u)
        else:
            # Sinon, on ajoute simplement l’unité au chunk courant.
            buffer.append(u)
            buffer_len += extra

    # À la fin, s’il reste du contenu dans le buffer -> transformer en chunk final.
    if buffer:
        chunks.append(" ".join(buffer))

    return chunks


def chunk_text(text: str, max_chars: int = 900) -> List[Chunk]:
    """
    Pourquoi cette logique est importante ?
    - Un traducteur NMT fonctionne mieux si on garde des morceaux cohérents.
    - Trop gros : risque technique / mémoire / qualité.
    - Trop petit : perte de contexte.
    - Cette stratégie essaye donc de rester "intelligente" avant de devenir "brute".
    """
    text = (text or "").strip()

    # Rien à faire si le texte est vide.
    if not text:
        return []

    out: List[str] = []

    # Étape 1 :
    # on découpe d’abord par paragraphes, car c’est la structure la plus naturelle.
    for p in split_paragraphs(text):
        # Si le paragraphe tient dans la limite, on le garde tel quel.
        if len(p) <= max_chars:
            out.append(p)
        else:
            # Sinon, on découpe le paragraphe en phrases.
            sents = split_sentences(p)

            # Puis on regroupe ces phrases en chunks de taille acceptable.
            out.extend(pack_units_to_chunks(sents, max_chars=max_chars))

    # On transforme les morceaux finaux en objets Chunk indexés.
    # L’index servira à reconstruire la traduction dans le bon ordre.
    return [Chunk(index=i, text=c) for i, c in enumerate(out)]
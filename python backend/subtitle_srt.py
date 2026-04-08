from __future__ import annotations

import re
from dataclasses import dataclass
from typing import List

# -------------------------------------------------------------------
# REGEX DE LIGNE TEMPORELLE SRT
# -------------------------------------------------------------------
# Un bloc SRT contient généralement :
# 1
# 00:00:01,000 --> 00:00:03,000
# Bonjour le monde
#
# Cette regex vérifie que la ligne temporelle ressemble bien à ce format.
# IMPORTANT -> Prévoir les différents format de précision car je suis tombé sur des fichiers qui ne suivaient poas ce format

_TIME_RE = re.compile(
    r"^\s*\d{2}:\d{2}:\d{2},\d{3}\s*-->\s*\d{2}:\d{2}:\d{2},\d{3}\s*$"
)


@dataclass
class SrtBlock:
    index: int
    time_range: str
    lines: List[str]


def parse_srt(srt_text: str) -> List[SrtBlock]:
    """
    Transforme un texte SRT brut en liste de blocs structurés.

    Logique business :
    - Un fichier SRT doit être compris comme une succession de "blocs"
    - Chaque bloc correspond à un sous-titre affiché à un moment donné

    Structure d’un bloc :
    1) index 2) time range 3) une ou plusieurs lignes de texte

    Cette fonction est volontairement tolérante :
    - si l’index est manquant ou invalide, on en génère un,
    - si le format de la ligne temporelle n’est pas parfait,
      on essaie quand même de continuer.
    """
    srt_text = (srt_text or "").replace("\r\n", "\n").replace("\r", "\n").strip()
    if not srt_text:
        return []

    # Les blocs SRT sont séparés par une ligne vide.
    raw_blocks = re.split(r"\n\s*\n", srt_text)
    blocks: List[SrtBlock] = []

    for raw in raw_blocks:
        # On retire les lignes totalement vides à l’intérieur d’un bloc.
        lines = [l.rstrip() for l in raw.split("\n") if l.strip() != ""]
        if len(lines) < 2:
            # Un bloc incomplet ne peut pas être exploité correctement.
            continue

        # Première ligne : index du bloc
        try:
            idx = int(lines[0].strip())
        except ValueError:
            # Cas réel possible :
            # certains fichiers SRT sont "sales" ou mal formés.
            # On régénère alors un index simple pour continuer le traitement.
            idx = len(blocks) + 1

        # Deuxième ligne : plage temporelle
        time_line = lines[1].strip()

        # Si la ligne de temps est étrange, on ne bloque pas le parsing --> Mais en vrai oui, ou au moins afficher/throw une erreur
        # On sera plus strict dans le futur
        if not _TIME_RE.match(time_line):
            pass

        # Le reste du bloc correspond aux lignes de texte du sous-titre.
        text_lines = lines[2:] if len(lines) > 2 else []

        blocks.append(SrtBlock(index=idx, time_range=time_line, lines=text_lines))

    return blocks


def blocks_to_srt(blocks: List[SrtBlock]) -> str:
    """
    Reconstruction du fichier SRT à partir des block traduits
    """
    out_lines: List[str] = []

    for b in blocks:
        out_lines.append(str(b.index))
        out_lines.append(b.time_range)
        out_lines.extend(b.lines)
        out_lines.append("")  # séparation standard entre blocs

    return "\n".join(out_lines).strip() + "\n"


def join_block_text(block: SrtBlock) -> str:
    """
    Transforme les lignes de texte d’un bloc SRT en un seul texte continu.

    Logique business :
    - Dans un fichier SRT, un même sous-titre peut être affiché sur plusieurs lignes
      pour des raisons visuelles.
    - Mais pour la traduction, il est souvent préférable de donner au modèle
      un texte reconstruit en une seule phrase continue.
    - Cela limite les traductions cassées ligne par ligne.

    Exemple :
    ["Bonjour", "comment vas-tu ?"] -> "Bonjour comment vas-tu ?"
    """
    return " ".join([l.strip() for l in block.lines if l.strip()]).strip()


def split_text_back_to_lines(text: str, max_line_len: int = 42) -> List[str]:
    """
    Réinsère des retours à la ligne dans le texte traduit
    pour reformer des sous-titres lisibles.

    Logique business :
    - Un texte traduit peut devenir trop long sur une seule ligne.
    - En sous-titrage, on veut des lignes relativement courtes pour la lecture.
    - Cette fonction fait donc un "rewrapping" simple par nombre de caractères.
    """
    text = (text or "").strip()
    if not text:
        return []

    words = text.split()
    lines: List[str] = []
    buf: List[str] = []
    size = 0

    for w in words:
        # Si le buffer n'est pas vide, ajouter un mot implique aussi un espace.
        add = len(w) + (1 if buf else 0)

        # Si ajouter ce mot dépasse la longueur max de ligne,
        # on finalise la ligne courante et on commence une nouvelle ligne.
        if buf and size + add > max_line_len:
            lines.append(" ".join(buf))
            buf = [w]
            size = len(w)
        else:
            buf.append(w)
            size += add

    if buf:
        lines.append(" ".join(buf))

    return lines


def translate_srt(
        srt_text: str,
        translator,
        source_language: str,
        target_language: str,
        max_chars: int = 900,
        max_new_tokens: int = 512,
        num_beams: int = 5,
) -> str:
    """
    Traduit un fichier SRT entier tout en conservant sa structure.

    Pipeline business :
    1) parser le SRT en blocs structurés,
    2) pour chaque bloc :
       - reconstruire le texte source en une seule chaîne,
       - traduire ce texte,
       - redécouper le texte traduit en lignes lisibles,
    3) reconstruire le fichier SRT final.

    Pourquoi bloc par bloc ?
    - Un bloc SRT représente une unité temporelle d’affichage.
    - On doit préserver cette unité.
    - On ne peut pas mélanger les textes de plusieurs timestamps.

    Important :
    - Ici, on traduit chaque bloc individuellement via translator.translate().
    - On part du principe qu’un bloc SRT est généralement court.
    - Donc pas besoin de la logique complète de chunking long texte.

    """
    blocks = parse_srt(srt_text)
    if not blocks:
        return ""

    for b in blocks:
        # On reconstruit le texte utile du bloc.
        src = join_block_text(b)

        # Si un bloc n’a pas de texte, on garde juste des lignes vides.
        if not src:
            b.lines = []
            continue

        # Traduction du contenu textuel du bloc.
        # On ne touche ni à l'index, ni au timing.
        res = translator.translate(
            src,
            source_language=source_language,
            target_language=target_language,
            max_new_tokens=max_new_tokens,
            num_beams=num_beams,
        )

        # Une fois traduit, on reformate le texte en lignes plus lisibles
        # pour rester proche d’un affichage sous-titre classique.
        b.lines = split_text_back_to_lines(res.translated_txt)

    # On recompose un fichier SRT complet avec le texte traduit.
    return blocks_to_srt(blocks)

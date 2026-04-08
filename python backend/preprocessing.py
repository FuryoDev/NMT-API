import re
from typing import Tuple, List

# -------------------------------------------------------------------
# RÈGLES DE NORMALISATION
# -------------------------------------------------------------------
# Logique business :
# Avant d’envoyer le texte au moteur de traduction, on peut vouloir
# "normaliser" certaines formulations.
#
# But possible :
# - réduire les expressions trop familières,
# - remplacer certains tournures par des formes plus neutres,
# - obtenir une traduction parfois plus stable.
#
# Attention :
# ce genre de normalisation n’est pas neutre.
# Elle peut modifier le ton, le style, voire légèrement le sens.
#
# Donc businessment parlant :
# - utile si le besoin est de "lisser" le texte avant traduction,
# - dangereux si on veut une fidélité absolue au texte source.
REPLACEMENTS = [
    # Expressions familières / vulgaires -> version plus neutre
    (r"\bon s'en bat les couilles\b", "on s'en fiche"),
    (r"\bon s'en fout\b", "on s'en fiche"),
    (r"\bvas[- ]?y\b", "peu importe"),
    (r"\bc'est pas grave\b", "ce n'est pas grave"),
]


def normalize_text(text: str) -> Tuple[str, List[str]]:
    """
    Applique des règles simples de normalisation sur le texte source.

    Retourne :
    - le texte transformé,
    - la liste des règles appliquées (utile pour le debug et la traçabilité).

    Logique business :
    - Le texte d’entrée peut contenir des formulations familières,
      relâchées, ou peu adaptées à une traduction de bonne qualité.
    - On applique donc des substitutions avant traduction.
    - On garde en mémoire les règles appliquées pour pouvoir expliquer
      ce qui a été modifié.

    Exemple :
    "on s'en fout" -> "on s'en fiche"

    Intérêt pédagogique :
    - cette fonction montre une logique de pipeline :
      entrée brute -> transformation -> sortie + informations de suivi.
    """
    applied: List[str] = []
    out = text

    # On parcourt toutes les règles de normalisation.
    for pattern, replacement in REPLACEMENTS:
        # Si la règle trouve au moins une occurrence dans le texte,
        # on applique le remplacement.
        if re.search(pattern, out, flags=re.IGNORECASE):
            out = re.sub(pattern, replacement, out, flags=re.IGNORECASE)

            # On garde une trace de la règle utilisée.
            # Très utile pour le debug ou pour expliquer à l’utilisateur
            # pourquoi le texte source a été modifié.
            applied.append(f"{pattern} -> {replacement}")

    return out, applied
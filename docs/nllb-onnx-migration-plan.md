# Migration NLLB Python -> ONNX Runtime natif .NET

## Cible d'architecture (backend .NET)

Le service de traduction est structuré pour isoler les briques ONNX suivantes :

1. **`NllbTranslationService`** (orchestrateur métier)
   - garde la signature actuelle (`Translate`, `NormalizeLanguage`) ;
   - conserve les infos de monitoring (`ModelName`, `Device`, `StartupMs`) ;
   - orchestre la chaîne tokenizer -> génération -> décodage.

2. **`ITranslationTokenizer`**
   - contrat pour encoder/décoder les tokens ;
   - gestion de `forced_bos_token_id` côté langue cible ;
   - implémentation actuelle : `StubNllbTokenizer` (compatible endpoint, non fidèle NLLB).

3. **`IOnnxNllbRunner`**
   - contrat de génération seq2seq ;
   - encapsule le chargement ONNX Runtime ;
   - implémentation actuelle : `OnnxNllbRunner` (charge le modèle ONNX si dispo, boucle de génération simplifiée).

4. **`NllbOnnxOptions` (configuration)**
   - variables externalisées dans `appsettings*.json` ;
   - modèle, device, chemin ONNX, paramètres de génération.

## Variables externalisées dans la configuration .NET

Section : `Translation:NllbOnnx`

- `ModelName`
- `Device`
- `ModelPath`
- `EnableOnnxRuntime`
- `MaxInputTokens`
- `DefaultMaxNewTokens`
- `DefaultNumBeams`
- `NoRepeatNgramSize`
- `RepetitionPenalty`

## Ce qui est implémentable immédiatement en C#

- Wiring DI et architecture de composants (fait).
- Chargement conditionnel ONNX Runtime (fait).
- Maintien de compatibilité des endpoints `translate`, `file`, `srt` (fait via interface inchangée).
- Monitoring de base service (fait).

## Ce qui nécessite Python (offline uniquement)

Pour une traduction NLLB réelle et fidèle, il faut exporter des artefacts ONNX depuis Hugging Face :

1. Tokenizer NLLB (vocab + merges + config) compatible C#.
2. Modèle ONNX exporté en **encoder/decoder** (ou modèle équivalent supportant la génération autoregressive).
3. Métadonnées de génération utiles (`decoder_start_token_id`, `eos_token_id`, ids langues, etc.).

> Python n'est requis que pour la préparation/export des artefacts, jamais pour le runtime API.

## Points techniques complexes / bloquants

1. **Génération seq2seq autoregressive**
   - boucle token par token ;
   - gestion des états de cache (`past_key_values`) ;
   - arrêt sur EOS ;
   - performance mémoire/latence.

2. **Beam search côté C#**
   - scoring log-prob ;
   - pénalités de longueur/répétition ;
   - early stopping cohérent avec le comportement Python.

3. **Parité tokenizer NLLB**
   - conversion exacte des IDs,
   - mapping des tokens langue,
   - normalisation identique au pipeline Python.

## Stratégie de migration progressive

### Étape 1 — service minimal fonctionnel (immédiat)

- Conserver `NllbTranslationService` comme façade stable.
- Injecter tokenizer + runner ONNX via DI.
- Autoriser démarrage même sans modèle ONNX (`EnableOnnxRuntime=false`) pour sécuriser les déploiements.

### Étape 2 — intégration ONNX réelle

- Export offline du modèle NLLB en ONNX (Python tooling).
- Déposer les artefacts dans `models/nllb/`.
- Implémenter un tokenizer C# réellement compatible (SentencePiece/BPE selon artefact).
- Brancher la vraie exécution ONNX dans `OnnxNllbRunner.Generate`.

### Étape 3 — parité comportement Python

- Ajouter forcing BOS langue cible exact.
- Implémenter beam search complet.
- Implémenter pénalités (`repetition_penalty`, `no_repeat_ngram_size`).
- Ajouter tests de non-régression sur un corpus de phrases de référence Python.

## Checklist d'implémentation recommandée

- [ ] Ajouter un package tokenizer C# compatible NLLB (ou wrapper maison sur artefacts exportés).
- [ ] Ajouter boucle seq2seq complète avec gestion cache decoder ONNX.
- [ ] Ajouter tests unitaires sur mapping langues et forced BOS.
- [ ] Ajouter tests d'intégration API pour `translate`, `translate/file`, `translate/srt`.
- [ ] Ajouter benchmark simple (latence p50/p95, mémoire) CPU.


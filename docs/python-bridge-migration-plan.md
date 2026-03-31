# Migration pragmatique vers un bridge Python (façade .NET conservée)

## Architecture cible

La façade officielle reste l'API ASP.NET Core (.NET 10). La traduction réelle est déléguée à un backend Python (source de vérité).

### Composants .NET

1. **`NllbTranslationService`**
   - service applicatif appelé par les controllers existants ;
   - normalise les langues ;
   - délègue la traduction au client HTTP Python ;
   - expose `ModelName`, `Device`, `StartupMs` pour le monitoring.

2. **`IPythonTranslationBackendClient` / `PythonTranslationBackendClient`**
   - client HTTP typé vers le service Python ;
   - méthodes `GetHealthAsync` et `TranslateAsync`.

3. **DTOs bridge (`PythonTranslateRequest`, `PythonTranslateResponse`, `PythonHealthResponse`)**
   - contrat d'échange explicite .NET <-> Python.

4. **`PythonTranslationBackendOptions`**
   - configuration via `appsettings` (`BaseUrl`, timeouts, fallback monitoring).

## Répartition des responsabilités

### Côté .NET (reste dans l'API publique)

- endpoints REST publics (`/translate`, `/translate/file`, `/translate/file/json`, `/translate/srt`) ;
- validation d'entrée et pré-traitement déjà en place ;
- orchestration globale et format de réponse public ;
- auth, rate-limit, logs, monitoring API.

### Côté Python (source de vérité traduction)

- chargement du modèle Hugging Face NLLB ;
- vraie inférence de traduction ;
- comportement de génération (beam search, forced BOS, etc.) ;
- optimisations runtime NMT pour RHEL (CPU/GPU selon déploiement).

## Stratégie d'intégration progressive

### Étape 1 — Bridge texte (implémentée)

- `NllbTranslationService` appelle `/translate` du backend Python ;
- propriétés de monitoring récupérées via `/health` au démarrage ;
- controllers inchangés.

### Étape 2 — Durcissement prod

- retries/backoff côté `HttpClient` ;
- gestion explicite des erreurs backend (timeouts, 5xx, indisponibilité) ;
- observabilité (latence .NET vs latence Python).

### Étape 3 — Parité complète et SLA

- corpus de tests de non-régression Python/.NET ;
- tuning des timeouts et limites de payload ;
- industrialisation RHEL (systemd, reverse proxy, supervision).

## Pourquoi cette approche minimise les changements

- Les controllers ne changent pas : ils consomment toujours `INmtTranslationService`.
- Le service SRT continue à traduire bloc par bloc via la même interface.
- Le backend Python peut évoluer indépendamment sans casser le contrat public .NET.

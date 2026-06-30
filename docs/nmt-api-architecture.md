# NMT API - Description technique et choix d'architecture

## 1. Objet du projet

Cette API expose un service de traduction automatique destiné à traduire :

- du texte brut ;
- des fichiers texte ;
- des fichiers de sous-titres SRT, en conservant la structure temporelle du fichier.

Le projet est parti d'un POC qui a permis de valider les premières briques d'inférence NMT. Le nettoyage et la restructuration actuels visent à en faire une base exploitable en production côté .NET, avec une architecture plus claire, des responsabilités séparées et un format de modèle standardisé.

## 2. Positionnement de l'API

L'API est une façade ASP.NET Core. Elle reste responsable de :

- exposer les endpoints REST publics ;
- valider les paramètres d'entrée ;
- charger le modèle de traduction ;
- orchestrer la traduction texte et SRT ;
- gérer les jobs asynchrones ;
- exposer la progression ;
- stocker une notation simple de la traduction ;
- exposer un endpoint de monitoring du modèle ONNX chargé.

Le moteur de traduction est désormais porté par ONNX Runtime dans le projet .NET. Il n'y a plus de backend Python dans le chemin principal.

## 3. Pourquoi ONNX

ONNX a été retenu pour rendre le modèle exploitable dans un contexte .NET de manière standardisée.

Les raisons principales :

- **Standardisation du modèle** : ONNX est un format interopérable, indépendant du framework d'entraînement initial. Un modèle issu de PyTorch, TensorFlow ou Hugging Face peut être exporté puis chargé de façon homogène.
- **Intégration .NET native** : `Microsoft.ML.OnnxRuntime` permet d'exécuter le modèle directement depuis l'API ASP.NET Core, sans lancer un service Python séparé.
- **Déploiement simplifié** : un seul processus applicatif .NET porte l'API et l'inférence. Cela simplifie l'exploitation, le monitoring, le packaging et la supervision.
- **Moins de couplage runtime** : l'API ne dépend plus d'un environnement Python, de versions CUDA/PyTorch côté Python, ou d'un microservice annexe à maintenir.
- **Chargement maîtrisé** : le modèle est chargé dans une `InferenceSession` singleton au démarrage de l'application. Il n'est pas rechargé à chaque requête.
- **Observabilité** : l'API expose les métadonnées ONNX utiles : chemin du modèle, inputs, outputs, temps de chargement, statut de chargement.

Ce choix est cohérent avec un objectif de production dans une organisation où la stabilité d'exploitation et la maintenabilité priment sur la souplesse expérimentale du POC.

## 4. Langues supportées

L'API accepte des codes langue courts côté consommateur, puis les convertit vers les codes NLLB attendus par le modèle.

Liste exposée :

`ar`, `cs`, `da`, `de`, `el`, `en`, `es`, `fa`, `fr`, `he`, `hr`, `hu`, `it`, `ja`, `ko`, `nl`, `no`, `pl`, `pt`, `ro`, `ru`, `sk`, `sl`, `tr`, `uk`, `zh`.

La correspondance est configurée dans `appsettings.json`, section `Translation:SupportedLanguages`.

Exemple :

- `fr` devient `fra_Latn` ;
- `en` devient `eng_Latn` ;
- `zh` devient `zho_Hans` ;
- `ar` devient `arb_Arab`.

Cette séparation évite d'exposer les codes internes NLLB aux clients de l'API.

## 5. Endpoints principaux

### `GET /api/translations/languages`

Retourne la liste des langues supportées et leur équivalent NLLB.

Usage : alimenter une interface ou valider côté client les choix source/target.

### `GET /api/translations/onnx/model`

Retourne les informations du modèle ONNX chargé :

- provider ;
- chemin du modèle ;
- statut de chargement ;
- date de chargement ;
- temps de chargement ;
- noms des inputs ;
- noms des outputs.

Usage : endpoint de diagnostic et de supervision applicative.

### `POST /api/translations/text`

Traduit immédiatement un texte court.

Corps JSON :

```json
{
  "text": "Bonjour tout le monde",
  "sourceLanguage": "fr",
  "targetLanguage": "en",
  "maxNewTokens": 512
}
```

Réponse : texte traduit, langues normalisées, codes NLLB, durée d'inférence et provider utilisé.

### `POST /api/translations/jobs/text`

Crée un job asynchrone pour traduire un texte.

Usage : privilégier ce endpoint si l'appelant veut suivre la progression de façon uniforme, même pour du texte.

### `POST /api/translations/jobs/file`

Crée un job asynchrone à partir d'un fichier texte envoyé en `multipart/form-data`.

Le fichier est lu en UTF-8, puis traduit comme texte complet.

### `POST /api/translations/jobs/srt`

Crée un job asynchrone à partir d'un fichier SRT.

Le fichier est parsé en blocs de sous-titres. Chaque bloc est traduit individuellement afin de conserver :

- l'index du sous-titre ;
- le timecode ;
- l'ordre des blocs ;
- une longueur de ligne compatible avec un affichage sous-titre.

### `GET /api/translations/jobs`

Retourne les jobs récents.

Paramètre optionnel : `take`.

### `GET /api/translations/jobs/{jobId}`

Retourne l'état d'un job :

- statut ;
- pourcentage ;
- étape en cours ;
- unités traitées ;
- total d'unités ;
- erreur éventuelle ;
- date de création, démarrage, fin ;
- lien vers le résultat si disponible ;
- notation si elle existe.

Pour un SRT, les unités correspondent aux blocs de sous-titres.

### `GET /api/translations/jobs/{jobId}/result`

Retourne le résultat d'un job terminé.

- `text/plain` pour texte/fichier ;
- `application/x-subrip` pour SRT.

### `POST /api/translations/jobs/{jobId}/rating`

Permet de noter une traduction terminée.

Corps JSON :

```json
{
  "score": 4,
  "comment": "Bonne traduction, quelques corrections éditoriales nécessaires.",
  "ratedBy": "prenom.nom"
}
```

Le score doit être compris entre 1 et 5.

## 6. Organisation des classes

### `Controllers`

#### `TranslationsController`

Contrôleur public principal.

Responsabilités :

- exposer les endpoints REST ;
- mapper les contrats API vers les services applicatifs ;
- convertir les exceptions métier en réponses HTTP ;
- retourner les résultats, statuts de job et informations ONNX.

Les anciens endpoints de test ONNX ont été supprimés pour éviter de publier des routes de POC avec des textes hardcodés.

### `Contracts/Requests`

#### `TranslateTextRequest`

Contrat JSON pour une traduction texte.

Champs :

- `text` ;
- `sourceLanguage` ;
- `targetLanguage` ;
- `maxNewTokens`.

#### `TranslateFileRequest`

Contrat `multipart/form-data` pour un fichier texte.

Champs :

- `file` ;
- `sourceLanguage` ;
- `targetLanguage` ;
- `maxNewTokens`.

#### `TranslateSrtRequest`

Contrat `multipart/form-data` pour un fichier SRT.

Champs identiques à `TranslateFileRequest`.

#### `RateTranslationRequest`

Contrat de notation d'une traduction.

### `Contracts/Responses`

#### `TranslationResponse`

Réponse pour une traduction immédiate.

Inclut :

- texte traduit ;
- langues source/target côté API ;
- langues source/target côté NLLB ;
- provider ;
- durée ;
- nombre de chunks si applicable.

#### `TranslationJobAcceptedResponse`

Réponse lors de la création d'un job.

Inclut :

- `jobId` ;
- statut initial ;
- URL de statut ;
- URL de résultat.

#### `TranslationJobStatusResponse`

Représentation lisible d'un job.

C'est le contrat principal pour suivre la progression.

#### `TranslationRatingResponse`

Retourne une notation enregistrée.

#### `OnnxModelInfoResponse`

Expose les métadonnées du modèle ONNX chargé.

#### `SupportedLanguageResponse`

Expose le code API et son code NLLB.

### `Services/Translation/Core`

#### `INmtTranslationService`

Interface applicative de traduction.

Elle cache le détail ONNX au contrôleur.

#### `NmtTranslationService`

Service métier principal.

Responsabilités :

- valider le texte ;
- valider les paramètres ;
- résoudre les langues ;
- encoder le texte avec le tokenizer NLLB ;
- imposer le token de langue cible ;
- appeler le runner ONNX ;
- décoder les tokens générés ;
- construire un `TranslationResult`.

#### `TranslationRequestOptions`

Objet interne pour transporter les paramètres de traduction normalisés.

#### `TranslationResult`

Résultat métier interne.

#### `TranslationDefaultsOptions`

Options applicatives venant de `appsettings.json`.

Inclut :

- langue source par défaut ;
- langue cible par défaut ;
- limite `maxNewTokens` par défaut ;
- limite de taille texte ;
- longueur de ligne SRT.

### `Services/Translation/Onnx`

#### `IOnnxNllbRunner`

Interface d'exécution ONNX.

Expose :

- `Generate(...)` ;
- `ModelInfo`.

#### `OnnxNllbRunner`

Wrapper autour de `Microsoft.ML.OnnxRuntime.InferenceSession`.

Point important : le runner est enregistré en singleton. La session ONNX est donc créée une seule fois par process applicatif.

Responsabilités :

- résoudre le chemin du modèle ;
- vérifier que le fichier existe ;
- charger `InferenceSession` ;
- exposer les inputs/outputs du modèle ;
- exécuter une génération greedy ;
- renvoyer les ids de tokens générés.

#### `OnnxModelWarmupHostedService`

Service lancé au démarrage.

Il force la résolution du singleton ONNX et du tokenizer afin que le modèle soit chargé dès le boot de l'application, et non lors de la première requête utilisateur.

Cela donne un comportement plus prévisible en production : si le modèle est absent ou incompatible, l'application échoue au démarrage plutôt que plus tard.

#### `NllbOnnxOptions`

Options ONNX :

- `ModelPath` ;
- `TokenizerPath` ;
- `MaxNewTokens` ;
- `EosTokenId` ;
- `TargetLanguageTokenId` fallback.

#### `GreedyGenerationRequest` et `GreedyGenerationResult`

Contrats internes du runner ONNX.

Le terme `Greedy` est conservé car il décrit l'algorithme actuel : à chaque étape, le token de probabilité maximale est choisi.

Le beam search n'est pas exposé pour l'instant, car il n'est pas implémenté dans le runner ONNX actuel.

### `Services/Translation/Tokenization`

#### `INllbTokenizer`

Interface de tokenization.

#### `NllbTokenizer`

Wrapper autour de `Tokenizers.HuggingFace`.

Responsabilités :

- charger `tokenizer.json` ;
- encoder le texte ;
- ajouter le token de langue source NLLB si disponible ;
- décoder les ids générés ;
- résoudre un token NLLB vers son id.

Une correction importante a été faite ici : la librairie `Tokenizers.HuggingFace` utilisée ne fournit pas `TokenToId`. Le service lit donc `tokenizer.json` et construit une table interne `token -> id` à partir de `model.vocab` et `added_tokens`.

### `Services/Translation/Language`

#### `ITranslationLanguageService`

Interface de validation et résolution des langues.

#### `TranslationLanguageService`

Convertit les codes courts exposés par l'API vers les codes NLLB.

#### `TranslationLanguageOptions`

Options configurables des langues supportées.

#### `UnsupportedLanguageException`

Exception métier utilisée pour retourner un `400 Bad Request` clair si une langue n'est pas supportée.

### `Services/Translation/Srt`

#### `ISrtService`

Interface de manipulation SRT.

#### `SrtService`

Responsabilités :

- parser un fichier SRT ;
- reconstruire un fichier SRT ;
- joindre les lignes d'un bloc pour traduction ;
- re-découper le texte traduit en lignes lisibles.

Le service est volontairement tolérant sur les fichiers SRT imparfaits, comme dans le POC initial.

#### `SrtBlock`

Représente un bloc SRT :

- index ;
- time range ;
- lignes de texte.

#### `SrtDocument`

Représente un ensemble de blocs SRT.

### `Services/Translation/Jobs`

#### `TranslationJob`

Objet d'état complet d'un job.

Il contient :

- identifiant ;
- type ;
- statut ;
- progression ;
- texte source ;
- résultat ;
- erreur ;
- métadonnées d'exécution ;
- notation.

#### `TranslationJobKind`

Enum :

- `Text` ;
- `File` ;
- `Srt`.

#### `TranslationJobStatus`

Enum :

- `Queued` ;
- `Running` ;
- `Succeeded` ;
- `Failed` ;
- `Canceled`.

#### `TranslationJobQueue`

Queue basée sur `System.Threading.Channels`.

Elle permet de découpler la réception HTTP du traitement de traduction.

#### `InMemoryTranslationJobStore`

Stockage en mémoire des jobs.

Il est suffisant pour une première version applicative, mais il faudra le remplacer par un stockage durable si l'on veut survivre aux redémarrages, historiser les notations ou traiter un volume important.

#### `TranslationJobService`

Service applicatif de création, lecture et notation des jobs.

#### `TranslationJobWorker`

`BackgroundService` qui consomme la queue.

Pour les jobs SRT, il traduit bloc par bloc et met à jour la progression après chaque bloc. C'est ce qui rend possible un suivi fin de la traduction.

#### `TranslationRating`

Notation simple :

- score 1 à 5 ;
- commentaire ;
- auteur ;
- date.

## 7. Flux de traduction texte

1. Le client appelle `POST /api/translations/text`.
2. Le contrôleur construit un `TranslationRequestOptions`.
3. `NmtTranslationService` valide le texte et les langues.
4. `TranslationLanguageService` convertit `fr` en `fra_Latn`, par exemple.
5. `NllbTokenizer` encode le texte.
6. `NllbTokenizer` ajoute le token de langue source si nécessaire.
7. `NllbTokenizer` résout le token de langue cible en id.
8. `OnnxNllbRunner` exécute le modèle.
9. Le résultat est décodé en texte.
10. L'API retourne une réponse JSON.

## 8. Flux de traduction SRT

1. Le client envoie un fichier via `POST /api/translations/jobs/srt`.
2. L'API crée un job avec statut `Queued`.
3. `TranslationJobWorker` récupère le job.
4. `SrtService` parse le fichier en blocs.
5. Chaque bloc est traduit séparément.
6. Après chaque bloc, le job est mis à jour :
   - `ProcessedUnits` ;
   - `TotalUnits` ;
   - `Percent` ;
   - `CurrentStep`.
7. `SrtService` reconstruit le fichier SRT.
8. Le job passe à `Succeeded`.
9. Le client récupère le résultat via `GET /api/translations/jobs/{jobId}/result`.

Ce choix garantit que les timecodes restent inchangés. On ne mélange pas plusieurs timestamps dans une même traduction.

## 9. Chargement du modèle

Le modèle est chargé par `OnnxNllbRunner`.

Dans `Program.cs` :

- `IOnnxNllbRunner` est enregistré en singleton ;
- `INllbTokenizer` est enregistré en singleton ;
- `OnnxModelWarmupHostedService` force leur résolution au démarrage.

Conséquence :

- le modèle n'est pas rechargé à chaque requête ;
- la première requête utilisateur ne subit pas le coût complet de chargement ;
- une erreur de modèle absent ou invalide est détectée au boot ;
- l'état du modèle est visible via `/api/translations/onnx/model`.

## 10. Configuration principale

Fichier : `NMT-api/appsettings.json`.

Sections importantes :

### `Translation:Defaults`

Paramètres fonctionnels :

- `SourceLanguage` ;
- `TargetLanguage` ;
- `MaxNewTokens` ;
- `MaxTextInputChars` ;
- `SrtMaxLineLength`.

### `Translation:JobQueue`

Paramètre technique :

- `Capacity` : capacité de la queue de jobs.

### `Translation:SupportedLanguages`

Mapping API -> NLLB.

### `NllbOnnx`

Paramètres modèle :

- `ModelPath` ;
- `TokenizerPath` ;
- `MaxNewTokens` ;
- `EosTokenId` ;
- `TargetLanguageTokenId`.

`TargetLanguageTokenId` reste comme fallback technique, mais la langue cible est normalement résolue dynamiquement à partir du tokenizer.

## 11. Librairies et dépendances importantes

### ASP.NET Core

Socle de l'API REST.

Utilisé pour :

- contrôleurs ;
- injection de dépendances ;
- hosted services ;
- configuration ;
- middleware ;
- OpenAPI.

### Microsoft.ML.OnnxRuntime

Exécution du modèle ONNX.

Classe centrale : `InferenceSession`.

### Tokenizers.HuggingFace

Chargement et utilisation du tokenizer Hugging Face exporté en `tokenizer.json`.

La librairie couvre l'encodage/décodage, mais pas la résolution directe `token -> id` dans la version utilisée. Cette partie est donc prise en charge localement en lisant le JSON du tokenizer.

### Scalar et Swagger

Documentation interactive de l'API.

Scalar est conservé comme UI moderne, Swagger comme UI plus classique et très connue des équipes.

### RTBF Flow / Pacemaker / Logger / Authentication

Librairies internes déjà présentes dans le socle du POC.

Elles couvrent :

- logging applicatif ;
- intégration Pacemaker ;
- authentication/rate limiting selon environnement ;
- logging HTTP.

### System.Threading.Channels

Utilisé pour la queue de jobs asynchrones.

Cette approche est simple et efficace dans une première version, sans imposer immédiatement un broker externe.

## 12. Choix de conception importants

### Un seul contrôleur public de traduction

`TranslationsController` centralise les opérations de traduction et évite la multiplication de routes de test.

### Séparation API / métier / ONNX

Le contrôleur ne connaît pas les détails de `InferenceSession`.

Cette séparation permet de remplacer ou améliorer le runner ONNX sans modifier les contrats HTTP.

### Jobs en mémoire

Le stockage en mémoire est un compromis de première version :

- rapide à intégrer ;
- suffisant pour une démo ou un premier environnement interne ;
- simple à comprendre.

Limite connue : les jobs disparaissent au redémarrage.

Pour une production complète, il faudra envisager SQL Server, Redis, ou une table dédiée selon les standards internes.

### Progression réelle pour SRT

La progression est fiable pour les fichiers SRT car elle correspond aux blocs réellement traduits.

Pour un texte simple, la progression est plus grossière : queued, running, completed.

### Notation simple

La notation est volontairement minimale.

Elle permet déjà de recueillir un signal métier :

- qualité perçue ;
- commentaire ;
- auteur.

Elle devra être persistée si l'on veut l'utiliser pour un reporting qualité ou une amélioration continue.

## 13. Limites actuelles

### Génération greedy uniquement

Le runner ONNX actuel effectue une génération greedy.

Cela signifie :

- pas de beam search ;
- pas de sampling ;
- pas de pénalité de répétition avancée ;
- pas de stratégie de génération configurable côté API.

C'est volontairement cohérent avec l'état réel du code : l'API n'expose pas `numBeams` pour ne pas promettre une fonctionnalité non implémentée.

### Stockage non durable

Les jobs et notations sont en mémoire.

À traiter avant une production à plus fort enjeu.

### Qualité modèle dépendante de l'export ONNX

La qualité dépend du modèle ONNX exporté, de son tokenizer, et de la façon dont les tokens de langue NLLB sont gérés.

Il faudra valider l'export avec un corpus de non-régression représentatif :

- textes courts ;
- textes longs ;
- SRT éditoriaux ;
- langues prioritaires ;
- accents et caractères non latins.

### Modèle dans le dépôt

Le projet contient actuellement des artefacts modèle dans `Models/Nllb`.

Pour une production et un repository propre, il faudra décider si ces artefacts restent versionnés ou s'ils sont fournis par packaging, stockage artefact, volume serveur, ou pipeline de déploiement.

Ce point relève davantage du setup repository/deployment que du nettoyage applicatif.

## 14. Prochaines étapes recommandées

1. Ajouter des tests automatisés sur :
   - validation des langues ;
   - parsing/reconstruction SRT ;
   - progression de jobs ;
   - notation ;
   - résolution token NLLB.

2. Remplacer `InMemoryTranslationJobStore` par un stockage durable si l'API doit gérer de vrais workflows utilisateurs.

3. Ajouter une stratégie de génération ONNX plus avancée si la qualité greedy n'est pas suffisante.

4. Définir la stratégie de distribution des modèles :
   - artefact externe ;
   - chemin configurable par environnement ;
   - validation au démarrage.

5. Ajouter un endpoint de readiness distinct si nécessaire :
   - API démarrée ;
   - modèle ONNX chargé ;
   - tokenizer chargé ;
   - ressources disponibles.

6. Mesurer les performances :
   - temps de chargement ;
   - latence texte ;
   - latence SRT par nombre de blocs ;
   - mémoire consommée ;
   - comportement CPU/GPU selon environnement.

## 15. Résumé exécutif

Le projet a été recentré sur une architecture .NET + ONNX conforme à l'objectif de production.

Le POC Python et les endpoints de test ont été retirés. L'API expose désormais une surface claire :

- traduction immédiate ;
- traduction asynchrone ;
- progression ;
- résultat téléchargeable ;
- notation ;
- monitoring du modèle ONNX ;
- langues supportées.

Le choix ONNX permet d'exécuter le modèle dans l'application .NET, de réduire les dépendances runtime, et de mieux maîtriser le chargement et l'exploitation. La base est désormais plus lisible, plus proche d'une API industrielle, et prête à recevoir les prochaines couches de durcissement : tests, persistance, supervision et packaging modèle.

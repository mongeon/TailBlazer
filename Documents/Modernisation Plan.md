# Plan de modernisation de TailBlazer

Ce document décrit le plan de modernisation de l'application. Chaque phase devrait faire l'objet d'une PR séparée.

## État actuel

| Élément | Situation | Problème |
|---|---|---|
| Framework | `net6.0-windows` (WPF) | .NET 6 n'est plus supporté depuis novembre 2024. Les versions LTS actuelles sont .NET 8 et .NET 10. |
| Conteneur d'injection de dépendances | StructureMap 4.7.1 (`AppRegistry.cs`, scan + `AppConventions`) | Projet abandonné depuis 2018 |
| Logging | log4net 2.0.15, derrière un `ILogger` maison | Ne suit pas les standards actuels de .NET |
| UI | MaterialDesignThemes **4.7.0-ci364** (préversion CI), MaterialDesignExtensions 3.3.0, Dragablz 0.0.3.234 | Une préversion CI en production, des extensions abandonnées, et un écart avec MDIX 5.x |
| Paquets morts | `ReactiveUI.WPF` (aucune utilisation trouvée), `FodyWeavers.xml` (Costura) sans paquet Fody, `App.config` / `app.config` avec des redirections de liaison vers Rx 3.x, `packages.config` dans Fixtures | Ce sont des restes de l'époque .NET Framework |
| Tests | xUnit 2.4, FluentAssertions 6.8, `ReadLinesFixture.cs` exclu de la compilation, plusieurs `NoWarn` | FluentAssertions 8+ est passé sous licence commerciale |
| Qualité du code | Pas de `<Nullable>`, attributs JetBrains `Annotations` copiés dans le code | |
| CI/CD | Badge AppVeyor dans le README, pas de `.github/workflows` | Pas de build visible sur ce fork |
| Paramètres | XML maison (`XmlEx`, des `*ToStateConverter`) + `Settings.settings` | Fonctionne, mais c'est verbeux |

## Phase 0 : filet de sécurité (rapide, peu risquée)

> ✅ Réalisée : workflow `.github/workflows/build.yml`, `Source/Directory.Build.props`, `Source/Directory.Packages.props`, `global.json`, et suppression de `ReadLinesFixture.cs` (il testait une API qui n'existe plus).

1. Ajouter un workflow GitHub Actions (`windows-latest`) avec `dotnet build` et `dotnet test`. Sans ça, aucune régression ne sera détectée.
2. Créer `Directory.Build.props` (paramètres communs : `LangVersion`, `ImplicitUsings`, `TreatWarningsAsErrors` progressif) et `Directory.Packages.props` (gestion centralisée des versions NuGet).
3. Ajouter un `global.json` pour figer le SDK.
4. Remettre `ReadLinesFixture.cs` en compilation ou le supprimer.

## Phase 1 : nettoyage

1. Supprimer `ReactiveUI.WPF`, `FodyWeavers.xml`, les `App.config` / `app.config` (redirections de liaison inutiles sur .NET Core) et `packages.config`.
2. Retirer le `<Page Remove="Themes\ComboBox.xaml" />` et le fichier s'il est mort.
3. Convertir le projet de tests vers les dernières versions de `Microsoft.NET.Test.Sdk` et xUnit. Remplacer FluentAssertions par Shouldly ou AwesomeAssertions (le fork MIT) pour éviter la licence.

## Phase 2 : runtime et dépendances

1. Passer à `net10.0-windows` (LTS). L'option prudente est `net8.0-windows`.
2. Mettre à jour DynamicData (7 → 9.x) et System.Reactive (6.x). Attention aux changements cassants de DynamicData, qui est au cœur de l'app : les tests de la phase 0 sont essentiels ici.
3. Mettre à jour MaterialDesignThemes vers 5.x stable. Les clés de style et de ressources ont changé, donc il faudra revoir les fichiers de `Themes/*.xaml`.
4. Remplacer MaterialDesignExtensions, utilisé seulement dans `TailBlazerWindow`. MDIX 5 couvre la plupart des besoins.
5. Dragablz : garder la dernière version, ou évaluer un remplacement si elle bloque avec MDIX 5. C'est le point le plus risqué de l'UI.

## Phase 3 : architecture et hébergement

1. Remplacer StructureMap par `Microsoft.Extensions.DependencyInjection`, avec le Generic Host (`Host.CreateApplicationBuilder`) dans `App.OnStartup`.
   - Les conventions de `AppConventions` et le `Scan` deviennent des enregistrements explicites, ou un scan avec **Scrutor**.
   - Le `Ctor<Type>("type").Is(x => x.ParentType)` du logger devient `ILogger<T>`.
   - Cette étape vient avant le logging, puisque le logger y est injecté.
2. Remplacer log4net par `Microsoft.Extensions.Logging` + Serilog (sink fichier). Supprimer `Log4NetLogger`, `LogFactory` et `ILogger` maison.
3. Configuration : `appsettings.json` + `IOptions<T>` pour les options générales. Garder le store XML existant au début pour ne pas casser les paramètres des utilisateurs, avec une migration optionnelle vers JSON (System.Text.Json) plus tard.

## Phase 4 : modernisation du code C#

1. Activer `<Nullable>enable</Nullable>` projet par projet, en commençant par `TailBlazer.Domain`. Remplacer les `[NotNull]` / `[CanBeNull]` JetBrains et supprimer `Domain/Annotations`.
2. Adopter les idiomes récents là où ça simplifie : `record`, constructeurs primaires, `required`, collection expressions, pattern matching.
3. Ajouter les analyseurs .NET (`AnalysisLevel=latest`) et un `.editorconfig`.
4. Option : CommunityToolkit.Mvvm (`[ObservableProperty]`, `[RelayCommand]`) pour remplacer `AbstractNotifyPropertyChanged` et la classe `Command` maison. À faire de façon incrémentale, sans tout réécrire.

## Phase 5 : performance et distribution

1. Revoir la lecture de fichiers (`FileHandling`) avec `FileStream` + `ArrayPool` / `Span<T>`. L'app vise des fichiers de 45 Go, donc il y a des gains possibles. Les mesurer avec BenchmarkDotNet.
2. Utiliser `[GeneratedRegex]` pour les regex statiques.
3. Publication : single-file + ReadyToRun (déjà présent), avec un workflow GitHub Release et un installeur (Velopack ou MSIX), ce qui permettrait des mises à jour automatiques.
4. Mettre à jour le README (badges, liens vers le fork).

## Phase 6 (optionnelle, à long terme) : multiplateforme

Migrer vers **Avalonia** permettrait de faire tourner l'app sur Linux et macOS, ce qui est pertinent pour un outil de *tail* de logs. `TailBlazer.Domain` (DynamicData/Rx) est déjà presque indépendant de WPF, sauf `UseWPF=true` qu'il faudrait retirer. C'est par contre un gros chantier sur la couche vue, à ne lancer qu'après les phases 0 à 4.

## Ordre recommandé

**0 → 1 → 2 (runtime + DynamicData) → 3 (injection de dépendances, puis logging) → 2 (UI MDIX/Dragablz) → 4 → 5.**

La migration UI est placée après l'injection de dépendances, parce qu'elle demande des tests manuels visuels. La phase 0 (CI sur Windows) est un prérequis avant de toucher au code.

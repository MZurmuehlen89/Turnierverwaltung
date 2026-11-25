# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Database

See `src/Fussballturnierverwaltung.Infrastructure/CLAUDE.md` (connection string, EF-Migrationsbefehle, Seed) — loaded automatically once a file in that project is touched. If you need the migration commands without having touched Infrastructure yet (e.g. right after a Domain entity change), read that file directly.

## Architecture

**Clean Architecture**, **dependency direction:** `Domain ← Application ← Infrastructure ← WinUi`

Domain has no external dependencies. `WinUi` is the sole startup project, referencing `Infrastructure` and all Application-layer services.

Note: Console, WPF, and an Avalonia feasibility spike previously existed alongside WinUi (see Issue #326 for their archived architecture/status before removal). WPF was the original desktop GUI but was retired because native WPF controls (`DataGrid`, `TextBox`, `ComboBox`) bind directly to `SystemColors` keys instead of inheritable styles, making a clean Dark Mode structurally unworkable — this drove the WinUI3 rebuild (#279ff.). Console was the original UI before any graphical one existed. Avalonia was a side-by-side feasibility spike (#278) evaluated against WinUI3 and not pursued further.

## Key Patterns

**Adding a new feature** typically requires touching all four layers:
1. **Domain**: entity in `Entitaeten/`, enum in `Enums/`
2. **Application**: repository interface + service class; validator if user input is involved
3. **Infrastructure**: EF Core configuration in `Persistenz/Konfigurationen/`, repository implementation, migration
4. **WinUi**: ViewModel + View pair, registered in DI, wired into navigation — see `src/Fussballturnierverwaltung.WinUi/CLAUDE.md` for the exact pattern and its pitfalls

**DI registration** lives in the WinUi startup (see `src/Fussballturnierverwaltung.WinUi/CLAUDE.md`) and `InfrastrukturErweiterungen.cs` (Infrastructure). All services are `Scoped` except `EinstellungenService` (reads/writes a JSON file, holds no DbContext) and
`Verbindungseinstellung` (#49: caches the connection string for the whole run; the start window switches it via
`GespeicherteUebernehmen`), which are `Singleton` – both registered in `AddInfrastruktur`.

**Data folder**: settings (`einstellungen.json`) and logs (`logs\`) live under `%APPDATA%\Turnierverwaltung`. The environment variable `TURNIERVERWALTUNG_DATENPFAD` redirects that folder (meant for automated GUI tests, #518/#494). Always get these paths from `Datenpfade` (Application/Einstellungen), never build them from `SpecialFolder.ApplicationData` yourself.

## WinUi Patterns

See `src/Fussballturnierverwaltung.WinUi/CLAUDE.md` (ViewModelTemplateSelector/IDialogService/ObservableProperty-Fallstrick/CommandBar/Theme-Fallstricke) — loaded automatically once a file in that project is touched.

## Naming Conventions

All domain types, properties, methods, and enums are in **German**. English is used only where established in German IT/football context (Rating, Repository, Service, DbContext, Seed, Import, Export, ViewModel, View).

| Pattern | Usage |
|---|---|
| `_camelCase` | Private/static fields |
| `camelCase` | Local variables |
| `ALL_UPPER` | Constants |
| `PascalCase` | Everything else (properties, methods, types, public members) |

`_camelCase` for private/static fields is mandatory for source-generated `[ObservableProperty]` fields (CommunityToolkit.Mvvm generates the PascalCase property from the `_camelCase` field name). Binding for new code only — no retroactive rename of existing code (see Issue #143).

## Formatting Conventions

Enforced machine-readably by the repo-root `.editorconfig` (naming rules + indentation/brace/using-directive settings, all at `suggestion` severity — visible as IDE hints, does not fail the build) — follow it. Not covered there:

- **Section-divider comments**: for logically distinct regions inside a larger file, use `// ── Section name ────────────────────────`.
- **XAML**: wrapped attributes align to the column of the first attribute on the opening tag; self-closing tags get a space before `/>`.

Binding for new code only — no big-bang reformat of the existing codebase. When a file is opened for an issue anyway, bring the touched parts in line with these conventions as part of that change (opportunistic cleanup, not a separate pass).

## Clean Code

New code and every refactoring follow this fixed selection from Clean Code Development
(clean-code-developer.de, grade in brackets):

| Principle | What it means here |
|---|---|
| **SRP / SoC** (orange) | One reason to change per class. A ViewModel/service that combines several independent responsibilities gets split (child ViewModels, builders, helper classes). |
| **IOSP + SLA** (red/orange) | A method either coordinates other methods (integration) or contains logic (operation) — not both, and it stays on one level of abstraction. |
| **DRY** (red) | The same logic exists once. |
| **KISS / YAGNI** (red/blue) | No interfaces, base classes or options in reserve — only what the current issue needs. |
| **FCoI** (red) | Composition over inheritance: child ViewModels are composed, no new inheritance hierarchies. |
| **Information Hiding / Tell, don't ask** (yellow/green) | Child ViewModels expose state read-only; the parent calls methods instead of writing into their fields. |
| **Boy Scout Rule** (red) | Touched code is brought in line with these principles as part of the change (same rule as for formatting). |
| **Automated unit tests** (yellow) | Logic that becomes pure/stateless through a split gets tests — as far as the test project can reach it (it does not reference `WinUi`). |

**Thresholds** (project convention, not part of CCD) at which a split must be considered — and the reason stated
if it is not done: method > ~40 lines, class > ~400 lines, more than ~5 constructor dependencies. These are
guidelines, not limits: what decides is whether the class is one coherent responsibility. A class above the
threshold that belongs together stays together; a split that only serves the line count is not made.

**Deliberate deviations:**

- **Comments stay** where they record the *why* that the code cannot show (WinUI pitfalls, issue references such
  as `#406`). Clean Code's "as few comments as possible" applies only to comments that restate the code.
- **Law of Demeter does not apply to XAML binding paths** (`Ziehung.AnimationLaeuft`): pass-through properties on
  the parent ViewModel would recreate exactly the bulk a split removes.
- **No big bang**: existing code is only restructured when an issue opens the file anyway, or through dedicated
  refactoring issues (label `thema: code-qualitaet`).

**Refactoring procedure:** two commits per cut — first move the code unchanged, then clean up inside the new
class — so a behaviour change can be traced to one of the two. No behaviour change in either.

## Static Analysis

`SonarAnalyzer.CSharp` and `Roslynator.Analyzers` run on every build of the projects under `src/` and `tests/`
(#498) — never as errors, the build does not fail on findings.

| File | Purpose |
|---|---|
| `Directory.Build.props` | adds both packages, writes one SARIF file per project to `analyse/` (git-ignored) |
| `.globalconfig` | severities: which rules are on, off or downgraded to `suggestion`, each with its reason |
| `build/SonarLint.xml` | Sonar thresholds (rule parameters cannot be set in `.globalconfig`) and the exclusion of EF migrations |
| `.editorconfig` (last section) | path-specific deviations for `tests/` and the migrations |

**Warnings are the Clean Code thresholds above**: S3776 (cognitive complexity > 15), S138 (method > 40 lines),
S107 (more than 5 parameters). When an issue touches a flagged method, consider the split as described under
Clean Code — the warning is a prompt, not a mandate. Everything else that stays on is either a likely bug or
cheap to fix; fix new findings instead of suppressing them. A deliberate exception gets a scoped
`#pragma warning disable/restore` with the reason on the same line (example: S2234 where home and away are
swapped on purpose for the second leg).

**Evaluation**: after a build, `pwsh build/Analyse-Auswerten.ps1` counts warnings per rule; `-MitHinweisen`
includes `suggestion` findings, `-Regel S3776,S138` lists the locations.

**Pitfalls** (both handled in the config, keep them in mind when changing it):
- Sonar ignores `generated_code = true` from `.editorconfig` — exclusions go into `sonar.exclusions` in `build/SonarLint.xml`.
- The WinUi XAML pre-compile pass (`XamlPreCompile`) runs the compiler without analyzers but with the same
  `ErrorLog`; `Directory.Build.props` blanks the `ErrorLog` around that target, otherwise an incremental build
  overwrites the WinUi SARIF file with an empty analysis.

## Model recommendation at every issue start (user rule 18.11.2026, binding)

**Before reading code or editing anything for an issue** — whenever the user names an issue ("#682", "mach #…",
"weiter mit …") — the **first message** states, unasked: (1) the recommended model (Sonnet / Opus plans + Sonnet
implements / Opus) with a one-line reason, taken from the issue's `## Grobplan`; (2) the switch instruction
("jetzt Opus + `/clear`, Start mit …"). The user switches the model themselves — never ask them to remind you.
Then wait; no code, no `Edit`, no sed before the user has confirmed or switched. The same announcement is due at
every later hand-over (plan done → Sonnet; Sonnet stuck twice or a red measurement to interpret → Opus at once;
Opus question answered → back to Sonnet) and the model used goes into the closing comment. Details and the model
selection table: memory `feedback_modellwahl_je_issue`. An issue without a `## Grobplan` gets one (with the
recommendation) before the start.

## Definition of Done (#665)

Two levels: the **issue criteria** say whether the problem is solved (different for every issue), the **general
DoD** says whether the work is properly finished (the same for every issue). An issue is closed only when both hold.

### Issue criteria

Written into the issue body as a section `## Kriterien` **before the code** (after the blocking/staleness check;
for effort `hard` together with the plan recap). Numbered K1, K2 …; behind each one, in brackets, the test that pins
it down (unit test class or GUI scenario) — or why there is none. Effort `simple`: one or two lines are enough.
A criterion found during the work is added and marked as *nachgetragen*.

| Issue type | Criteria |
|---|---|
| Feature with UI | Given/When/Then, including the states *before/in between* (placeholders, not yet drawn, old data, empty rounds); test per criterion, unit tests shown red locally first, GUI counter-check on a throwaway `ci/<nr>-gegenprobe` branch (#591) |
| Feature without UI / improvement | Given/When/Then, unit test per criterion |
| Bug | reproduction (given/when → wrong result), expected behaviour, cause proven (no candidate fixes), regression test shown red before the fix |
| Refactoring | target structure named; behaviour unchanged (existing tests green without modification); measured values before/after (complexity, lines) |
| Performance | measured value before, target value, measured value after (in the issue) |
| Documentation / chore | what lies where afterwards |

### General DoD

Checked off in the closing comment as a block `**Definition of Done**`, each point *erfüllt*, *entfällt* or skipped
with a reason:

1. **Criteria** — every issue criterion is met, each with its test (or the stated reason there is none).
2. **Build and unit tests** green, no pending model changes (enforced by the commit hook).
3. **Code review** — `/code-review high` on the unpushed changes, findings fixed or skipped with a reason
   (documentation-only pushes excepted).
4. **GUI tests** — the full run on the push/PR is green; new UI behaviour has a GUI scenario or is listed as a gap.
5. **Manual gaps** — whatever the GUI tests cannot see (visuals, paths without scenario, real database) has been
   named and confirmed by the user, or there is none.
6. **Code quality** — Clean Code rules above followed in touched code, no new analyzer warnings there; every new
   `RibbonAktion` has a verified `IconGlyph`.
7. **Documentation** — affected `CLAUDE.md` files, GUI coverage (#529), milestone description and seed checked and
   updated.
8. **Follow-ups** — every known follow-up topic exists as its own issue (prefix, labels, phase), not only as a
   sentence in the comment.
9. **Closing comment** — commit hashes, deviations from the issue text, reasons for non-obvious decisions, this
   DoD block.

## Claude Code Hooks (#586)

Workflow rules that used to live only in memory are enforced by hooks in `.claude/settings.json` (scripts in
`.claude/hooks/`, PowerShell 7, shared helpers in `Hook-Gemeinsam.ps1`). They apply to the `Bash` and `PowerShell` tools.
`Vorfilter.sh` starts the PowerShell scripts only when the command text matches (a pwsh start costs ~0.7 s and the hooks
run on every shell call; with the filter an ordinary call costs ~0.25 s). It uses bash built-ins only, so it cannot
silently pass everything when Git Bash's tools are missing from `PATH`.

| Hook | When | What |
|---|---|---|
| `Commit-Pruefung.ps1` | before `git commit` | Only if code under `src/`/`tests/` (or build config) changed: build the solution; if Domain or the EF configuration changed, `dotnet ef migrations has-pending-model-changes`; if code outside WinUi/GUI tests changed, run the unit tests. Red → commit blocked. |
| `Push-Pruefung.ps1` | before `git push` | blocks while the working tree has uncommitted changes |
| `Migration-Anwenden-Hinweis.ps1` | after `dotnet ef migrations add` | reminds to run `dotnet ef database update` right away |

- The commit check looks at the working tree, not only the index (`git add -A && git commit` stages after the hook ran),
  of the repository the command targets (`git -C <path>`, a preceding `cd`, otherwise the session directory) — so a
  commit in another worktree is checked there. Only this project and its `git worktree`s are checked: hooks run before
  the command is approved, and building a foreign repository would execute its MSBuild code. Build restores only when project/build files changed (64 s vs. 26 s).
- Commands are recognised from the text (`git` at the start of a command, also with options like `-c`/`--no-pager`);
  a test command that merely *contains* `; git push` as a string is blocked too — put such tests in a script file.
- Hooks see the whole command before any part runs: `git commit … && git push` is blocked (the tree is still dirty
  when the push check runs). Commit and push in separate calls.
- It takes about 35–40 s for a WinUi-only change and 1.5–2 min with unit tests — **don't build and test by hand before
  committing**, the hook does it. GUI tests are not part of it (CI, see memory).
- A blocked call returns the findings to Claude (exit code 2); fix them, don't work around the hook.

## Dependency Updates (Dependabot, #542)

`.github/dependabot.yml` checks NuGet (directories `/`, `/src/*`, `/tests/*` — via the `.slnx` alone Dependabot found
only one project; `tools/` is not covered) and GitHub Actions every Monday 06:00. Minor/patch updates come grouped (`dotnet` = EF Core + `Microsoft.Extensions` + Npgsql + ProtectedData,
which depend on each other and move together within a major version — `Microsoft.Extensions` may run one major
ahead of EF Core, since 11.0 targets net10.0 too (#640/#641); when EF Core's next major arrives, take its packages
(Design, Relational, InMemory, and Npgsql's provider of the same major) in one step and drop the `ignore` for
ProtectedData majors in `dependabot.yml` (ProtectedData follows the EF Core line like Pkcs did before #49, user decision
11.11.2026); `windows-sdk`, `analyzer`, `testpakete`, `uebrige`), major updates one PR each. Dependabot alerts and security
updates are switched on in the repo settings — security fix PRs arrive any time. Every PR runs the unit and GUI tests (#525, #636).

**WinUi project:** in the old repository Dependabot skipped it (27.10.2026); since the move to the new repository
(#634) it is updated like the others (10.11.2026: #637 raised WinUi together with the `dotnet` group, #638 is the
WinUi-only `windows-sdk` group). Still check every PR that raises EF Core/`Microsoft.Extensions` (or another package
WinUi uses too) for `Fussballturnierverwaltung.WinUi.csproj` in its file list; if it is missing, raise the same
packages there in the same step — the build does not catch the mismatch, NuGet silently resolves the higher
transitive version.

How PRs are handled (user decision 27.10.2026):
- **Minor/patch groups:** Claude checks open Dependabot PRs (`gh pr list --author app/dependabot`) when asked or
  when starting an issue, and merges them itself once unit and GUI tests are green (both run on every PR since #636;
  the GUI run matters most for groups that reach the UI: `windows-sdk`, `testpakete` with FlaUI, `uebrige` with
  CommunityToolkit/Serilog). GitHub Actions groups: merge when green.
- **Major updates:** Claude reads the release notes/breaking changes, builds locally, runs unit and GUI tests (EF Core:
  additionally migrations and the real database), and presents the result — **the user decides** on the merge.
- After merging, pull `main` locally before continuing work.

## Important Domain Concepts

**`TurnierRunde.AuswaertstorRegelAktiv`** is a Boolean flag active only when `MitRueckspiel = true`. The away-goals rule is evaluated *before* `EntscheidungsVerfahren` when checking aggregate scores (AD-001).

**`TurnierRunde.RegelwerkId`** stores a per-round Regelwerk. Runden override the tournament-level default Regelwerk set on `TurnierInstanz` (AD-026).

**K.-o.-Runde braucht ein Entscheidungsverfahren (#624):** Eine K.-o.-Runde (KoRunde, Finale, SpielUmPlatzDrei) braucht ein *wirksames* Regelwerk (`runde.Regelwerk ?? TurnierInstanz.StandardRegelwerk`, wie in `SimulationsService`) mit mindestens einem Entscheidungsverfahren, sonst endet ein Remis ohne Sieger und die Folgerunde bleibt offen. Die Regel steht einmal in `KoRegelwerk` (reine Funktionen, Application/Turniere), geprüft wird an der **Konfiguration** (Prävention statt später Meldung): `KoRegelwerkPruefung` im `TurnierInstanzService` (Runden speichern, Runde ändern, Turnierstandard ändern) und im `RegelwerkService` (letztes Verfahren aus einem von einer K.-o.-Runde genutzten Regelwerk entfernen → Fehler mit Turnier und Runde); das Rundenformular bietet bei K.-o. nur Regelwerke mit Verfahren an (Wechsel der Rundenart setzt ein ungeeignetes zurück), der Turnierkopf schränkt den Standard ein, sobald eine K.-o.-Runde kein eigenes Regelwerk hat. Absicherung für Bestand: `SpielErgebnisValidator` lehnt ein K.-o.-Einzel- oder -Rückspiel ohne Sieger ab (`SiegerErforderlich`; das K.-o.-Hinspiel nicht) – auch in der Simulation, die über `ErgebnisErfassenAsync` speichert. Bestand wird nicht nachgeprüft (wie #687); Platzhalter-Spielpläne bleiben ungeprüft (Vorstufe, #620). Die frühere Sonderprüfung im `SpielplanService` entfiel.

**`Mannschaft.GesamtRating`** is a computed C# property `(O+M+D)/3m`, not stored in the database (AD-002).

**`SimulationsEngine`** is stateless (pure static methods). It uses the Knuth algorithm for Poisson draws. Pass a seeded `Random` instance for reproducible results. The seed is recorded in `SimulationsLauf.ZufallsSeed`.

**Risk axis and playing style (#673, #667):** in the detailed simulation every team sits on a risk axis (`Spielsituation`: Mauern −2, Defensiv −1, Neutral 0, Offensiv 1, AllesNachVorn 2, Brechstange 3; factors interpolated in between). `Risikoentscheidung` decides which stage is due (leading: Defensiv, then Mauern). `Mannschaft.Grundausrichtung` (5 stages) is the starting point and shifts the switching times by 5 min per stage; `Mannschaft.Flexibilitaet` (3 stages) sets how far a team follows a due stage (`S + (1 − f) · g`) and how fast (window 12/8/4 min) — see `Teamprofil`. Default (ausgeglichen/normal, value 0) behaves exactly as before #667. The seed takes the Grundausrichtung from the ratings (`SpielweisenVorschlag`) plus a hand list (`SpielweiseSeedDaten`).

**`TurnierRunde.Spielbedeutung` (#668):** gering/normal/hoch shifts the starting point of both teams on the risk axis (gering +0.5, hoch −0.25, added to the Grundausrichtung inside `Teamprofil`; switching times unchanged); normal behaves exactly as before. Pre-set by `SpielbedeutungVorbelegung` from the Rundenart (Finale hoch, Spiel um Platz 3 gering) in the round form on every Rundenart change, in the seed templates and once by migration. The importance per team from the league/group table belongs to #176 and is meant to use the same shift.

**Cards in the detailed simulation (#682):** `ROTE_KARTEN_JE_90` / `GELBE_KARTEN_JE_90` in `Spielablauf` are per **team** and match (0,075 red = 0,15 per match; 1,88 yellow, Bundesliga 2015/16–2024/25). A red card is not independent of the match state: `Spielsituation.Rotfaktor` scales the red rate ×1,6 for a team one goal behind and ×0,4 for one one goal ahead (not at larger margins; frequency stays the same). Without it the numerical advantage helps only +7 points instead of the real +16, and neither a stronger strength penalty nor the risk axis is the lever (measurement series in the issue).

**Spielstand, Rückzug und Spielkontrolle (#674):** Three effects on the chance rate of the detailed simulation, calibrated
against the realism tests (1:0 + 0:1, Dixon-Coles ρ, leads at half-time; measurement series over 10 seeds in #674):
(1) `Spielsituation.Rueckzug` – the **stages** Defensiv/Mauern that the Risikoentscheidung picks to protect a lead invite
pressure (own attack 0,95/0,90, opponent's 1,05/1,10 for a team exactly on the stage); the risk **axis** itself (Grundausrichtung,
Spielbedeutung = Haltung) is unchanged, a defensively set-up team still concedes less (#667/#668). (2) `Spielstandfaktor`
– after an equaliser (1:1, 2:2 …) both teams ×0,80, uneven ×0,96, 0:0 unchanged. (3) `Spielkontrolle` – hidden AR(1)
state z (×0,97 per minute, stationary σ 0,3), home ×e^z, away ×e^−z, normalised; deliberately **without** goal
coupling (momentum measured worse) and without "Nachlassen bei sicherer Führung" (not needed, measured worse). It draws
from its own stream (`IZufallsabzweigung`): the scripted test randoms (`VorgegebenerZufall`/`VorgegebenerAnfang`) hand it
a resting stream, so their per-minute draws stay untouched.
**Schwung nach dem Ausgleich (#692):** (4) `Spielsituation.Schwungfaktor` – whoever equalises has ×1,25 chances until the next goal, the opponent ÷1,25 (`AUSGLEICH_SCHWUNG`); state `_schwung` in `Spielablauf`, set in `TorErzielen` when the goal makes the decisive lead 0 (in the second leg the aggregate incl. first leg, like the tactics), any other goal ends it. Not applied in extra time (like `Spielstandfaktor`); a replay starts a new `Spielablauf` without it. Draws no random number (without an equaliser a game is bit-identical, `AusgleichsschwungOhneAusgleichTests`). Reason: real games are turned more often after an equaliser than simulated ones (Bundesliga, 1 up at half-time and caught up: turned 34,2 % real, 27,4 % simulated; diagnosis and measurement series over strengths 1,10–1,30 in #692). Differs from #674's rejected goal coupling of the `Spielkontrolle`: it acts only after an equaliser and ends with the next goal.

**Kondition (#677):** `Mannschaft.Kondition` (Enum `Kondition` schwach −1 / normal 0 / stark 1, Muster `Flexibilitaet`) bestimmt, wie schnell ein Team in der ausführlichen Simulation ermüdet. `Teamprofil.Ermuedungsfaktor` k = 1,15 / 1 / 0,85; Wirkung 1: `Kartenfaktor(Ermüdung · k)` je Team statt eines gemeinsamen Faktors. Wirkung 2: `Spielsituation.Konditionsrueckstand` r = (k − 1) · max(0, Ermüdung − 0,5) (null bis zur Halbzeit, wächst in der Verlängerung weiter), `Konditionsfaktor` = (1 − A·r_eigen)(1 + B·r_Gegner) auf die Chancenrate (A 0,8, B 1,0; müde Abwehr wiegt schwerer; der gemeinsame `Zeitfaktor` der Verlängerung bleibt unverändert). Wirkung 3: `Teamprofil.Offensivdrang` −5 Minuten bei schwach (mauert früher, drückt später). **Normal rechnet bitgenau wie vor #677 und zieht keinen zusätzlichen Zufall** – Kalibrierung #404 und Realismus-Tests bleiben unberührt; A und B sind an `KonditionWirkungTests` (K3–K6, 10 Seeds) kalibriert. Nicht gekoppelt: Spielkontrolle #674. Seed: einmalig aus EA-FC-Ausdauerwerten abgeleitet (`tools/Kondition-EAFC`), im Repo nur die Stufenliste `KonditionSeedListe` (oberes Viertel stark, unteres schwach, getrennt für Vereine und Nationalteams; Rest normal), kein Hinweis auf die Quelle in der App; Bestand per Migration `MannschaftKonditionVorbelegen`.

**Nervenstärke (#669):** `Mannschaft.Nervenstaerke` (Enum `Nervenstaerke` schwach −1 / normal 0 / stark 1, Muster `Kondition`) verschiebt die Trefferquote im **Elfmeterschießen** (`Elfmeterschiessen`, Basis 73 % + (OffensivRating − 70) · 0,002): stark +2, schwach −2 Punkte, **unter Druck ±3** (`Nervenwirkung`, reine Funktion, danach Grenze 0,5–0,92). Unter Druck steht ein regulärer Schuss, wenn ein Fehlschuss das Schießen sofort verloren macht (Rückstand des Schützen > seine verbleibenden Schüsse nach diesem), im Sudden Death jeder Schuss. Bewusst **keine allgemeine Druckkurve** (reale Größen nicht belastbar, Recherche im Issue) – so bleibt **normal bitgleich** und zieht keinen zusätzlichen Zufall (`NervenstaerkeTests`, Referenz vor der Änderung erzeugt). Nicht betroffen: Elfmeter im Spiel (`Chancenart`, 78,5 %), Spielsimulation, Kondition/Ermüdung. Abstand an der Siegchance gewählt (stark–normal ≈ 54 %, stark–schwach ≈ 58 %). Nicht mit #679 (Tagesform) doppelt bestrafen: die Nervenstärke wirkt nur im Elfmeterschießen. Seed: aus EA-FC-Werten (Mittel aus Ruhe am Ball und Elfmeter der elf besten Spieler) mit `tools/Kondition-EAFC`, im Repo nur `NervenstaerkeSeedListe` (oberes Viertel stark, unteres schwach, Vereine und Nationalteams getrennt); Bestand per Migration `MannschaftNervenstaerkeVorbelegen`, Pflege in der Mannschaftspflege und im CSV (Spalte `Nervenstaerke`).

**Turnierstärke (#679):** Je Turnier und Team eine feste, verborgene Abweichung von der Stärke, einmal gezogen und für alle Spiele des Turniers beibehalten (Griechenland 2004 als zusammenhängender Lauf statt Glück in jedem Spiel). `Turnierstaerke.Wert(staerkeSeed, mannschaftId)` ist rein und zustandslos: `Random` aus einem festen Hash von Seed und Mannschafts-ID (kein `HashCode.Combine`, der ist je Prozess verschieden), Normalverteilung × `STREUUNG` 0,075 (`Normalverteilung`, aus der `Spielkontrolle` herausgelöst, Zugfolge unverändert). `Turnierform(Heim, Gast)` trägt die beiden Werte in die Engine (`SimulationsEngine.Simulieren`/`…BisWiederholung`/`EntscheidungsverfahrenAnwenden`, optionaler Parameter); Faktor auf die Torerwartung `e^(t_Angreifer − t_Verteidiger − σ²)` vor dem `Clamp`, im Wiederholungsspiel wandert der Wert mit dem Team (`Tauschen()`). **Ohne Form (freies Einzelspiel, Hin-/Rückspiel ohne Turnier) bitgleich wie vor #679 und kein zusätzlicher Zufall** (K6-Referenz über 15 Seeds). Das Elfmeterschießen kennt die Form nicht. Speicher: `TurnierInstanz.StaerkeSeed` (`int?`), vergeben von `ITurnierInstanzRepository.StaerkeSeedSichernAsync`: bleibt, solange das Turnier ein Ergebnis hat; sind alle Ergebnisse gelöscht, zieht die nächste Simulation einen neuen (neuer Durchlauf); Kandidat aus `Zufallsquelle.SeedZiehen()`, nicht aus dem `rng` des Spiels (sonst verschöbe sich dessen Strom). Die Stammdaten-Ratings bleiben unberührt; Elo (#359) darf die Turnierstärke nicht einlesen. **Gemessen und verworfen:** Tagesform je Spiel (macht Wenden seltener, mehr hohe Siege) und kleinere `STAERKE_SPREIZUNG` (hilft nur bei hohen Ergebnissen, schadet 1:0, Meister, Pokal) – Wenden und 1:0 gehören zu #692, Belastung zwischen Spielen zu #693. Wirkung (`TurnierstaerkeMessung`, Explicit, 10 Seeds × 1000 K.-o.-Turniere mit den 16 stärksten BL-Teams): Sieger aus der unteren Hälfte 11,26 % → 12,55 %. Realismus-Messreihe über 30 Seeds mit Turnierstärke je Saison: 0/30 rot mit Rauschzuschlag.

**Chancenmodell der ausführlichen Simulation (#431):** `Spielablauf` würfelt je Minute keine Tore mehr, sondern erst eine Chance (Rate `ChancenJe90 · Angriffsfaktor`, Wahrscheinlichkeit `r`, ab r ≥ 1 immer; **höchstens eine Chance je Minute** (#692), ein Tor wirkt ab der Folgeminute: mehrere Chancen je Minute mit `r / (1 + r)` in einer Schleife machten ihre Zahl geometrisch und die Tore um rund 3 % überstreut, Varianz der Gesamttore 3,41 gegen real 2,99), dann wessen, dann die Art (`Chancenart`: Elfmeter 0,16 je Team und Spiel mit 78,5 %, Großchance 1,85 mit 37 %, normale Chance 10,9 mit 7 %; `Chancenmodell`), dann das Tor per Quote der Art × `Spielsituation.Verwertungsfaktor`. `Chancenprofil` teilt die unveränderte Torerwartung λ nach Ratings auf (Mittelfeld = Menge, Offensiv und gegnerische Defensive = Verwertung): `ChancenJe90 · MittlereQuote(Verwertung) = λ`, die Kalibrierung #404 und der schnelle Pfad bleiben unberührt. Eine Poisson-Torrate in Chancen × Quote zu zerlegen ändert die Torverteilung nur noch in der Streuung (eine Chance je Minute streut weniger als die frühere Schleife) – verändern können sie nur Faktoren, die auf Menge und Güte verschieden wirken. Seit Schritt 4: die Risikoachse wirkt getrennt – wer mehr riskiert, hat mehr, aber schlechtere Chancen (Rate E², Quote 1/E), gegen ein Team, das aufmacht, steigen Menge und Güte je zur Hälfte (√G); ins leere Tor geht gegen die Brechstange, was nur deren offene Abwehr trifft (`Spielsituation.Verwertung.DurchGegner`). `Spielsituation.Zeitfaktor` gibt der Chancenrate ein Zeitprofil je Viertelstunde plus Nachspielzeit (ersetzt den linearen `Torfaktor`; Verlängerung unverändert nach Ermüdung). Ereignisse: Tore tragen `SpielEreignis.Chancenart` (Altdaten und alles ohne Chance `null`); ein vergebener Elfmeter oder eine vergebene Großchance ist `EreignisArt.Grosschance`, ein verwandelter Elfmeter im Spiel ein `Tor` („Tor per Elfmeter“, zählt überall als Tor), vergebene normale Chancen erzeugen kein Ereignis; `ElfmeterTor`/`ElfmeterVerschossen` gehören weiter allein dem Elfmeterschießen. Standards (Ecke/Freistoß) sind nur Herkunft im Tortext. Statistik „Großchancen“ zählt nur Spiele mit Chancendaten (`SpielErgebnis.MitChancendaten`, setzt die Simulation; `StatistikBerechnung.Grosschancen`). `ABLAUF_KORREKTUR` (Seed 678, enges Fenster; seit #674 0,882) ist an die Stichprobe gebunden: wer an der Chancenrate dreht, prüft die Realismus-Tests neu – und zwar mit der Messreihe `RealismusMessreihe` über mehrere Seeds (Aufruf in `tests/Fussballturnierverwaltung.Tests/CLAUDE.md`), nicht durch Umstellen des Seeds von Hand: jede Parameteränderung würfelt den ganzen Zufallsstrom neu. Seit #683 werden die Realismus-Tests erst rot, wenn ihre Kennzahl die Bandgrenze um mehr als 2 Jackknife-Standardfehler überschreitet (Rauschzuschlag) – mehrere Kennzahlen liegen im Mittel *auf* der Bandgrenze, ein einzelner Seed entscheidet dann nur noch über Zufall; die Messreihe zeigt zusätzlich die echten Abweichungen vom Realbild.

**Heimvorteil, Spreizung und Zeitprofil (#694):** Der Heimanteil der Tore (Bundesliga 55,7 %) fehlte schon in der Start-Torerwartung (Ratings ergaben 53,2 %; Risikoachse und Rückzug sind als Ursache widerlegt). `SimulationsEngine.Heimvorteil` (`HEIMVORTEIL` 1,05) multipliziert die Torerwartung des Teams mit dem Ortsvorteil und dividiert die des Gegners, vor `turnierfaktor` und `Clamp`, **nicht auf neutralem Platz** (bei `Spielortvorteil.Auswaerts` hat das nominelle Gastteam ihn); die Heim-/Auswärtsstärken der Stammdaten bleiben unberührt. Er allein vergrößert die Tordifferenzen, darum `STAERKE_SPREIZUNG` 2,2 → 2,1 (2,0 bringt die Kalibrierung #404 zum Kippen). `Spielsituation._zeitprofil` ist gegenüber #431 in der 1. Halbzeit ×1,035, in der 2. ×0,977 (Bundesliga 44,6 % der Tore vor der Pause, Länderspiele 43,8 %; die Taktik verschiebt zusätzlich Tore nach hinten), die 3. Viertelstunde liegt dadurch knapp über der Schlussphase. Messreihe über 30 Seeds: Heimanteil 55,5 %, 0:0 zur Pause 25,2 %, Anteil 1. Halbzeit 44,5 %, 0/30 Seeds rot mit Rauschzuschlag (Kennzahlen „Heimanteil der Tore“, „0:0 zur Pause“ im Katalog, Bänder Anteil 1. Halbzeit ≥ 44,0 %, +1 zur Pause: Niederlage ≥ 10,0 %). Preis: Tordifferenz ≥ 3 an der Bandgrenze (22,5 %), und die kleinere Spreizung hebt auf neutralem Platz den Anteil der Sieger aus der unteren Hälfte (`TurnierstaerkeMessung`, 30 Seeds × 1000, mit Turnierstärke 11,81 → 12,75 %, Top 4 72,18 → 70,35 %) – bewusst akzeptiert. Der Heimvorteil verschiebt den Stärkeanteil der Risikoentscheidung (`_heimAnteil`) um rund 2 Minuten; die Tests mit festen Zufallsfolgen halten ihn über `HeimTeam()`/`GastTeam()` (Gast auswärts 55 %) bei 2/3. Jede Änderung an einer dieser Konstanten würfelt die Zufallsströme neu: Realismus-Messreihe laufen lassen.

**Dynamische Teamstärke (#359):** Schalter `TurnierInstanz.DynamischeTeamstaerkeAktiv` („Dynamische Teamstärke“ im Turnierkopf, Standard aus, wird geklont); sichtbar ist nur er, kein K, keine Elo-Zahl, keine gespeicherte Historie. Intern ein Elo-Modell nach eloratings (`DynamischeTeamstaerke`, rein und zustandslos): `Elo = 1500 + 18 · (Gesamtrating − 75)`, Heimbonus 60 (nur Team mit Ortsvorteil, auf neutralem Platz 0), `R_neu = R + K·G·(W − W_e)`, K nach Kategorie (WM 60, Kontinental 50, Pokal 30, sonst 20), G 1 / 1,5 / (11+N)/8. Verlängerungstore zählen, Elfmeterschießen und Los sind Remis, Hin-/Rückspiel und Wiederholungsspiel einzeln (Letzteres mit tatsächlichem Heimrecht). Der **Stand wird nicht gespeichert**, sondern vor jedem Spiel aus allen Ergebnissen davor berechnet (Schlüssel Runde, Spieltag, Spielnummer, Teil – nicht Erfassungszeit): ein gelöschtes oder überschriebenes Ergebnis wirkt damit von selbst zurück. In die Simulation geht nur die **Abweichung** vom Startwert, als Zuschlag auf die vorhandene `Turnierform` (`WIRKUNG` 0,25 · `VOLLE_WIRKUNG` = `STAERKE_SPREIZUNG`/75/18 je Elo-Punkt); die Turnierstärke (#679) fließt nicht in Elo ein, kein zusätzlicher Zufall. Einbindung allein in `SimulationsService.VorgangAsync` (je Vorgang und Turnier einmal: Stärke-Seed + `DynamischeTeamstaerkeVerlauf`); freie Einzelspiel-/Hin-Rück-Simulation und Stammdaten bleiben unberührt, **Schalter aus rechnet bitgleich** (`DynamischeTeamstaerkeAusTests`). Eichung auf das tatsächliche Update `G·(W − W_e)`, nicht auf `W − W_e` (sonst steigen Favoriten wegen G systematisch). Gemessen (Issue-Kommentar): Drift-Steigung −0,014 (`DynamischeTeamstaerkeMessung`), Realismus-Messreihe 30 Seeds mit `REALISMUS_DYNAMIK=1` 1/30 rot mit Rauschzuschlag (ρ). **Bekannte Grenze:** die Bänder sind nur für Bundesliga-Saisons mit K 20 gemessen; bei K 30 (und höher) waren in der Vormessung 3/30 Seeds rot, K.-o.-Turniere sind nicht gegen Bänder geprüft.

**Tabellenbedarf (#176):** Vor jedem Liga-/Gruppen-/Topf-Liga-Spiel (nicht Schweizer System: seine späteren Spieltage sind noch nicht angesetzt) bekommt jedes Team genau einen `Tabellenbedarf`, rein aus dem Stand **vor dem Spieltag** (Punkte, offene Spiele, Zonen der Runde aus `TabellenZonen`, je Gruppe): `TabellenbedarfRechner` rechnet wie `RangPrognose` über Punkte-Schranken ohne Tiebreaker (Punktgleichheit zählt als „kann vorbeiziehen“). Vorrang: *Ohne Ziel* (die Zone steht fest, auch rechnerisch abgestiegen) › *Remis reicht* (ein Remis sichert die beste noch erreichbare Zone) › *Sieg nötig* (nach einem Remis ist sie auch mit lauter Siegen verloren) › *Abstiegskampf* (höchstens 10 offene Spiele, Klassenerhalt nicht gesichert, höchstens 3 Punkte über dem ersten Platz einer roten Tabellenbereichs-Zone) › *Offen*. Eine Zone unter Vorbehalt („die n besten Dritten“) gilt nie als sicher; ohne Zonen, ohne Spieltag oder mit offenem Platzhalter in der Runde ist der Bedarf *Offen*. Wirkung: `Teamprofil.Bedarf` verschiebt den Startpunkt (*Abstiegskampf* −0,25, *Ohne Ziel* +0,5, addiert zur Spielbedeutung); *Sieg nötig* spielt bei Gleichstand nach dem Stufenplan für ein Tor Rückstand, *Remis reicht* nach dem Führungsplan, *Ohne Ziel* hat keine Brechstange (`Risikoentscheidung.Stufenplan`). Einbindung: `TabellenbedarfQuelle` lädt je Vorgang die Zonen und das Punktesystem, `TabellenbedarfVerlauf` hält den Stand im Speicher und schreibt ihn nach jedem Spiel fort (`Setzen`, nach dem Muster `DynamischeTeamstaerkeVerlauf`), `SimulationsService` reicht `TabellenbedarfJeTeam` als letzten optionalen Parameter an `SimulationsEngine.Simulieren` weiter – nur für Runden mit Tabelle und Zonen. **K.-o., freies Einzelspiel, Hin-/Rückspiel ohne Turnier und Bedarf *Offen* rechnen bitgleich wie vor #176 und ziehen keinen zusätzlichen Zufall** (`TabellenbedarfOhneTabelleTests`, Referenz vor der Änderung). Grundlage der Größen: Realdaten Bundesliga 2015/16–2024/25 (Abstiegskampf eher weniger Tore, ohne Ziel +0,41 ± 0,18 Tore); *Sieg nötig*/*Remis reicht* sind nicht an Realdaten gemessen. Gemessen (`TabellenbedarfMessung`, Explicit, mit Bundesliga-Zonen 1 / 6 / 15, ohne → mit Bedarf): *Ohne Ziel* Tore 3,14 → 3,47 (+0,34), Remis 26,4 → 22,1 %; *Sieg nötig* Remis 27,1 → 22,7 %; *Offen* unverändert; Realismus-Messreihe über 30 Seeds mit `REALISMUS_LAGE=1`: 0/30 rot mit Rauschzuschlag (ohne Zuschlag liegen mehr Seeds knapp außerhalb eines Bandes: Tordifferenz ≥ 3, ρ – der Preis des Bedarfs, wie im Issue vorgemessen). Laufzeit (`TabellenbedarfLaufzeitMessung`, 306 Spiele, InMemory): ohne Zonen 2670 ms, mit Zonen 2671 ms. Nicht Teil: Mentalität (#684), Titel-/Europarennen als eigener Bedarf, Belastung (#693).

**Punkteübernahme (#355):** `TurnierRunde.Punktevortrag` (Enum `Punktevortrag`: Keine, Voll, HalbiertAbgerundet, HalbiertAufgerundet) lässt die Tabelle einer Folgerunde mit Punkten aus der Abschlusstabelle der **einen** Quellrunde starten (Österreich: Meister-/Qualifikationsgruppe halbiert abgerundet; Belgien: Champions-/Europe-Play-offs halbiert aufgerundet, Abstiegs-Play-offs voll). Nur Punkte – Spiele, S/U/N und Tore zählen allein die Folgerunde. Der Vortrag steckt in `TabellenRegeln` (`Vortraege` je Team als `Vortragswert`, `VorrundenPlaetze`), `TabellenRechner` schlägt ihn auf die Punkte, nur im **Gesamtstand** (Hin-/Rückrunde, Heim- und Auswärtstabelle rechnen ohne); damit rechnen `GruppenRechner`, `RangPrognose`, `TabellenVerlaufRechner` und der Tabellenstand nach Spieltag ohne Sonderweg. `TabellenService.PunktevortragLadenAsync` lädt ihn (nur für aufgelöste Teams; `null` ohne Modus, ohne genau eine Quellrunde mit Tabelle), `TabellenbedarfGrundlage` startet den Bedarf (#176) mit ihm. **Bei Punktgleichheit entscheidet fest direkt nach den Punkten, vor allen Kriterien des Regelsets, der Rundungsvorsprung** (`Vortragswert.Rundungsvorsprung` = ungerundet − ganz: abgerundet vorn, aufgerundet hinten; verglichen wird die Rundung, nicht die Höhe des Vortrags) – kein einstellbares Kriterium. Neues Kriterium `RankingRegelArt.PlatzVorrunde` ordnet nach dem Platz in der Quellrunde (Belgien). Formular: „Punkteübernahme“ nur bei Runde mit Tabelle und Übergangsregeln aus genau einer Runde mit Tabelle, sonst springt der Modus auf Keine (`RundenViewModel.PunkteuebernahmeAbgleichen`); `UebergangsregelValidator` prüft dasselbe am Feld `Punktevortrag`. Anzeige: Spalte „Vortrag“ vor „Pkt.“ (Breite 0 ohne Punkteübernahme, `TabellenSpalten.MitVortrag`), ↓/↑ bei gerundetem Wert, Tooltip mit Herleitung, „–“ vor der Auflösung der Platzhalter. Vorlagen „Österreichische Bundesliga“ und „Belgische Pro League“ (Seed; Belgien nur aus en.wikipedia belegt, nicht aus dem Ligareglement; Ratings der nachgetragenen Klubs geschätzt, Logodateien fehlen im privaten Bildmaterial). Nicht abgebildet: Schottland (3 Durchgänge, Bilanz) → #715, „nur Direktbegegnungen“ → #716, Einzelspiel und Aufstiegs-Play-offs in Belgien, Conference-League-Play-offs in Österreich. `TabellenService` hat dadurch 6 Konstruktorparameter (S107), weil das Laden zu den Rundendaten gehört.

**Wiederholungsspiel (#547):** Teil des `SpielErgebnis`, kein eigenes Spiel: `HeimToreWiederholung`/`AuswaertsToreWiederholung` (+ `…Verlaengerung`) aus Sicht des Heimteams des Spiels, `SpielEreignis.ImWiederholungsspiel` markiert dessen Ereignisse (Minuten zählen neu ab 0; der Halbzeitstand des ersten Spiels zählt sie nicht). `HeimToreGesamt` bleibt der Stand des ersten Spiels, der Sieger kommt aus dem Wiederholungsspiel (`SiegerErmittlung`, auch im K.-o.-Rückspiel ohne Hinspielstand). Engine: `WiederholungsspielAusgang` – neuer `Spielablauf`, Heimrecht getauscht außer bei neutralem Platz oder Ortsvorteil eines Gastgebers; die Verlängerungsart *vor* dem Wiederholungsspiel in der Kette läuft dort noch einmal, das Los u. a. erst danach; höchstens ein Wiederholungsspiel. Statistik: `MannschaftsSpiel.IstWiederholungsspiel` – das Wiederholungsspiel ist ein zusätzliches Spiel mit eigenen Toren und Karten (`MannschaftsSpiel.Ereignisse` filtert nach der Markierung). Eingabe: `ErgebnisEntscheidung.WiederholungAktiv`/`Wiederholung`, Felder in der Ergebnisleiste (eigene Spalte wegen der festen Höhe). Regelwerk (#687): `RegelwerkValidator` lässt keine Kette zu, die mit `Wiederholungsspiel` endet – ein torgleiches Wiederholungsspiel hätte sonst keinen Sieger. Bestandsregelwerke mit so einer Kette (nur selbst angelegte, die Seeds haben immer ein Los dahinter) werden nicht nachgeprüft; sie scheitern wie vorher beim Speichern des Ergebnisses. Anzeige (#247): jedes Spiel mit eigener Uhr ist eine eigene Karte (`SpielberichtKarte`, im `SpaltenPanel` nebeneinander): `Zeitleiste.Erstellen` liefert nur das erste Spiel, `Zeitleiste.ErstellenWiederholungsspiel` + `SpielKopf.Wiederholungsspiel` die Wiederholungskarte mit dem tatsächlichen Heimteam links. Der Heimrecht-Tausch ist nicht gespeichert, sondern kommt aus `SimulationsEngine.HeimrechtTauscht` (Engine und Anzeige nutzen dieselbe Regel; die Anzeige kennt nur `Runde.NeutralerSpielort`). **Eigener Schritt (#691):** `SimulationsEngine.Simulieren` ist „anhalten + sofort austragen“: `SimulierenBisWiederholung` (Rückspiel: `EntscheidungsverfahrenBisWiederholung`) hält die Verfahrenskette vor dem Wiederholungsspiel an und liefert ein `SimulationsSchritt` – das Ergebnis ohne Wiederholungsspiel plus `AusstehendesWiederholungsspiel`, das auf **demselben `Random`** austrägt (`Austragen()`, ein zweiter Aufruf wirft; ein frischer Strom begänne mit festem `Zufall:Seed` dieselbe Folge wie das erste Spiel). Nur die freie Einzelspiel-Simulation nutzt den Schritt (`EinzelspielSimulationsViewModel`: Platzhalter statt Wiederholungskarte, Knopf „Wiederholungsspiel simulieren“ nur bei Regelwerken mit Wiederholungsspiel, aktiv solange es ansteht; ein neues Hinspiel, ein Wechsel von Auswärtstorregel, Mannschaft oder Regelwerk verwirft es); die Turnier-Simulation bleibt ein Zug. Jede Karte zeigt nur den Stand ihres eigenen Spiels (`SimulationsErgebnis.ErstesSpiel()` schneidet Wiederholungsspiel, Elfmeterschießen und Los ab): Elfmeterschießen und Los beenden die Kette und kamen damit nach dem Wiederholungsspiel – sie stehen auf dessen Karte (`SpielKopf.Wiederholungsspiel`/`Zeitleiste.ErstellenWiederholungsspiel` bekommen dafür das vollständige Ergebnis); Summe und „nach Wiederholungsspiel“ stehen nur im Gesamtkopf (`FreiePaarungGesamt`, bei ausstehendem Schritt „nach Rückspiel – Wiederholungsspiel steht an“, kein Sieger). Der Platzhalter ist ein eigener Wrapper, damit das `SpaltenPanel` die Spalte nur bei Bedarf zählt; `SpielberichtControl` bindet `ZeigeWiederholungAusstehend` auch am Turnier-ViewModel (dort fest `false`).

**`Zufallsquelle`** (Application/Zufall, singleton) is the only source of randomness for draws, fixtures and simulation (#519): get a `Random` per operation via `Erzeugen()`, never use `Random.Shared` or `new Random()` directly (exceptions: `GruppenZiehung`'s deliberately fixed `new Random(0)`; the `Spielablauf` branches a stream for the `Spielkontrolle` off the match's `Random` via `new Random(rng.Next(…))` – derived, reproducible with the seed, #674). The configuration value `Zufall:Seed` (environment variable `Zufall__Seed`) fixes the seed for GUI tests — every operation then starts with the same sequence; the app logs a warning at startup when it is set.

**`TurnierBaumKnoten`** has circular self-references — deleting/saving trees needs a multi-phase save; see `src/Fussballturnierverwaltung.Infrastructure/CLAUDE.md`.

## Testing

See `tests/Fussballturnierverwaltung.Tests/CLAUDE.md` — loaded automatically once a file in that project is touched.

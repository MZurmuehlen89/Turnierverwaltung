---
name: realismus-messreihe
description: Realismus-Messreihe der ausführlichen Simulation aufrufen und deuten (Seeds, Umgebungsvariablen, Rauschzuschlag, "n/30 rot"). Nutzen nach jeder Änderung an Chancenrate, Konstanten oder Spielsituation.
---

**Measurement series** `Realismus/RealismusMessreihe`
(`Explicit`, not in the normal run or CI, ~13 s per seed): `$env:REALISMUS_SEEDS='678,1,2,3'; $env:REALISMUS_AUSGABE='<csv>'`,
then `dotnet test --project tests/Fussballturnierverwaltung.Tests -- --filter-class "*RealismusMessreihe" --explicit only`
(default: seeds 678, 1–29). It writes value and jackknife SE per metric and seed as CSV and, next to it, a summary
(mean, SD over seeds, mean SE, red seeds with/without margin). Run it after every change to the simulation instead of
switching the seed by hand; the genuine deviations from the real picture (≥ 2.5 σ) belong to the calibration issues. A metric the simulation misses today is **not** loosened: its test carries an
unconditional `Skip` naming the measured value and the issue that is to fix it — that issue switches it back on.
Classes built from a noisy per-pairing estimate (half A) must not use that estimate as the prediction as well: it
regresses to the mean (#678) — group by A, measure everything in B. **Cross estimation (#679):** the draw-rate classes
use both halves: each game is assigned to a class by the win chance of the *other* half, and both halves are measured
(twice the games per class, minimum 2000 instead of 1000; a game still never decides its own class). Before, the single
half B left the smallest class (0.33–0.40) so noisy that the maximum over classes hit the band by chance (2/30 seeds red,
caused by noise, not by the tournament strength). **Sample with tournament strength (#679):** `RealismusStichprobe` draws one
`Turnierform` per season (σ 0.075), like a real season; the reference without it is commit 6d5cf95. `REALISMUS_DYNAMIK=1` runs the series with the dynamic team strength (#359; at K 30+ more seeds go red, bands are measured for K 20). **Table situation (#176):** `REALISMUS_LAGE=1` plays the seasons in schedule order with the `Tabellenbedarf` applied (Bundesliga zones 1 / 6 / 15); `Simulation/TabellenbedarfMessung` (`Explicit`, `BEDARF_SEEDS`, `BEDARF_AUSGABE`) shows the effect per need class (share of team games, goals, draw rate, points – need only classified vs. applied), `Simulation/TabellenbedarfLaufzeitMessung` the run time of a 306-game season with and without zones. Do not move bands or the
seed by hand after such a change — run the measurement series. `Simulation/TurnierstaerkeMessung` (`Explicit`, 10 seeds ×
1000 knockout tournaments of the 16 strongest Bundesliga teams, neutral venue) measures the effect (K11: share of winners from
the lower half).

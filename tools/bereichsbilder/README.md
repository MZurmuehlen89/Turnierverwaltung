# Bereichsbilder (#269)

Ein KI-generiertes Hintergrundbild je Editor bzw. Reiter, vollflächig hinter dem halbdurchsichtigen Seitenpanel
(Tönung 0,5, keine Unschärfe). Die Bilder werden **nur aus Textprompts** erzeugt – Vorlagenfotos dienen höchstens als
Ideengeber und werden nie als Eingabebild verwendet (sonst gilt das Ergebnis womöglich als Bearbeitung des Originals).

## Technische Vorgaben (für jedes Bild)

| Punkt | Vorgabe |
|---|---|
| Größe | 16:10, quer. Vorgabe war 2560 × 1600; **geliefert und eingebaut sind 1586 × 992 px** (App skaliert `UniformToFill`, die Bilder sind bewusst ruhig, Hochskalieren brächte nur Dateigröße) |
| Rohbilder | PNG als Lieferung nach `tools/bereichsbilder/original/` (nicht im Repo); `python tools/bereichsbilder/umwandeln.py` schreibt daraus die JPEGs nach `Assets/Bereiche/` (Ist: 105–270 KB je Bild) |
| Format | JPEG, Qualität ~85, sRGB, Ziel < 600 KB |
| Bildaufbau | Hauptmotiv in der Mitte (die App schneidet je nach Fenstergröße links/rechts oder oben/unten ab); Ränder ruhig |
| Inhalt | kein Text, keine Buchstaben/Zahlen, keine Logos, keine echten Marken (Vereinstrikots, WM-Pokal), keine erkennbaren Gesichter |
| Dateiname | wie in der Tabelle unten, Ablage `src/Fussballturnierverwaltung.WinUi/Assets/Bereiche/` |
| Werkzeug | eines mit kommerzieller Nutzung (Adobe Firefly; Midjourney/ChatGPT im bezahlten Tarif) – Werkzeug und Datum unten je Bild eintragen |

Weil das Panel das Bild **nicht** weichzeichnet, sollen die Bilder selbst ruhig sein: geringe Schärfentiefe,
große glatte Flächen, wenig feines Muster. Hell- und Dunkelmodus nutzen dasselbe Bild; die App legt eine
Farbüberlagerung darüber.

## Gemeinsamer Stilzusatz

An jeden Motiv-Prompt anhängen:

> Photorealistic cinematic sports photography, wide 16:10 landscape composition, main subject centered, calm image
> with large smooth areas and soft shallow depth of field, gentle bokeh, natural slightly muted colors, subtle film
> grain, no text, no letters, no numbers, no logos, no brand marks, no watermark, no recognizable faces.

Negativ-Prompt (falls das Werkzeug einen hat): `text, letters, numbers, logo, brand, watermark, signature, face, busy pattern, harsh contrast, oversaturated`

## Motive

| Datei | Editor / Reiter | Motiv-Prompt (vor den Stilzusatz) | Werkzeug, Datum |
|---|---|---|---|
| `bereich-mannschaften.jpg` | Mannschaften | Empty football dressing room, plain unbranded jerseys in red, blue, white and dark grey hanging on hooks above a wooden bench, warm orange ceiling lights | ChatGPT, 27.11.2026 |
| `bereich-laender.jpg` | Länder & Verbände › Länder | A classic leather football styled as a glowing globe with soft continents, floating in a dark blue stadium atmosphere, cool blue light | ChatGPT, 27.11.2026 |
| `bereich-verbaende.jpg` | Länder & Verbände › Verbände | Formal assembly hall with a wooden lectern in the center, a row of colorful flags without recognizable emblems in the background, warm lamp light, brown wood tones | ChatGPT, 27.11.2026 |
| `bereich-regelwerke.jpg` | Regelwerke | A referee whistle, a yellow card and a red card lying on fresh green grass, close-up from slightly above, daylight | ChatGPT, 27.11.2026 |
| `bereich-ranglisten.jpg` | Ranglisten | A plain printed ranking list on paper on a clipboard, rows of tiny illegible lines, out of focus, neutral white and light grey tones, soft daylight | ChatGPT, 27.11.2026 |
| `bereich-turnierkopf.jpg` | Turniere › Turnierkopf | A generic golden football trophy of original design on a black pedestal, single spotlight from above, dark background | ChatGPT, 27.11.2026 |
| `bereich-runden.jpg` | Turniere › Runden | A light grey magnetic wall planner with blank coloured magnetic cards in blue, red, yellow and green arranged in columns that narrow from left to right like a tournament bracket (eight, four, two, one), cards without any writing, soft office daylight, slightly angled view | ChatGPT, 27.11.2026 |
| `bereich-vorbelegung.jpg` | Turniere › Vorbelegung | View from inside a dark stadium players' tunnel towards the bright pitch at the exit, light at the end of the tunnel | ChatGPT, 27.11.2026 |
| `bereich-auslosung.jpg` | Turnierdurchführung › Auslosung | Three large glass draw bowls filled with small white balls on a stage, cool blue stage lighting, sparkling highlights | ChatGPT, 27.11.2026 |
| `bereich-spielplan.jpg` | Turnierdurchführung › Spielplan & Baum | Aerial top-down drone view of a football pitch with mowing stripes and crisp white lines, centre circle in the middle, daylight | ChatGPT, 27.11.2026 |
| `bereich-simulation.jpg` | Turnierdurchführung › Simulation | Packed football stadium at night, bright floodlights, glowing green pitch, crowd as soft blurred lights | ChatGPT, 27.11.2026 |
| `bereich-statistik.jpg` | Turnierdurchführung › Statistik | Inside a commentators' booth at night with glowing blue monitors, looking out through the window onto a floodlit pitch | ChatGPT, 27.11.2026 |
| `bereich-einzelspiel.jpg` | Einzelspiel-Simulation | A football on the centre spot of an empty pitch at dusk, violet and orange sky, low sun glow on the horizon | ChatGPT, 27.11.2026 |
| `bereich-importexport.jpg` | Import / Export | An unbranded team bus parked in front of a stadium at sunset, headlights on, warm evening light | ChatGPT, 27.11.2026 |
| `bereich-wartung.jpg` | Datenbankwartung | A groundskeeper's line-marking cart on a pitch in early morning light, fresh white line, mist, pale yellow sky | ChatGPT, 27.11.2026 |

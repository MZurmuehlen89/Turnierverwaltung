# Seed-Bildmaterial-Beschaffung (Issue #233)

Hilfsskripte zur Beschaffung von Wappen/Flaggen/Logos für die Seed-Stammdaten. Kein Teil der
ausgelieferten App — reine Build-/Datenaufbereitungs-Werkzeuge, deren Ergebnis (PNGs) manuell nach
`src/Fussballturnierverwaltung.Infrastructure/Seed/Bildmaterial/` kopiert und dort als Embedded
Resource eingebunden wird.

## Stand (2026-08-27)

| Kategorie | Status | Commit |
|---|---|---|
| 209 Nationalflaggen | ✅ fertig | abf5c3b |
| 7 Verbandslogos | ✅ fertig (Lizenz vor Veröffentlichung prüfen) | abf5c3b |
| Nationalmannschafts-Wappen | 🟡 173/209 | ed0cc09 |
| Vereinswappen | ⬜ 0/224, noch nicht begonnen | — |

## Wichtig: Rate-Limit

Siehe Memory `reference_wikimedia_api_ratelimits.md`. Unidentifizierte Clients (kein Bot-Account,
kein API-Token) sind bei Wikimedia auf **10 Anfragen/Minute** begrenzt. Beide Skripte halten das über
`SLEEP_SECONDS = 6.5` ein — **nicht verkleinern**, und **nie zwei Instanzen parallel starten**
(Wikimedia-Etikette verlangt serielle Anfragen; das hat in dieser Session zu doppelten/fehlerhaften
Log-Einträgen geführt, siehe Commit-Historie).

## Nationalmannschaften fortsetzen (36 fehlend)

```bash
cd tools/seed-bildmaterial
python fetch_national.py 0 209
```

Läuft automatisch nur die fehlenden Codes nach (Resume über `national_log.csv`, Status `OK` wird
übersprungen). Bei 36 Einträgen × ~2 Anfragen × 6.5s ≈ 8 Minuten.

Fehlende Codes (Stand 2026-08-27): AUS, BAN, BHU, BRB, CAM, CAN, CHI, COL, CRC, DOM, HAI, HON, IDN,
IND, JOR, KUW, LCA, LIE, NCA, NCL, NIR, NZL, PAN, PAR, PHI, PLE, PUR, SLV, SWE, TOG, TPE, UAE, USA,
VEN, VGB, VIN. Bei einem Teil (z. B. NIR) ist das Badge-Feld der Wikipedia-Infobox schlicht leer —
die werden vermutlich dauerhaft ohne Treffer bleiben und müssten von Hand nachgetragen werden.

## Vereinswappen (224, noch nicht begonnen)

```bash
cd tools/seed-bildmaterial
python fetch_clubs.py 0 224
```

224 Einträge × bis zu 3 Anfragen × 6.5s ≈ **bis zu 75 Minuten** — realistisch in mehreren Blöcken
laufen lassen (`start_idx`/`end_idx`-Argumente nutzen), z. B. `python fetch_clubs.py 0 60`,
`python fetch_clubs.py 60 120`, … Zwischen Blöcken ruhig eine Pause einlegen.

**Kein Trigramme-Äquivalent zur Verifikation** (anders als bei Nationalmannschaften) — Vereinsnamen
sind nicht immer eindeutig, False-Positives sind möglich. Nach dem Lauf eine Stichprobe (~15-20
zufällige PNGs) visuell prüfen, bevor eingebettet wird.

## Nach jedem Lauf: konvertieren + einbetten

```bash
cd svgtool
dotnet run -- ../svg_national ../png_national 64      # bzw. ../svg_clubs ../png_clubs 64
```

Dann PNGs nach `src/Fussballturnierverwaltung.Infrastructure/Seed/Bildmaterial/Nationalmannschaften/`
bzw. `.../Vereinswappen/` kopieren (Dateiname = `Mannschaft.Kurzname` + `.png`; bei Vereinen kann
`Kurzname` über Länder hinweg kollidieren — im Zweifel prüfen, ob im Zielordner schon eine Datei mit
demselben Namen für ein anderes Land liegt). `dotnet build` + `dotnet test` (462 Tests) laufen lassen,
dann WPF/Console-Projekt neu bauen und `SeedDatenService.InitialisierenAsync()` einmal ausführen
(z. B. Console-DLL direkt starten, bricht beim interaktiven Menü ab — reicht für den Seed-Lauf).
Ergebnis per `sqlcmd` gegen `Mannschaften.Wappen` verifizieren (siehe Memory `feedback_db_first_debugging`).

`MannschaftsBildmaterialNachruesten` in `SeedDatenService.cs` übernimmt das Befüllen automatisch
(Kategorie "Vereinswappen" ist dort bereits verdrahtet, auch wenn aktuell noch keine Dateien vorliegen).

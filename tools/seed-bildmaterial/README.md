# Seed-Bildmaterial-Beschaffung (Issue #233)

Hilfsskripte zur Beschaffung von Wappen/Flaggen/Logos für die Seed-Stammdaten. Kein Teil der
ausgelieferten App — reine Build-/Datenaufbereitungs-Werkzeuge, deren Ergebnis (PNGs) manuell nach
`src/Fussballturnierverwaltung.Infrastructure/Seed/Bildmaterial/` kopiert und dort als Embedded
Resource eingebunden wird.

## Ablage (#633)

Nur die Flaggen aus flag-icons (MIT) liegen im Repo (`Bildmaterial/Flaggen/`). Wappen, Bilder der
Nationalmannschaften, Turnier- und Verbandslogos (Marken, urheberrechtlich geschützt) und die olympische
Flagge von Chinesisch Taipei gehören in den git-ignorierten Ordner `Bildmaterial/Privat/<Kategorie>/` – lokal
ein Klon des privaten Repos `Turnierverwaltung-Bildmaterial`, neue Bilder dort committen. Der Build bettet
beide Ordner ein; `SeedBildQuelle` sucht erst in `Flaggen/` usw., dann in `Privat/`. Fehlt ein Bild, bleibt
das Feld leer und die App zeigt den Platzhalter.

`flaggen_mapping.tsv` ordnet nur die flag-icons-Flaggen zu (211). Chinesisch Taipei (TPE) fehlt dort bewusst: statt der
Flagge der Republik China (flag-icons `tw`) zeigt der Seed die olympische Flagge, und die liegt als Marke im privaten Repo.

## Stand (2026-08-27)

| Kategorie | Status | Commit |
|---|---|---|
| 209 Nationalflaggen | ✅ fertig | abf5c3b |
| 7 Verbandslogos | ✅ fertig (Marken, nur im privaten Repo, #633) | abf5c3b |
| Nationalmannschafts-Wappen | ✅ 197/209 abgeschlossen (12 dauerhaft ohne Wikipedia-Badge) | ed0cc09, (dieser Commit) |
| Vereinswappen | ✅ 224/224 abgeschlossen | (dieser Commit) |
| Vereinswappen Nachtrag #351 (GER 3. Liga/Regionalliga, ENG Championship/League One) | ✅ 76/76 (`python fetch_clubs.py 224 300`) | (#351-Commit) |
| Vereinswappen Nachtrag #381 (Top 200 UEFA-Klub-Koeffizient, `python fetch_clubs.py 300 415`) | ✅ 114/115 (HNK Rijeka: Infobox ohne Logo) | (#381-Commit) |
| Vereinswappen Nachtrag #575 (Segunda División, Serie B, Ligue 2 2024/25, `python fetch_clubs.py 415 475`) | ✅ 60/60 | (#575-Commit) |
| Vereinswappen Nachtrag #581 (FIFA-Klub-WM-Ranking 2021–2024, Vereine außerhalb Europas, `python fetch_clubs.py 475 659`) | ✅ 181/184 (Central Coast FC, Galaxy FC, Vaivase-Tai: Infobox ohne Logo) | (#581-Commit) |
| Vereinswappen Nachtrag #582 (oberste Ligen außerhalb Europas, `python fetch_clubs.py 659 808`) | ✅ 145/149 (Dibba Al-Hisn, MA Tétouan, US Tataouine, Royal AM: Infobox ohne Logo) | (#582-Commit) |
| Turnierlogos #481 (ein Logo je Wettbewerb der Seed-Vorlagen) | ✅ siehe `turnierlogos_log.csv` | (#481-Commit) |

Damit ist Issue #233 inhaltlich vollständig — die verbleibenden 12 Nationalmannschaften haben laut
Stichprobe (z. B. Panama) ein schlicht leeres `Badge`-Feld in der Wikipedia-Infobox, keine
Skript-Limitierung. Nachtrag nur noch manuell möglich (Verbands-Website o. Ä.), kein Automatisierungs-
Kandidat mehr.

## Turnierlogos (#481)

`python fetch_turnierlogos.py` holt je Wettbewerb das Logo aus der Infobox des englischen Wikipedia-Artikels
nach `raw_turnierlogos/` (Code = Dateiname, muss zu `TurnierLogoNachruestung` in `Infrastructure/Seed`
passen). Danach `cd svgtool && dotnet run -- ../raw_turnierlogos ../png_turnierlogos 128` und die PNGs nach
`src/Fussballturnierverwaltung.Infrastructure/Seed/Bildmaterial/Privat/Turnierlogos/` kopieren. Zeigt die Infobox
kein Logo (z. B. nur den Pokal), steht in `LOGO_OVERRIDES` der Dateiname des Logos.

Die Wikipedia-Zugriffe aller Skripte liegen in `wikipedia_api.py`.

## Wichtig: Rate-Limit

Siehe Memory `reference_wikimedia_api_ratelimits.md`. Unidentifizierte Clients (kein Bot-Account,
kein API-Token) sind bei Wikimedia auf **10 Anfragen/Minute** begrenzt. Beide Skripte halten das über
`SLEEP_SECONDS = 6.5` ein — **nicht verkleinern**, und **nie zwei Instanzen parallel starten**
(Wikimedia-Etikette verlangt serielle Anfragen; das hat in dieser Session zu doppelten/fehlerhaften
Log-Einträgen geführt, siehe Commit-Historie).

## Bugs behoben in dieser Session (relevant bei künftiger Wiederverwendung der Skripte)

- **`get_thumb_url` verwarf Commons-Dateien fälschlich**: Die MediaWiki-API liefert für Dateien, die
  nicht lokal auf en.wikipedia liegen (sondern auf Wikimedia Commons — der Normalfall), eine
  `pageid=-1`, **obwohl** `imageinfo` trotzdem mitgeliefert wird. Der alte Code hat `pid == "-1"` als
  "nicht gefunden" behandelt und dadurch ca. 19 von 36 fehlenden Nationalmannschafts-Wappen verworfen.
  Fix: nur auf Vorhandensein von `imageinfo` prüfen, nicht auf die Page-ID.
- **Titel-Sonderfälle**: Länder mit eigener Frauen-Nationalmannschaft haben den generischen Titel
  ("X national football team") oft als Begriffsklärungsseite belegt — Fallback auf
  "X men's national football team" ergänzt. Zusätzlich ein `TITLE_OVERRIDES`-Dict für Länder, deren
  Artikel komplett anders heißen (USA/Kanada/Australien nutzen "soccer" statt "football", Chinese
  Taipei hat kein "national" im Titel).
- **Vereine mit neuerem Infobox-Template**: `extract_club_image` prüfte nur auf den String
  "Infobox football club", neuere Artikel nutzen aber `{{Football club infobox` (umgekehrte
  Wortreihenfolge) — beide Varianten werden jetzt akzeptiert.
- **Doppelt kodierte Dateinamen**: Manche Commons-Dateien sind im Wikitext bereits Prozent-kodiert
  (Akzente/Umlaute), `urllib.parse.quote()` hat das doppelt kodiert und den Lookup zerschossen — Fix:
  vorher `unquote()`.
- **Deutsche Vereinsnamen ohne Wikipedia-Entsprechung**: Für ausländische Vereine mit eingedeutschtem
  Namen (Inter Mailand, AC Mailand, CSKA/Spartak Moskau, Metalist Charkiw, AEK Athen, River Plate) traf
  die Volltextsuche oft Spielerseiten statt der Vereinsseite — `TITLE_OVERRIDES`-Dict mit den echten
  englischen Artikeltiteln ergänzt.
- **Echte Kurzname-Kollision in den Stammdaten gefunden**: `Celta Vigo` und `Celtic FC` hatten beide
  `Kurzname="CEL"` (Wappen-Lookup-Schlüssel) — Celtic FC wurde in `VereinsmannschaftenSeedDaten.cs` auf
  `"CLT"` umbenannt (`vereine.tsv` entsprechend angepasst). Bei einer bereits laufenden lokalen DB muss
  der Bestandsdatensatz zusätzlich per UPDATE nachgezogen werden (Kurzname + `Wappen=NULL`, dann Seed
  erneut laufen lassen) — ein reiner Neustart mit dem aktualisierten Seed-Code reicht bei einer
  **frischen** DB dagegen aus. Dabei zusätzlich ein unreferenziertes Duplikat (zwei identische
  "Celtic FC"-Zeilen) in der lokalen Dev-DB gefunden und gelöscht — Ursache unklar, vermutlich Rest
  eines früheren doppelten Seed-Laufs, kein Code-Bug mehr in der aktuellen Seed-Quelle gefunden.
- **Vereine außerhalb Europas (#581)**: Infoboxen in anderer Schreibweise (`Infobox Football club`,
  `infobox football club`), Logos als GIF/JPG/JPEG und mit altem Präfix `Image:` — `extract_club_image`
  prüft deshalb ohne Groß-/Kleinschreibung und nimmt diese Formate an (svgtool skaliert Rasterbilder).

## Nach jedem Lauf: konvertieren + einbetten

```bash
cd svgtool
dotnet run -- ../svg_national ../png_national 64      # bzw. ../svg_clubs ../png_clubs 64
```

Dann PNGs nach `src/Fussballturnierverwaltung.Infrastructure/Seed/Bildmaterial/Privat/Nationalmannschaften/`
bzw. `.../Vereinswappen/` kopieren (Dateiname = `Mannschaft.Kurzname` + `.png`; bei Vereinen kann
`Kurzname` über Länder hinweg kollidieren — im Zweifel prüfen, ob im Zielordner schon eine Datei mit
demselben Namen für ein anderes Land liegt), im privaten Repo committen und die WinUi-App neu bauen und
starten — der Start führt `SeedDatenService.InitialisierenAsync()` aus und trägt die neuen Bilder nach.
Ergebnis per `sqlcmd` gegen `Mannschaften.Wappen` verifizieren (siehe Memory `feedback_db_first_debugging`).

`MannschaftsBildmaterialNachruesten` in `SeedDatenService.cs` übernimmt das Befüllen automatisch
(Kategorie "Vereinswappen" ist dort bereits verdrahtet, auch wenn aktuell noch keine Dateien vorliegen).

# Kondition-EAFC (#677, #669)

Leitet die Kondition (schwach/normal/stark, #677) und die Nervenstärke (#669) der Seed-Mannschaften einmalig aus den
Spielerwerten in EA SPORTS FC ab. Im Repo landet nur die Stufenliste (`KonditionSeedListe.cs`,
`NervenstaerkeSeedListe.cs`, nur abweichende Teams), **keine Rohdaten**; in der App gibt es keinen Hinweis auf EA oder FIFA (Datenbankrecht, Marken – vor einem Verkauf Quelle und
Bedingungen prüfen, siehe #677).

| Skript | Tut |
|---|---|
| `abrufen.py` | holt die Spielerliste der EA-Ratings-API (`drop-api.ea.com/rating/ea-sports-fc`) **seriell**, 100 je Seite mit 1,5 s Pause (Retry-After wird beachtet), und speichert je Spieler nur Gesamtwert, Ausdauer, Ruhe am Ball, Elfmeter, Team, Nation und Geschlecht |
| `auswerten.py` | Teamwert = Mittel des Spielerwerts der elf besten Männer je Team (Kondition: Ausdauer; Nervenstärke: Mittel aus Ruhe am Ball und Elfmeter); Vereine und Nationalteams getrennt in Viertel (oben stark, unten schwach); schreibt die Liste der gewählten Kennzahl und `bericht.txt` – die andere Liste bleibt unberührt |

## Aufruf

Nur Python-Standardbibliothek; die Rohdaten in einen Ordner **außerhalb** des Repos legen:

```powershell
python -I tools/Kondition-EAFC/abrufen.py <datenordner>
python -I tools/Kondition-EAFC/auswerten.py <datenordner> . kondition        # KonditionSeedListe.cs
python -I tools/Kondition-EAFC/auswerten.py <datenordner> . nervenstaerke    # NervenstaerkeSeedListe.cs
```

## Zuordnung

- Nationalteams über `NATIONEN` (EA-Name → FIFA-Code); nur Seed-Länder zählen.
- Vereine über den normalisierten Namen, dann Ähnlichkeit ab `SCHWELLE` (die Treffer darunter listet der Bericht zur
  Sichtung), dazu die von Hand gepflegte `ABGLEICH`-Liste. Unsichere Fälle (gleicher Name in mehreren Ländern,
  Reserveteams) stehen nicht darin und bleiben „normal“; `AUSSCHLUSS` nimmt falsche Ähnlichkeitstreffer heraus.
- Stand 18.11.2026: 576 EA-Teams mit mindestens elf Spielern, 392 (68 %) einem der 808 Seed-Vereine zugeordnet; 93 von
  212 Nationalteams haben genug Spieler. Ergebnis: 121 stark, 121 schwach (196 Vereine, 46 Nationalteams).
- Nach einem neuen Lauf: Migration nur nötig, wenn Bestandsdatenbanken die neue Liste bekommen sollen – die bestehende
  Migration `MannschaftKonditionVorbelegen` wendet die Liste zum Zeitpunkt ihrer Ausführung an.
- Nervenstärke (#669, Lauf vom 21.11.2026, 17.873 Spieler): dieselbe Zuordnung (392 Vereine, 93 Nationalteams), Ergebnis 121 stark,
  121 schwach (196 Vereine, 46 Nationalteams); Bestand per Migration `MannschaftNervenstaerkeVorbelegen`. Die Zeilen im Ordner
  `__pycache__` und die Rohdaten sind nicht im Repo.

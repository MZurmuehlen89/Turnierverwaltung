# Realismus-Zielwerte (#678)

Die Zielwerte der Realismus-Tests (`tests/Fussballturnierverwaltung.Tests/Simulation/SimulationsRealismusTests.cs`)
stammen aus diesen beiden Auswertungen öffentlicher Rohdaten. Die Werte und ihre Herleitung stehen im Body von #678.

| Skript | Daten | Liefert |
|---|---|---|
| `bundesliga.py` | football-data.co.uk, Bundesliga 2015/16–2024/25 (3059 Spiele) | Ergebnisverteilung, Halbzeitstände und Führungssicherung, Rote Karten und Überzahl, Remisquote je Favoritenklasse (Pinnacle-Schlussquoten), Dixon-Coles-ρ |
| `torminuten.py` | martj42/international_results (CC0), Pflicht-Länderspiele ab 2010 | Tore je 15 Minuten, Nachspielzeit, Führung nach Minute t, längste torlose Phase |

Torminuten aus der Bundesliga gibt es nicht frei als Rohdaten. Die Länderspieldaten dienen deshalb nur für das
Zeitprofil (Anteile, keine absoluten Zahlen).

## Daten holen

Die Daten werden **nicht eingecheckt**. In einen eigenen, leeren Ordner laden:

- `https://football-data.co.uk/mmz4281/<saison>/D1.csv` für `<saison>` = `1516`, `1617`, … `2425`, jeweils
  gespeichert als `D1_<saison>.csv` (also `D1_1516.csv` … `D1_2425.csv`)
- `https://raw.githubusercontent.com/martj42/international_results/master/goalscorers.csv`
- `https://raw.githubusercontent.com/martj42/international_results/master/results.csv`

## Aufruf

Nur Python-Standardbibliothek. Die Skripte liegen außerhalb des Datenordners und bekommen ihn als Argument:

```powershell
$env:PYTHONIOENCODING = 'utf-8'
python -I tools/Realismus-Zielwerte/bundesliga.py <datenordner>
python -I tools/Realismus-Zielwerte/torminuten.py <datenordner>
```

`bundesliga.py` braucht wegen der Dixon-Coles-Anpassung je Saison etwa eine halbe Minute.

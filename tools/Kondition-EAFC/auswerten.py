"""Leitet aus den Spielerwerten (abrufen.py) eine dreistufige Kennzahl je Seed-Mannschaft ab und schreibt die
Stufenliste (#677 Kondition, #669 Nervenstärke); Bericht in bericht-<kennzahl>.txt.
Aufruf: python -I auswerten.py <datenordner> <repo-wurzel> [kondition|nervenstaerke]   (Standard: kondition)
Ein Lauf schreibt nur die Liste seiner Kennzahl; die der anderen bleibt unberührt.

Kondition: Spielerwert = Ausdauer. Nervenstärke: Spielerwert = Mittel aus Ruhe am Ball (composure) und Elfmeter (penalties).
Teamwert = Mittel des Spielerwerts der elf Spieler mit dem höchsten Gesamtwert (nur Männer, mindestens elf Spieler).
Vereine und Nationalteams werden getrennt in Viertel geteilt (die Nationalspieler liegen höher): oberes Viertel
stark, unteres schwach, Rest normal – verglichen werden nur Teams, die einer Seed-Mannschaft zugeordnet sind.
Zuordnung der Vereine über den Namen (normalisiert, dann Ähnlichkeit ab SCHWELLE) plus die Abgleichsliste unten;
die Nationalteams über NATIONEN (EA-Name → FIFA-Code). Nicht Zugeordnetes bleibt „normal“."""
import difflib
import json
import re
import statistics
import sys
import unicodedata
from collections import defaultdict
from pathlib import Path

SCHWELLE = 0.88
ELF = 11

# Je Kennzahl: Spielerwert, Enum, Name der erzeugten Liste, Issue für den Dateikopf.
KENNZAHLEN = {
    "kondition": {
        "wert": lambda s: s["ausdauer"], "enum": "Kondition", "praefix": "Kondition", "issue": "#677",
        "bezeichnung": "Ausdauer",
    },
    "nervenstaerke": {
        "wert": lambda s: (s["ruhe"] + s["elfmeter"]) / 2, "enum": "Nervenstaerke", "praefix": "Nervenstaerke",
        "issue": "#669", "bezeichnung": "Ruhe am Ball und Elfmeter",
    },
}

# EA-Name → FIFA-Code des Seed-Landes.
NATIONEN = {
    "Afghanistan": "AFG", "Albania": "ALB", "Algeria": "ALG", "Andorra": "AND", "Angola": "ANG",
    "Antigua and Barbuda": "ATG", "Argentina": "ARG", "Armenia": "ARM", "Australia": "AUS", "Austria": "AUT",
    "Azerbaijan": "AZE", "Bangladesh": "BAN", "Barbados": "BRB", "Belarus": "BLR", "Belgium": "BEL", "Benin": "BEN",
    "Bermuda": "BER", "Bolivia": "BOL", "Bosnia and Herzegovina": "BIH", "Brazil": "BRA", "Bulgaria": "BUL",
    "Burkina Faso": "BFA", "Burundi": "BDI", "Cameroon": "CMR", "Canada": "CAN", "Cape Verde Islands": "CPV",
    "Central African Republic": "CTA", "Chad": "CHA", "Chile": "CHI", "China PR": "CHN", "Chinese Taipei": "TPE",
    "Colombia": "COL", "Comoros": "COM", "Congo": "CGO", "Congo DR": "COD", "Costa Rica": "CRC", "Croatia": "CRO",
    "Cuba": "CUB", "Cyprus": "CYP", "Czech Republic": "CZE", "Denmark": "DEN", "Dominican Republic": "DOM",
    "Ecuador": "ECU", "Egypt": "EGY", "El Salvador": "SLV", "England": "ENG", "Equatorial Guinea": "EQG",
    "Estonia": "EST", "Faroe Islands": "FRO", "Finland": "FIN", "France": "FRA", "Gabon": "GAB", "Gambia": "GAM",
    "Georgia": "GEO", "Germany": "GER", "Ghana": "GHA", "Gibraltar": "GIB", "Greece": "GRE", "Grenada": "GRN",
    "Guatemala": "GUA", "Guinea": "GUI", "Guinea-Bissau": "GNB", "Guyana": "GUY", "Haiti": "HAI", "Holland": "NED",
    "Honduras": "HON", "Hong Kong": "HKG", "Hungary": "HUN", "Iceland": "ISL", "India": "IND", "Indonesia": "IDN",
    "Iran": "IRN", "Iraq": "IRQ", "Israel": "ISR", "Italy": "ITA", "Jamaica": "JAM", "Japan": "JPN", "Jordan": "JOR",
    "Kenya": "KEN", "Korea Republic": "KOR", "Kosovo": "KVX", "Latvia": "LVA", "Lebanon": "LBN", "Liberia": "LBR",
    "Libya": "LBA", "Liechtenstein": "LIE", "Lithuania": "LTU", "Luxembourg": "LUX", "Madagascar": "MDG",
    "Malawi": "MWI", "Malaysia": "MAS", "Mali": "MLI", "Malta": "MLT", "Mauritania": "MTN", "Mexico": "MEX",
    "Moldova": "MDA", "Montenegro": "MNE", "Montserrat": "MSR", "Morocco": "MAR", "Mozambique": "MOZ",
    "Namibia": "NAM", "New Zealand": "NZL", "Niger": "NIG", "Nigeria": "NGA", "North Macedonia": "MKD",
    "Northern Ireland": "NIR", "Norway": "NOR", "Pakistan": "PAK", "Palestine": "PLE", "Panama": "PAN",
    "Paraguay": "PAR", "Peru": "PER", "Philippines": "PHI", "Poland": "POL", "Portugal": "POR",
    "Republic of Ireland": "IRL", "Romania": "ROU", "Russia": "RUS", "Rwanda": "RWA", "Saudi Arabia": "KSA",
    "Scotland": "SCO", "Senegal": "SEN", "Serbia": "SRB", "Sierra Leone": "SLE", "Slovakia": "SVK",
    "Slovenia": "SVN", "Somalia": "SOM", "South Africa": "RSA", "Spain": "ESP", "Sri Lanka": "SRI",
    "St. Kitts and Nevis": "SKN", "St. Lucia": "LCA", "Suriname": "SUR", "Sweden": "SWE", "Switzerland": "SUI",
    "Syria": "SYR", "Tajikistan": "TJK", "Tanzania": "TAN", "Thailand": "THA", "Togo": "TOG",
    "Trinidad and Tobago": "TRI", "Tunisia": "TUN", "Turkey": "TUR", "Uganda": "UGA", "Ukraine": "UKR",
    "United Arab Emirates": "UAE", "United States": "USA", "Uruguay": "URU", "Uzbekistan": "UZB", "Vanuatu": "VAN",
    "Venezuela": "VEN", "Wales": "WAL", "Zambia": "ZAM", "Zimbabwe": "ZIM",
}

# EA liefert „Curaçao“ und „Côte d'Ivoire“ mit kaputtem Sonderzeichen – über den Anfang erkannt.
NATIONEN_ANFANG = {"Cura": "CUW", "C�te": "CIV", "Côte": "CIV"}

# EA-Vereinsname → (Kurzname, Land) der Seed-Mannschaft, wo der Name nicht von selbst passt. Nach Sichtung der
# Nächstliegenden je Nichttreffer von Hand gepflegt; was unsicher ist (gleicher Name in mehreren Ländern, Reserveteams),
# steht nicht drin und bleibt „normal“.
ABGLEICH = {
    "Albacete BP": ("ALB", "ESP"), "Argentinos Jrs.": ("ARJ", "ARG"), "Athletic Club": ("ATH", "ESP"),
    "Atl. Nacional": ("NAL", "COL"), "Barcelona SC": ("BSG", "ECU"), "FC Barcelona": ("BAR", "ESP"),
    "Başakşehir": ("BSK", "TUR"), "Beşiktaş": ("BJK", "TUR"), "Braunschweig": ("EBR", "GER"),
    "Brighton": ("BHA", "ENG"), "CFR 1907 Cluj": ("CFR", "ROU"), "Celta": ("CEL", "ESP"),
    "Charlton Ath": ("CHA", "ENG"), "Club Brugge": ("CLB", "BEL"), "Como": ("CMO", "ITA"),
    "D. Alavés": ("ALV", "ESP"), "Daejeon Hana": ("DJN", "KOR"), "Dep. Riestra": ("RIE", "ARG"),
    "Doncaster": ("DON", "ENG"), "Düsseldorf": ("F95", "GER"), "ESTAC Troyes": ("TRY", "FRA"),
    "En Avant Guingamp": ("EAG", "FRA"), "Estudiantes": ("EDL", "ARG"), "FC Basel 1893": ("BAS", "SUI"),
    "F.C. København": ("FCK", "DEN"), "Ferencvárosi TC": ("FTC", "HUN"), "Fiorentina": ("FIO", "ITA"),
    "Frankfurt": ("SGE", "GER"), "Genoa": ("GEN", "ITA"), "Havre AC": ("LHV", "FRA"), "Hearts": ("HRT", "SCO"),
    "Hertha BSC": ("BSC", "GER"), "Huddersfield": ("HUD", "ENG"), "Ind. Rivadavia": ("IRI", "ARG"),
    "Instituto": ("ICO", "ARG"), "Ipswich": ("IPS", "ENG"), "Jagiellonia": ("JAG", "POL"),
    "Jeonbuk Hyundai": ("JHM", "KOR"), "Juventus": ("JUV", "ITA"), "Lech Poznań": ("LPO", "POL"),
    "Legia Warszawa": ("LGW", "POL"), "Leverkusen": ("B04", "GER"), "M'gladbach": ("BMG", "GER"),
    "Man Utd": ("MNU", "ENG"), "Mantova": ("MAN", "ITA"), "Melb. Victory": ("MEV", "AUS"),
    "Montpellier": ("MHC", "FRA"), "New England": ("NER", "USA"), "Newell's": ("NOB", "ARG"),
    "Norwich": ("NOR", "ENG"), "Nott'm Forest": ("NFO", "ENG"), "OL": ("OL", "FRA"), "OM": ("OM", "FRA"),
    "Olympiacos FC": ("OLY", "GRE"), "PAOK FC": ("PAK", "GRE"), "Paris SG": ("PSG", "FRA"),
    "Parma": ("PCA", "ITA"), "Peterborough": ("PET", "ENG"), "Philadelphia": ("PHU", "USA"),
    "Pogoń Szczecin": ("POG", "POL"), "Preston": ("PNE", "ENG"), "QPR": ("QPR", "ENG"),
    "Qarabağ FK": ("QAR", "AZE"), "R. Oviedo": ("OVI", "ESP"), "RB Salzburg": ("RBS", "AUT"),
    "Red Bulls": ("NYR", "USA"), "SJ Earthquakes": ("SJE", "USA"), "SV Waldhof": ("WAL", "GER"),
    "Sampdoria": ("SMP", "ITA"), "Sarmiento": ("SAR", "ARG"), "Shakhtar Donetsk": ("SCH", "UKR"),
    "Sheffield Wed": ("SHW", "ENG"), "Slavia Praha": ("SLP", "CZE"), "Sounders FC": ("SEA", "USA"),
    "Sp. Charleroi": ("CHL", "BEL"), "Sparta Praha": ("SPP", "CZE"), "Spurs": ("TOT", "ENG"),
    "Stockport": ("STO", "ENG"), "Talleres": ("TAL", "ARG"), "U. de Chile": ("UCH", "CHI"),
    "Univ. Craiova": ("UCR", "ROU"), "Universitario": ("UNI", "PER"), "VfL Bochum 1848": ("BOC", "GER"),
    "Vålerenga Fotball": ("VAL", "NOR"), "Well. Phoenix": ("WEL", "NZL"), "West Brom": ("WBA", "ENG"),
    "West Ham": ("WHU", "ENG"), "Whitecaps FC": ("VWC", "CAN"), "Wolves": ("WOL", "ENG"), "Wycombe": ("WYC", "ENG"),
}

# Ähnlichkeitstreffer, die nach Sichtung falsch sind (Sporting Kansas City ist nicht Sporting Lissabon).
AUSSCHLUSS = {"Sporting KC"}

FUELLWOERTER = {"fc", "cf", "ac", "as", "sc", "sv", "vfb", "vfl", "tsg", "fk", "sk", "afc", "bsc", "ssc", "us", "ud",
                "cd", "rc", "rcd", "sd", "ca", "club", "de", "calcio", "1", "04", "05", "1899", "ik", "if", "bk"}


def normal(name):
    s = unicodedata.normalize("NFKD", name.lower())
    s = "".join(c for c in s if not unicodedata.combining(c))
    s = re.sub(r"[^a-z0-9 ]", " ", s)
    return " ".join(w for w in s.split() if w not in FUELLWOERTER)


def seed_lesen(wurzel):
    daten = Path(wurzel) / "src/Fussballturnierverwaltung.Infrastructure/Seed/Daten"
    vereine = re.findall(
        r'new\("([^"]+)",\s*"([^"]+)",\s*"([A-Z]{3})"', (daten / "VereinsmannschaftenSeedDaten.cs").read_text(encoding="utf-8"))
    nationen = set(re.findall(
        r'new\("([A-Z]{3})",\s*\d', (daten / "NationalmannschaftenSeedDaten.cs").read_text(encoding="utf-8")))
    return vereine, nationen


def nation_code(name):
    if name in NATIONEN:
        return NATIONEN[name]
    return next((c for anfang, c in NATIONEN_ANFANG.items() if name.startswith(anfang)), None)


def teamwerte(gruppen, spielerwert):
    """{Name: Spielerliste} → {Name: Mittel des Spielerwerts der elf besten}, nur mit mindestens elf Spielern."""
    werte = {}
    for name, liste in gruppen.items():
        beste = sorted(liste, key=lambda s: -s["gesamt"])[:ELF]
        if len(beste) == ELF:
            werte[name] = statistics.mean(spielerwert(s) for s in beste)
    return werte


def stufen(werte):
    """{Schlüssel: Wert} → {Schlüssel: +1/-1} für oberes/unteres Viertel."""
    reihe = sorted(werte, key=lambda k: werte[k])
    viertel = len(reihe) // 4
    ergebnis = {k: -1 for k in reihe[:viertel]}
    ergebnis.update({k: 1 for k in reihe[len(reihe) - viertel:]})
    return ergebnis


def verein_zuordnen(ea_namen, vereine):
    nach_name = defaultdict(list)
    for eintrag in vereine:
        nach_name[normal(eintrag[0])].append(eintrag)
    zuordnung, unsicher, mehrdeutig = {}, [], []
    nach_schluessel = {(e[1], e[2]): e for e in vereine}
    for ea in ea_namen:
        if ea in AUSSCHLUSS:
            continue
        if ea in ABGLEICH:
            zuordnung[ea] = nach_schluessel[ABGLEICH[ea]]  # KeyError = Tippfehler in der Abgleichsliste
            continue
        n = normal(ea)
        treffer = nach_name.get(n)
        verhaeltnis = 1.0
        if not treffer:
            beste = difflib.get_close_matches(n, list(nach_name), n=1, cutoff=SCHWELLE)
            if beste:
                treffer = nach_name[beste[0]]
                verhaeltnis = difflib.SequenceMatcher(None, n, beste[0]).ratio()
        if treffer and len(treffer) > 1:
            mehrdeutig.append(ea)
        elif treffer:
            zuordnung[ea] = treffer[0]
            if verhaeltnis < 1.0:
                unsicher.append((ea, treffer[0][0], verhaeltnis))
    return zuordnung, unsicher, mehrdeutig


def zeile(art, kurz, land, stufe, enum, kommentar=""):
    name = "Stark" if stufe > 0 else "Schwach"
    return f'        new(Mannschaftsart.{art}, "{kurz}", "{land}", {enum}.{name}),{kommentar}'


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    ordner, wurzel = Path(sys.argv[1]), sys.argv[2]
    kennzahl = KENNZAHLEN[sys.argv[3] if len(sys.argv) > 3 else "kondition"]
    spielerwert, enum, praefix = kennzahl["wert"], kennzahl["enum"], kennzahl["praefix"]
    spieler = [s for s in json.loads((ordner / "spieler.json").read_text(encoding="utf-8")) if not s["frauen"]]
    vereine, seed_nationen = seed_lesen(wurzel)
    bericht = []

    # ── Vereine ──
    gruppen = defaultdict(list)
    for s in spieler:
        if s["team"]:
            gruppen[s["team"]].append(s)
    werte = teamwerte(gruppen, spielerwert)
    zuordnung, unsicher, mehrdeutig = verein_zuordnen(werte, vereine)
    verein_werte = {ea: werte[ea] for ea in zuordnung}
    verein_stufen = stufen(verein_werte)
    bericht.append(f"Vereine: {len(werte)} EA-Teams mit mindestens 11 Spielern, {len(zuordnung)} zugeordnet "
                   f"({len(zuordnung) / max(1, len(werte)):.0%}); Seed hat {len(vereine)} Vereine")
    bericht.append("Mehrdeutig (nicht zugeordnet): " + ", ".join(mehrdeutig))
    bericht.append("Nicht zugeordnet: " + ", ".join(sorted(set(werte) - set(zuordnung) - set(mehrdeutig))))
    bericht.append("Zuordnung über Ähnlichkeit (prüfen): " + "; ".join(f"{a} -> {b} ({r:.2f})" for a, b, r in unsicher))
    bericht.append(f"Vereine: Quartile ({kennzahl["bezeichnung"]}) {[round(q, 1) for q in statistics.quantiles(verein_werte.values(), n=4)]}")

    # ── Nationalteams ──
    nat = defaultdict(list)
    for s in spieler:
        code = nation_code(s["nation"]) if s["nation"] else None
        if code in seed_nationen:
            nat[code].append(s)
    nat_werte = teamwerte(nat, spielerwert)
    nat_stufen = stufen(nat_werte)
    unbekannt = sorted({s["nation"] for s in spieler if s["nation"] and nation_code(s["nation"]) is None})
    bericht.append(f"Nationalteams: {len(nat_werte)} von {len(seed_nationen)} Seed-Teams mit mindestens 11 Spielern; "
                   f"EA-Nationen ohne Code: {unbekannt}")
    bericht.append(f"Nationalteams: Quartile ({kennzahl["bezeichnung"]}) {[round(q, 1) for q in statistics.quantiles(nat_werte.values(), n=4)]}")

    # ── Liste schreiben ──
    zeilen = []
    for ea, stufe in sorted(verein_stufen.items(), key=lambda kv: (zuordnung[kv[0]][2], zuordnung[kv[0]][1])):
        name, kurz, land = zuordnung[ea]
        zeilen.append(zeile("Verein", kurz, land, stufe, enum, f"   // {name}"))
    for code, stufe in sorted(nat_stufen.items()):
        zeilen.append(zeile("Nationalmannschaft", code, code, stufe, enum))
    schluessel = [(z.split('"')[1], z.split('"')[3]) for z in zeilen if ".Verein," in z]
    doppelt = sorted({k for k in schluessel if schluessel.count(k) > 1})
    bericht.append(f"Liste: {len(verein_stufen)} Vereine, {len(nat_stufen)} Nationalteams; doppelte Vereinsschlüssel: {doppelt}")
    if doppelt:  # das statische Wörterbuch im Seed würde beim Start werfen – nichts schreiben
        raise SystemExit("Abbruch, mehrere EA-Teams auf derselben Seed-Mannschaft: " + str(doppelt))

    ziel = Path(wurzel) / f"src/Fussballturnierverwaltung.Infrastructure/Seed/Daten/{praefix}SeedListe.cs"
    kopf = ("using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;\n"
            "using Fussballturnierverwaltung.Domain.Enums;\n\n"
            "namespace Fussballturnierverwaltung.Infrastructure.Seed.Daten;\n\n"
            f"/// <summary>Erzeugt von <c>tools/Kondition-EAFC/auswerten.py</c> ({kennzahl['issue']}) – nicht von Hand ändern. Nur die\n"
            "/// Teams, die vom Standard „normal“ abweichen.</summary>\n"
            f"public static class {praefix}SeedListe\n{{\n"
            f"    public static readonly IReadOnlyList<{praefix}SeedDaten.Eintrag> Handliste =\n    [\n")
    ziel.write_text(kopf + "\n".join(zeilen) + "\n    ];\n}\n", encoding="utf-8")
    (ordner / f"bericht-{praefix.lower()}.txt").write_text("\n\n".join(bericht), encoding="utf-8")
    print("\n\n".join(bericht))


if __name__ == "__main__":
    main()

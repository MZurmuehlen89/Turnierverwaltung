# Beschafft Wettbewerbslogos fuer die Seed-Turniervorlagen (Issue #481) ueber die Wikipedia-Action-API.
# Nutzung: python fetch_turnierlogos.py
# Ist wiederholbar: bereits erfolgreiche Codes (Status OK in turnierlogos_log.csv) werden uebersprungen.
#
# WICHTIG (siehe reference_wikimedia_api_ratelimits.md): SLEEP_SECONDS=6.5 haelt uns unter dem
# 10-Anfragen/Minute-Limit fuer unidentifizierte Clients. NICHT mehrere Instanzen parallel starten.
#
# Die Codes muessen mit den Dateinamen in TurnierLogoNachruestung._logoDateien (Infrastructure/Seed) uebereinstimmen.
import csv, os, re, sys, time
from wikipedia_api import api_get, get_content, get_thumb_url, download_binary

SLEEP_SECONDS = 6.5
OUT_DIR = "raw_turnierlogos"
LOG_FILE = "turnierlogos_log.csv"
os.makedirs(OUT_DIR, exist_ok=True)

# Code (Dateiname) -> Titel des englischen Wikipedia-Artikels
WETTBEWERBE = {
    "FIFA_WM": "FIFA World Cup",
    "FIFA_KLUB_WM": "FIFA Club World Cup",
    "UEFA_EM": "UEFA European Championship",
    "UEFA_NATIONS_LEAGUE": "UEFA Nations League",
    "COPA_AMERICA": "Copa América",
    "AFRIKA_CUP": "Africa Cup of Nations",
    "AFC_ASIAN_CUP": "AFC Asian Cup",
    "CONCACAF_GOLD_CUP": "CONCACAF Gold Cup",
    "UEFA_CHAMPIONS_LEAGUE": "UEFA Champions League",
    "UEFA_EUROPA_LEAGUE": "UEFA Europa League",
    "UEFA_CONFERENCE_LEAGUE": "UEFA Conference League",
    "DFB_POKAL": "DFB-Pokal",
    "FA_CUP": "FA Cup",
    "BUNDESLIGA": "Bundesliga",
    "ZWEITE_BUNDESLIGA": "2. Bundesliga",
    "PREMIER_LEAGUE": "Premier League",
    "LA_LIGA": "La Liga",
    "SERIE_A": "Serie A",
    "LIGUE_1": "Ligue 1",
}

# Code -> Dateiname, wo die Infobox kein brauchbares Logo zeigt. WM/EM/Copa haben kein Wettbewerbslogo ohne Jahr
# (nur Schriftzug bzw. Pokalfoto) - dort gilt das Logo der neuesten Ausgabe (Nutzerentscheidung #481). La Liga und
# Ligue 1: Fassung ohne Sponsor; fuer Serie A und FA Cup gibt es keine.
LOGO_OVERRIDES = {
    "FIFA_WM": "2026 FIFA World Cup emblem.svg",
    "UEFA_EM": "UEFA Euro 2028 Logo.svg",
    "COPA_AMERICA": "Conmebol Copa America 2024 Logo.svg",
    "LA_LIGA": "LaLiga 2023 Vertical Logo.svg",
    "LIGUE_1": "Ligue 1 2024 Logo.png",
}

def extract_logo(content):
    # Infobox football tournament / football league: Logo steht unter "logo" oder "image".
    m = re.search(r"\|\s*(?:logo|image)\s*=\s*\[?\[?(?:File:|Image:)?([^\|\]\n{}]+\.(?:svg|png|jpg|SVG|PNG|JPG))", content)
    return m.group(1).strip() if m else None

already_done = set()
if os.path.exists(LOG_FILE):
    with open(LOG_FILE, encoding="utf-8") as f:
        already_done = {row[0] for row in csv.reader(f) if len(row) > 1 and row[1] == "OK"}

with open(LOG_FILE, "a", newline="", encoding="utf-8") as log_f:
    writer = csv.writer(log_f)
    for code, title in WETTBEWERBE.items():
        if code in already_done:
            continue
        try:
            logo = LOGO_OVERRIDES.get(code)
            if not logo:
                content = get_content(title)
                time.sleep(SLEEP_SECONDS)
                logo = extract_logo(content) if content else None
            if not logo:
                writer.writerow((code, "NO_MATCH", title))
                continue
            thumb_url = get_thumb_url(logo, width=256)
            time.sleep(SLEEP_SECONDS)
            if not thumb_url:
                writer.writerow((code, "NO_THUMB", f"{title} | {logo}"))
                continue
            ext = os.path.splitext(thumb_url.split("?")[0])[1].lower() or ".png"
            download_binary(thumb_url, os.path.join(OUT_DIR, f"{code}{ext}"))
            time.sleep(SLEEP_SECONDS)
            writer.writerow((code, "OK", f"{title} | {logo}"))
        except Exception as e:
            writer.writerow((code, "ERROR", str(e)))
        log_f.flush()
        print(code, file=sys.stderr)

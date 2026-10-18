# Beschafft Vereinswappen (Issue #233) ueber die Wikipedia-Action-API.
# Nutzung: python fetch_clubs.py [start_idx] [end_idx]   (Default: alle 224 Zeilen aus vereine.tsv)
# Ist wiederholbar/resumable: bereits erfolgreiche Kurznamen (Status OK in clubs_log.csv) werden uebersprungen.
#
# WICHTIG (siehe reference_wikimedia_api_ratelimits.md): SLEEP_SECONDS=6.5 haelt uns unter dem
# 10-Anfragen/Minute-Limit fuer unidentifizierte Clients. NICHT erhoehen ohne triftigen Grund,
# NICHT mehrere Instanzen parallel starten (Etikette verlangt serielle Anfragen).
import urllib.request, urllib.parse, json, re, time, os, sys, csv
from wikipedia_api import HEADERS, api_get, get_content, get_thumb_url, download_binary

SLEEP_SECONDS = 6.5
OUT_DIR = "svg_clubs"
LOG_FILE = "clubs_log.csv"
os.makedirs(OUT_DIR, exist_ok=True)

def search_title(query):
    url = f"https://en.wikipedia.org/w/api.php?action=query&list=search&srsearch={urllib.parse.quote(query)}&format=json&srlimit=1"
    data = api_get(url)
    hits = data.get("query", {}).get("search", [])
    return hits[0]["title"] if hits else None

def extract_club_image(content):
    if "Infobox football club" not in content and "Football club infobox" not in content:
        return None
    m = re.search(r"\|\s*(?:image|current)\s*=\s*\[?\[?(?:File:)?([^\|\]\n]+\.(?:svg|png|SVG|PNG))", content)
    return m.group(1).strip() if m else None

# Deutsche Namen aus vereine.tsv, die auf en.wikipedia unter einem anderen
# Titel liegen bzw. deren Volltextsuche auf Fehltreffer (Spielerseiten etc.) lief.
TITLE_OVERRIDES = {
    "INT": "Inter Milan",
    "ACM": "AC Milan",
    "CSK": "PFC CSKA Moscow",
    "SPK": "FC Spartak Moscow",
    "MET": "FC Metalist Kharkiv",
    "AEK": "AEK Athens F.C.",
    "RIV": "Club Atlético River Plate",
}

with open("vereine.tsv", encoding="utf-8") as f:
    rows = [line.rstrip("\n").split("\t") for line in f if line.strip()]

already_done = set()
if os.path.exists(LOG_FILE):
    with open(LOG_FILE, encoding="utf-8") as f:
        for row in csv.reader(f):
            if row and row[2] == "OK":
                already_done.add(row[1])

start_idx = int(sys.argv[1]) if len(sys.argv) > 1 else 0
end_idx = int(sys.argv[2]) if len(sys.argv) > 2 else len(rows)

log_f = open(LOG_FILE, "a", newline="", encoding="utf-8")
writer = csv.writer(log_f)
ok_count = 0
total = 0

for i, (name, kurz, land) in enumerate(rows[start_idx:end_idx], start=start_idx):
    if kurz in already_done:
        continue
    total += 1
    try:
        title_used = TITLE_OVERRIDES.get(kurz, name)
        content = get_content(title_used)
        time.sleep(SLEEP_SECONDS)
        badge = extract_club_image(content) if content else None
        if not badge:
            found_title = search_title(f"{name} football club")
            time.sleep(SLEEP_SECONDS)
            if found_title:
                content = get_content(found_title)
                time.sleep(SLEEP_SECONDS)
                title_used = found_title
                badge = extract_club_image(content) if content else None
        if badge:
            thumb_url = get_thumb_url(badge, width=128)
            time.sleep(SLEEP_SECONDS)
            if thumb_url:
                ext = os.path.splitext(thumb_url.split("?")[0])[1].lower() or ".png"
                download_binary(thumb_url, os.path.join(OUT_DIR, f"{kurz}{ext}"))
                writer.writerow((name, kurz, "OK", f"{title_used} | {badge}"))
                ok_count += 1
            else:
                writer.writerow((name, kurz, "NO_THUMB", f"title={title_used} badge={badge}"))
        else:
            writer.writerow((name, kurz, "NO_MATCH", f"tried={title_used}"))
    except Exception as e:
        writer.writerow((name, kurz, "ERROR", str(e)))
    log_f.flush()
    if (i - start_idx) % 10 == 0:
        print(f"... {i}/{end_idx}", file=sys.stderr)

log_f.close()
print(f"Fertig: {ok_count}/{total} erfolgreich in diesem Lauf.")

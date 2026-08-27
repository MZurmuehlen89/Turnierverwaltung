# Beschafft Nationalmannschafts-Wappen (Issue #233) ueber die Wikipedia-Action-API.
# Nutzung: python fetch_national.py [start_idx] [end_idx]   (Default: alle 209 Zeilen aus fifa_english.tsv)
# Ist wiederholbar/resumable: bereits erfolgreiche Codes (Status OK in national_log.csv) werden uebersprungen.
#
# WICHTIG (siehe reference_wikimedia_api_ratelimits.md): SLEEP_SECONDS=6.5 haelt uns unter dem
# 10-Anfragen/Minute-Limit fuer unidentifizierte Clients. NICHT erhoehen ohne triftigen Grund,
# NICHT mehrere Instanzen parallel starten (Etikette verlangt serielle Anfragen).
import urllib.request, urllib.parse, json, re, time, os, sys, csv

SLEEP_SECONDS = 6.5
HEADERS = {"User-Agent": "Turnierverwaltung-SeedBot/1.0 (privates Hobbyprojekt, siehe github.com/MZurmuehlen89/Turnierverwaltung; kontakt via GitHub Issue)"}
OUT_DIR = "svg_national"
LOG_FILE = "national_log.csv"
os.makedirs(OUT_DIR, exist_ok=True)

def api_get(url, retries=5):
    req = urllib.request.Request(url, headers=HEADERS)
    for attempt in range(retries):
        try:
            with urllib.request.urlopen(req, timeout=15) as r:
                return json.load(r)
        except urllib.error.HTTPError as e:
            if e.code == 429 and attempt < retries - 1:
                wait = int(e.headers.get("Retry-After", 10 * (attempt + 1)))
                time.sleep(wait)
                continue
            raise

def get_content(title):
    url = f"https://en.wikipedia.org/w/api.php?action=query&titles={urllib.parse.quote(title)}&prop=revisions&rvprop=content&rvslots=main&format=json&redirects=1"
    data = api_get(url)
    pages = data.get("query", {}).get("pages", {})
    for pid, page in pages.items():
        if pid == "-1":
            return None
        rev = page.get("revisions", [{}])[0]
        return rev.get("slots", {}).get("main", {}).get("*", "")
    return None

def extract_badge(content):
    m = re.search(r"\|\s*Badge\s*=\s*\[?\[?(?:File:)?([^\|\]\n]+\.(?:svg|png|SVG|PNG))", content)
    return m.group(1).strip() if m else None

def extract_trigramme(content):
    m = re.search(r"\|\s*FIFA[ _]?Trigramme\s*=\s*([A-Za-z]{2,4})", content)
    return m.group(1).strip().upper() if m else None

def get_thumb_url(filename, width=128):
    url = f"https://en.wikipedia.org/w/api.php?action=query&titles={urllib.parse.quote('File:' + filename)}&prop=imageinfo&iiprop=url&iiurlwidth={width}&format=json"
    data = api_get(url)
    pages = data.get("query", {}).get("pages", {})
    for pid, page in pages.items():
        if pid == "-1":
            return None
        info = page.get("imageinfo", [{}])[0]
        return info.get("thumburl") or info.get("url")
    return None

def download_binary(url, out_path, retries=5):
    req = urllib.request.Request(url, headers=HEADERS)
    for attempt in range(retries):
        try:
            with urllib.request.urlopen(req, timeout=20) as r:
                content = r.read()
            with open(out_path, "wb") as f:
                f.write(content)
            return len(content)
        except urllib.error.HTTPError as e:
            if e.code == 429 and attempt < retries - 1:
                wait = int(e.headers.get("Retry-After", 10 * (attempt + 1)))
                time.sleep(wait)
                continue
            raise

with open("fifa_english.tsv", encoding="utf-8") as f:
    rows = [line.strip().split("\t") for line in f if line.strip()]

already_done = set()
if os.path.exists(LOG_FILE):
    with open(LOG_FILE, encoding="utf-8") as f:
        for row in csv.reader(f):
            if row and row[2] == "OK":
                already_done.add(row[0])

start_idx = int(sys.argv[1]) if len(sys.argv) > 1 else 0
end_idx = int(sys.argv[2]) if len(sys.argv) > 2 else len(rows)

log_f = open(LOG_FILE, "a", newline="", encoding="utf-8")
writer = csv.writer(log_f)
ok_count = 0
total = 0

for i, (code, ename) in enumerate(rows[start_idx:end_idx], start=start_idx):
    if code in already_done:
        continue
    total += 1
    try:
        title = f"{ename} national football team"
        content = get_content(title)
        time.sleep(SLEEP_SECONDS)
        trig = extract_trigramme(content) if content else None
        badge = extract_badge(content) if content else None
        if badge:
            thumb_url = get_thumb_url(badge, width=128)
            time.sleep(SLEEP_SECONDS)
            if thumb_url:
                ext = os.path.splitext(thumb_url.split("?")[0])[1].lower() or ".png"
                download_binary(thumb_url, os.path.join(OUT_DIR, f"{code}{ext}"))
                writer.writerow((code, ename, "OK", f"{title} | {badge} | trig={trig}"))
                ok_count += 1
            else:
                writer.writerow((code, ename, "NO_THUMB", f"title={title} trig={trig} badge={badge}"))
        else:
            writer.writerow((code, ename, "NO_MATCH", f"title={title} trig={trig} badge=None"))
    except Exception as e:
        writer.writerow((code, ename, "ERROR", str(e)))
    log_f.flush()
    if (i - start_idx) % 10 == 0:
        print(f"... {i}/{end_idx}", file=sys.stderr)

log_f.close()
print(f"Fertig: {ok_count}/{total} erfolgreich in diesem Lauf.")

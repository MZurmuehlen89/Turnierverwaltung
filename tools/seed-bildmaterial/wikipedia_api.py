# Gemeinsame Wikipedia-API-Zugriffe der Beschaffungsskripte (#233, #481).
# Rate-Limit: siehe reference_wikimedia_api_ratelimits.md - die Pausen halten die aufrufenden Skripte ein.
import json, time, urllib.error, urllib.parse, urllib.request

HEADERS = {"User-Agent": "Turnierverwaltung-SeedBot/1.0 (privates Hobbyprojekt, siehe github.com/MZurmuehlen89/Turnierverwaltung; kontakt via GitHub Issue)"}

def api_get(url, retries=6):
    req = urllib.request.Request(url, headers=HEADERS)
    for attempt in range(retries):
        try:
            with urllib.request.urlopen(req, timeout=15) as r:
                return json.load(r)
        except urllib.error.HTTPError as e:
            if e.code == 429 and attempt < retries - 1:
                wait = int(e.headers.get("Retry-After", 15 * (attempt + 1)))
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

def get_thumb_url(filename, width=128):
    # Manche Commons-Dateinamen stehen im Wikitext bereits Prozent-kodiert
    # (z.B. Umlaute/Akzente) - ohne unquote wuerde quote() sie doppelt kodieren.
    filename = urllib.parse.unquote(filename)
    url = f"https://en.wikipedia.org/w/api.php?action=query&titles={urllib.parse.quote('File:' + filename)}&prop=imageinfo&iiprop=url&iiurlwidth={width}&format=json"
    data = api_get(url)
    pages = data.get("query", {}).get("pages", {})
    for pid, page in pages.items():
        info_list = page.get("imageinfo")
        if not info_list:
            continue
        info = info_list[0]
        return info.get("thumburl") or info.get("url")
    return None

def download_binary(url, out_path, retries=6):
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
                wait = int(e.headers.get("Retry-After", 15 * (attempt + 1)))
                time.sleep(wait)
                continue
            raise

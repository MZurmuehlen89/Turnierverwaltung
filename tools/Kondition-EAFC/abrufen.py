"""Holt die Spielerliste der EA-Ratings-API seitenweise (seriell, mit Pause) und speichert je Spieler nur die Felder,
die die Auswertung braucht (#677). Aufruf: python -I abrufen.py <datenordner>
Die Rohdaten bleiben lokal (im Repo git-ignoriert) – eingecheckt wird nur die Stufenliste aus auswerten.py."""
import json, sys, time, urllib.request, urllib.error
from pathlib import Path

URL = "https://drop-api.ea.com/rating/ea-sports-fc?locale=en&limit={limit}&offset={offset}"
SEITE = 100
PAUSE_SEKUNDEN = 1.5
KENNUNG = "Turnierverwaltung-Datentool"


def seite_holen(offset):
    for versuch in range(6):
        anfrage = urllib.request.Request(URL.format(limit=SEITE, offset=offset), headers={"User-Agent": KENNUNG})
        try:
            with urllib.request.urlopen(anfrage, timeout=60) as antwort:
                return json.load(antwort)
        except urllib.error.HTTPError as fehler:
            warten = int(fehler.headers.get("Retry-After", 0) or 0) or 10 * (versuch + 1)
            print(f"offset {offset}: HTTP {fehler.code}, warte {warten} s", flush=True)
            time.sleep(warten)
        except (urllib.error.URLError, TimeoutError) as fehler:
            print(f"offset {offset}: {fehler}, neuer Versuch", flush=True)
            time.sleep(10 * (versuch + 1))
    raise SystemExit(f"offset {offset}: aufgegeben")


def main():
    ziel = Path(sys.argv[1])
    ziel.mkdir(parents=True, exist_ok=True)
    spieler, offset, gesamt = [], 0, None
    while gesamt is None or offset < gesamt:
        daten = seite_holen(offset)
        gesamt = daten["totalItems"]
        for s in daten["items"]:
            spieler.append({
                "id": s["id"], "gesamt": s["overallRating"], "ausdauer": s["stats"]["stamina"]["value"],
                "teamId": (s.get("team") or {}).get("id"), "team": (s.get("team") or {}).get("label"),
                "nation": (s.get("nationality") or {}).get("label"), "frauen": (s.get("gender") or {}).get("id") != 0,
            })
        offset += SEITE
        print(f"{len(spieler)}/{gesamt}", flush=True)
        time.sleep(PAUSE_SEKUNDEN)
    (ziel / "spieler.json").write_text(json.dumps(spieler, ensure_ascii=False), encoding="utf-8")


if __name__ == "__main__":
    main()

"""Zielwerte #678: Torminuten aus martj42/international_results (LÃ¤nderspiele ab 2010, ohne Freundschaftsspiele)."""
import csv, os, sys
from collections import Counter, defaultdict

DATEN = sys.argv[1]
ergebnisse = {}
with open(os.path.join(DATEN, "results.csv"), encoding="utf-8") as f:
    for z in csv.DictReader(f):
        if z["date"] < "2010" or z["tournament"] == "Friendly" or z["home_score"] in ("", "NA"):
            continue
        ergebnisse[(z["date"], z["home_team"], z["away_team"])] = (int(z["home_score"]), int(z["away_score"]), z["tournament"])

tore = defaultdict(list)
with open(os.path.join(DATEN, "goalscorers.csv"), encoding="utf-8") as f:
    for z in csv.DictReader(f):
        k = (z["date"], z["home_team"], z["away_team"])
        if k not in ergebnisse or z["minute"] in ("", "NA"):
            continue
        tore[k].append((int(float(z["minute"])), z["team"] == z["home_team"]))

# nur Spiele mit vollstÃ¤ndiger Torliste; VerlÃ¤ngerungstore (> 90) fallen weg, das Ergebnis gilt nach 90 Minuten
# (Nachspielzeit ist in den Daten als Minute 45 bzw. 90 erfasst)
vollst = {k: sorted((m, h) for m, h in v if m <= 90) for k, v in tore.items() if len(v) == sum(ergebnisse[k][:2])}
for k, liste in vollst.items():
    ergebnisse[k] = (sum(1 for _, h in liste if h), sum(1 for _, h in liste if not h), ergebnisse[k][2])
ohne_tore = [k for k, e in ergebnisse.items() if e[0] + e[1] == 0]
print(f"Spiele mit Ergebnis: {len(ergebnisse)}, mit vollstÃ¤ndiger Torliste (<=90'): {len(vollst)}, torlos: {len(ohne_tore)}")
alle_minuten = [m for v in vollst.values() for m, _ in v]
n_tore = len(alle_minuten)
mc = Counter(alle_minuten)
print(f"Tore: {n_tore}; je Spiel (inkl. 0:0) {n_tore/(len(vollst)+len(ohne_tore)):.3f}")
for m in [42,43,44,45,46,47,87,88,89,90]:
    print(f"  Minute {m}: {mc[m]}")
buckets = [(1,15),(16,30),(31,45),(46,60),(61,75),(76,90)]
print("Anteil je 15 Minuten (45/90 inkl. Nachspielzeit):")
for lo, hi in buckets:
    print(f"  {lo:2d}-{hi:2d}: {sum(mc[m] for m in range(lo,hi+1))/n_tore:.3f}")
print(f"  1. HZ gesamt {sum(mc[m] for m in range(1,46))/n_tore:.3f}")
ueber45 = mc[45] - (mc[42]+mc[43]+mc[44])/3
ueber90 = mc[90] - (mc[87]+mc[88]+mc[89])/3
print(f"Ãœberschuss Minute 45 ~ {ueber45:.0f} ({ueber45/n_tore:.3f}), Minute 90 ~ {ueber90:.0f} ({ueber90/n_tore:.3f}) -> Nachspielzeit-Anteil ~ {(ueber45+ueber90)/n_tore:.3f}")

# FÃ¼hrung zur Minute t -> Endergebnis (vollstÃ¤ndige Spiele + torlose)
print("\nFÃ¼hrung nach Minute t -> Sieg/Remis/Niederlage des FÃ¼hrenden")
for t in [15, 30, 45, 60, 75, 85]:
    for v in [1, 2, 3]:
        c = Counter()
        for k, liste in vollst.items():
            h = sum(1 for m, heim in liste if m <= t and heim); a = sum(1 for m, heim in liste if m <= t and not heim)
            if abs(h - a) != v: continue
            fh = h > a
            e = ergebnisse[k]; d = (e[0] - e[1]) * (1 if fh else -1)
            c[(d > 0) - (d < 0)] += 1
        n = sum(c.values())
        if n >= 30:
            print(f"  t={t:2d} +{v} n={n:5d}  S {c[1]/n:.3f} U {c[0]/n:.3f} N {c[-1]/n:.3f}")

# lÃ¤ngste torlose Phase je Spiel (0..90)
print("\nLÃ¤ngste torlose Phase je Spiel")
laengen = []
for k, liste in vollst.items():
    punkte = [0] + [m for m, _ in liste] + [90]
    laengen.append(max(b - a for a, b in zip(punkte, punkte[1:])))
laengen += [90] * len(ohne_tore)
n = len(laengen)
for s in [45, 60, 75, 90]:
    print(f"  >= {s} min: {sum(1 for l in laengen if l >= s)/n:.3f}")
print(f"  Median {sorted(laengen)[n//2]}")
# SpÃ¤tes Tor: Anteil Spiele mit Tor ab der 85. Minute
print(f"Spiele mit Tor ab 85' (inkl. 90+): {sum(1 for v in vollst.values() if any(m >= 85 for m, _ in v))/n:.3f}")

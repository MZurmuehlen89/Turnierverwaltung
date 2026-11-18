"""Zielwerte #678 aus football-data.co.uk, Bundesliga 2015/16â€“2024/25."""
import csv, glob, math, os, sys
from collections import Counter, defaultdict

DATEN = sys.argv[1]
spiele = []
for pfad in sorted(glob.glob(os.path.join(DATEN, "D1_*.csv"))):
    saison = pfad[-8:-4]
    with open(pfad, encoding="utf-8-sig", errors="replace") as f:
        for z in csv.DictReader(f):
            if not z.get("FTHG") or not z.get("HTHG") or not z.get("HR"):
                continue
            def quote(*namen):
                for n in namen:
                    try:
                        v = float(z.get(n) or "")
                        if v > 1: return v
                    except ValueError:
                        pass
                return None
            oh, od, oa = quote("PSCH", "AvgCH", "B365H"), quote("PSCD", "AvgCD", "B365D"), quote("PSCA", "AvgCA", "B365A")
            spiele.append(dict(saison=saison, heim=z["HomeTeam"], gast=z["AwayTeam"],
                h=int(z["FTHG"]), a=int(z["FTAG"]), hh=int(z["HTHG"]), ha=int(z["HTAG"]),
                hr=int(z["HR"]), ar=int(z["AR"]), oh=oh, od=od, oa=oa))

n = len(spiele)
print(f"Spiele: {n}")
def anteil(bed): return sum(1 for s in spiele if bed(s)) / n

print("\n== Ergebnisverteilung ==")
tore = sum(s["h"] + s["a"] for s in spiele)
print(f"Tore/Spiel {tore/n:.3f}")
print(f"Heimsieg {anteil(lambda s: s['h']>s['a']):.3f}  Remis {anteil(lambda s: s['h']==s['a']):.3f}  AuswÃ¤rtssieg {anteil(lambda s: s['h']<s['a']):.3f}")
for e in [(0,0),(1,0),(0,1),(1,1),(2,1),(1,2),(2,0),(0,2),(2,2)]:
    print(f"  {e[0]}:{e[1]} {anteil(lambda s, e=e: (s['h'],s['a'])==e):.4f}")
print(f"1:0 oder 0:1 {anteil(lambda s: (s['h'],s['a']) in [(1,0),(0,1)]):.4f}")
zu_null = sum((s["a"]==0) + (s["h"]==0) for s in spiele) / (2*n)
print(f"Zu-null je Team-Spiel {zu_null:.3f}")
print(f"Spiele mit Zu-null (mind. ein Team) {anteil(lambda s: s['h']==0 or s['a']==0):.3f}")
for k in [5,6,7]:
    print(f">= {k} Tore {anteil(lambda s, k=k: s['h']+s['a']>=k):.4f}")
for k in [3,4,5]:
    print(f"Differenz >= {k} {anteil(lambda s, k=k: abs(s['h']-s['a'])>=k):.4f}")
torverteilung = Counter(s["h"]+s["a"] for s in spiele)
print("Tore je Spiel Verteilung", {k: round(v/n,4) for k,v in sorted(torverteilung.items())})

print("\n== Halbzeit ==")
ht = sum(s["hh"]+s["ha"] for s in spiele)
print(f"Anteil Tore 1. HZ {ht/tore:.3f}")
print(f"0:0 zur Pause {anteil(lambda s: s['hh']==0 and s['ha']==0):.3f}")

def ausgang_fuehrender(s, vorsprung_ht):
    # liefert (fuehrender ist heim?, ausgang aus Sicht des FÃ¼hrenden: 1/0/-1)
    d = s["hh"] - s["ha"]
    if abs(d) != vorsprung_ht: return None
    fh = d > 0
    end = (s["h"] - s["a"]) * (1 if fh else -1)
    return fh, (end > 0) - (end < 0)

def p_impl(s):
    if not s["oh"]: return None
    r = [1/s["oh"], 1/s["od"], 1/s["oa"]]; t = sum(r)
    return [x/t for x in r]

print("FÃ¼hrung zur Halbzeit -> Endausgang (Sieg/Remis/Niederlage des FÃ¼hrenden)")
for v in [1,2,3]:
    for filt_name, filt in [("alle", lambda s, fh: True),
                            ("FÃ¼hrender ist Favorit", lambda s, fh: p_impl(s) and (p_impl(s)[0] > p_impl(s)[2]) == fh),
                            ("FÃ¼hrender ist AuÃŸenseiter", lambda s, fh: p_impl(s) and (p_impl(s)[0] > p_impl(s)[2]) != fh)]:
        c = Counter();
        for s in spiele:
            r = ausgang_fuehrender(s, v)
            if r and filt(s, r[0]): c[r[1]] += 1
        m = sum(c.values())
        if m: print(f"  +{v} {filt_name:26s} n={m:4d}  S {c[1]/m:.3f}  U {c[0]/m:.3f}  N {c[-1]/m:.3f}")

print("\n== Rote Karten ==")
rote = sum(s["hr"]+s["ar"] for s in spiele)
print(f"Rote je Team-Spiel {rote/(2*n):.4f}; Spiele mit mind. 1 Rot {anteil(lambda s: s['hr']+s['ar']>0):.3f}")
c = Counter(); erwartet = 0.0; ep = 0.0; m = 0
for s in spiele:
    if s["hr"] == s["ar"]: continue
    ueberzahl_heim = s["hr"] < s["ar"]
    d = (s["h"]-s["a"]) * (1 if ueberzahl_heim else -1)
    c[(d>0)-(d<0)] += 1; m += 1
    p = p_impl(s)
    if p: erwartet += p[0] if ueberzahl_heim else p[2]; ep += 1
print(f"Spiele mit Rot-Differenz n={m}: Ãœberzahlteam S {c[1]/m:.3f} U {c[0]/m:.3f} N {c[-1]/m:.3f}; Siegchance laut Quote vor dem Spiel {erwartet/ep:.3f}")

print("\n== Quoten-Kalibrierung (Schlussquote, normiert) ==")
klassen = [(0.33,0.40),(0.40,0.50),(0.50,0.60),(0.60,0.70),(0.70,0.80),(0.80,1.0)]
for lo, hi in klassen:
    g = [(s, p_impl(s)) for s in spiele if p_impl(s)]
    g = [(s,p) for s,p in g if lo <= max(p[0],p[2]) < hi]
    if not g: continue
    sieg = sum(1 for s,p in g if (s["h"]>s["a"]) == (p[0]>p[2]) and s["h"]!=s["a"])
    rem = sum(1 for s,p in g if s["h"]==s["a"])
    mp = sum(max(p[0],p[2]) for s,p in g)/len(g); md = sum(p[1] for s,p in g)/len(g)
    print(f"  Favorit {lo:.2f}-{hi:.2f} n={len(g):4d} Anteil {len(g)/n:.3f}  p_Fav {mp:.3f} -> Sieg {sieg/len(g):.3f} | p_Remis {md:.3f} -> Remis {rem/len(g):.3f}")

print("\n== Dixon-Coles: Poisson je Saison (Maher), rho per ML ==")
def fit(saison_spiele):
    teams = sorted({s["heim"] for s in saison_spiele} | {s["gast"] for s in saison_spiele})
    att = {t:1.0 for t in teams}; deff = {t:1.0 for t in teams}; heim = 1.3
    for _ in range(200):
        for t in teams:
            g = sum(s["h"] for s in saison_spiele if s["heim"]==t) + sum(s["a"] for s in saison_spiele if s["gast"]==t)
            e = sum(deff[s["gast"]]*heim for s in saison_spiele if s["heim"]==t) + sum(deff[s["heim"]] for s in saison_spiele if s["gast"]==t)
            att[t] = g/e
        for t in teams:
            g = sum(s["a"] for s in saison_spiele if s["heim"]==t) + sum(s["h"] for s in saison_spiele if s["gast"]==t)
            e = sum(att[s["gast"]] for s in saison_spiele if s["heim"]==t) + sum(att[s["heim"]]*heim for s in saison_spiele if s["gast"]==t)
            deff[t] = g/e
        heim = sum(s["h"] for s in saison_spiele) / sum(att[s["heim"]]*deff[s["gast"]] for s in saison_spiele)
    return [(att[s["heim"]]*deff[s["gast"]]*heim, att[s["gast"]]*deff[s["heim"]], s) for s in saison_spiele]

lm = []
for saison in sorted({s["saison"] for s in spiele}):
    lm += fit([s for s in spiele if s["saison"]==saison])
def pois(k, l): return math.exp(-l)*l**k/math.factorial(k)
def tau(x,y,l,m,rho):
    if (x,y)==(0,0): return 1-l*m*rho
    if (x,y)==(0,1): return 1+l*rho
    if (x,y)==(1,0): return 1+m*rho
    if (x,y)==(1,1): return 1-rho
    return 1.0
for e in [(0,0),(1,0),(0,1),(1,1)]:
    erw = sum(pois(e[0],l)*pois(e[1],m) for l,m,s in lm)
    beob = sum(1 for l,m,s in lm if (s["h"],s["a"])==e)
    print(f"  {e[0]}:{e[1]} beobachtet {beob} unabhÃ¤ngig erwartet {erw:.1f} VerhÃ¤ltnis {beob/erw:.3f}")
erw_rem = sum(sum(pois(k,l)*pois(k,m) for k in range(12)) for l,m,s in lm)
print(f"  Remis beobachtet {sum(1 for l,m,s in lm if s['h']==s['a'])} unabhÃ¤ngig erwartet {erw_rem:.1f}")
best = None
for i in range(-300, 101):
    rho = i/1000
    ll = sum(math.log(max(1e-12, tau(s["h"],s["a"],l,m,rho))) for l,m,s in lm)
    if best is None or ll > best[1]: best = (rho, ll)
print(f"  rho (ML, Parameter fest) = {best[0]:.3f}")
# grobe Standardabweichung Ã¼ber KrÃ¼mmung
r0 = best[0]; h = 0.005
f = lambda rho: sum(math.log(max(1e-12, tau(s["h"],s["a"],l,m,rho))) for l,m,s in lm)
k2 = (f(r0+h) - 2*f(r0) + f(r0-h)) / h**2
print(f"  Standardfehler rho ~ {math.sqrt(-1/k2):.3f}")

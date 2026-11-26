---
name: issue-abschluss
description: Abschluss eines Issues: /code-review high, Definition-of-Done-Block für den Schlusskommentar, Folge-Issues, Memory, Pause. Nutzen, bevor ein Issue geschlossen oder gepusht wird.
---

# Issue-Abschluss

1. **Vor dem Push:** `/code-review high` auf die noch nicht gepushten Änderungen; Funde beheben oder mit Grund überspringen. Reine Doku-Pushes sind ausgenommen.
2. **Push erst ohne offene Arbeit** (Hook blockiert bei uncommitteten Änderungen). Auf den grünen Komplettlauf der GUI-Tests warten.
3. **Dokumentation am Fundort nachziehen:** die `CLAUDE.md` am Ort der geänderten Kernlogik; die Root-`CLAUDE.md` nur bei einer neuen **schichtübergreifenden** Regel (dann eine Zeile + Verweis). Messreihen bleiben im Issue. Eine neue Unter-`CLAUDE.md` nur für Ordner mit ≥ ~1 KB nicht offensichtlicher Regeln, nicht auf Vorrat. Außerdem GUI-Abdeckung (#529), Meilenstein-Beschreibung, Seed.
4. **Folge-Themen** als eigene Issues anlegen (Präfix, Labels, Phase), nicht nur als Satz im Kommentar.
5. **Schlusskommentar** (`gh issue close <nr> --comment …`): Commit-Hashes, Abweichungen vom Issue-Text, Gründe nicht offensichtlicher Entscheidungen, verwendetes Modell (und Wechsel), dazu den DoD-Block:

```
**Definition of Done**
1. Kriterien — erfüllt/entfällt/begründet: …
2. Build und Unit-Tests grün, keine offenen Modelländerungen (Commit-Hook)
3. Code-Review (/code-review high) — Funde: …
4. GUI-Tests — Komplettlauf grün; neues UI-Verhalten mit Szenario oder als Lücke genannt
5. Manuelle Lücken — benannt und vom Nutzer bestätigt, oder keine
6. Code-Qualität — Clean-Code-Regeln, keine neuen Analyzer-Warnungen; jede neue RibbonAktion mit verifiziertem IconGlyph
7. Dokumentation — am Fundort des Codes nachgezogen; Root nur bei neuer schichtübergreifender Regel
8. Folge-Issues — als eigene Issues angelegt: …
9. Schlusskommentar — Hashes, Abweichungen, Begründungen, dieser Block
```

Jeder Punkt: *erfüllt*, *entfällt* oder mit Grund ausgelassen. Ist einer offen, wird nicht geschlossen.
6. **Memory aktualisieren** (`project_aktueller_stand.md`, `MEMORY.md`, betroffene Einzeldateien), **dann** dem Nutzer kurz zusammenfassen, was erledigt wurde und was ansteht, und **auf Feedback warten** — nicht direkt das nächste Issue beginnen.

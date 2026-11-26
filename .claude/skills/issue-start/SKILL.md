---
name: issue-start
description: Start eines Issues ("mach #…", "weiter mit #…", "#123"): Modell- und Effort-Empfehlung ansagen und warten, Grobplan, Sperr-/Aktualitätsprüfung, Kriterien anlegen. Zuerst nutzen, bevor Code gelesen oder geändert wird.
---

# Issue-Start

Auslöser: der Nutzer nennt ein Issue. Reihenfolge einhalten.

1. **Issue lesen** (`gh issue view <nr>`), noch keinen Code.
2. **Erste Nachricht, ungefragt:** empfohlenes Modell mit Einzeiler-Begründung (aus `## Grobplan`) + Effort + Wechselanweisung ("jetzt Opus + `/clear`, Start mit …"). Dann **warten** — kein Code, kein `Edit`, kein sed vor der Bestätigung. Der Nutzer stellt das Modell selbst um.
   - Modelle: **Sonnet** (`simple`, Doku/Chore, Dependabot-Minor, Seed-Nachträge, Muster-Kopie), **Opus plant → Sonnet setzt um** (`medium`/`hard` mit planbarem Weg; Opus schreibt einen eigenständigen `## Plan` ins Issue), **Opus** (Bug mit unklarer Ursache, `thema: code-qualitaet`, Simulations-/Rundenlogik, Turnierbaum/EF-Mehrphasen, Konzept-/Designfragen, Major-Updates).
   - **Effort:** Standard **Medium**; **High** nur bei `aufwand: hard` oder einer roten Messung zur Deutung.
   - Fehlt `## Grobplan`: erst ergänzen (mit Empfehlung), dann ansagen.
3. **Jeder spätere Wechsel wird vorab angesagt** (Plan fertig → Sonnet; Sonnet hängt zweimal oder rote Messung → sofort Opus; Opus-Frage beantwortet → zurück zu Sonnet), immer mit Angabe, ob mit oder ohne `/clear`. Läuft die Arbeit im richtigen Modell weiter, keine Modellfrage. Das verwendete Modell kommt in den Schlusskommentar.
4. **Sperr-/Aktualitätsprüfung:** `blocked-by` prüfen (Blocker auch dann melden, wenn er irrelevant scheint; hart vs. weich benennen); bei älteren Issues aktuellen Code gegen die Beschreibung prüfen, bei Abweichung mit dem Nutzer abstimmen. Prio-Label muss gesetzt sein, sonst nicht bearbeiten.
5. **Offene Dependabot-PRs** ansehen (`gh pr list --author app/dependabot`), Ablauf: Skill `dependabot-prs`.
6. **`## Kriterien`** in den Issue-Body schreiben, **bevor** Code entsteht (K1, K2 … mit Test je Kriterium; bei `hard` zusammen mit dem Plan-Recap). Inhalt je Typ: Tabelle unten. Bei `hard` zusätzlich **Plan-Recap** vorlegen (Konzept, Schichten/Dateien, Reihenfolge) und bei sichtbarer UI-Änderung ein klickbares Mockup; erst nach Bestätigung codieren.
7. Beim Anlegen neuer Issues: Titel-Präfix `Bug:`/`Feature:`/`Verbesserung:`, Labels Typ + Aufwand (selbst eingeschätzt) + Prio, Phase (Meilenstein, außer `prio: parked`), `## Grobplan` mit `Modell-Empfehlung`. Issue-Nummern nie raten — erst `gh issue create`.

## Kriterien je Issue-Typ

| Issue-Typ | Kriterien |
|---|---|
| Feature mit UI | Given/When/Then inkl. Zustände *davor/dazwischen* (Platzhalter, noch nicht ausgelost, Altdaten, leere Runden); Test je Kriterium, Unit-Tests lokal rot belegen, GUI-Gegenprobe auf Wegwerf-Branch `ci/<nr>-gegenprobe` (#591) |
| Feature ohne UI / Verbesserung | Given/When/Then, Unit-Test je Kriterium |
| Bug | Reproduktion (given/when → falsches Ergebnis), erwartetes Verhalten, Ursache belegt (keine Kandidaten-Fixes), Regressionstest vor dem Fix rot |
| Refactoring | Zielstruktur benannt; Verhalten unverändert (bestehende Tests grün ohne Änderung); Messwerte vorher/nachher (Komplexität, Zeilen) |
| Performance | Messwert vorher, Zielwert, Messwert nachher (im Issue) |
| Doku / Chore | was danach wo liegt |

Bei `simple` reichen ein bis zwei Zeilen. Ein während der Arbeit gefundenes Kriterium wird nachgetragen und als *nachgetragen* markiert.

---
name: issue-start
description: Start eines Issues ("mach #…", "weiter mit #…", "#123"): Modell- und Effort-Empfehlung ansagen und warten, Grobplan, Sperr-/Aktualitätsprüfung, Kriterien anlegen. Zuerst nutzen, bevor Code gelesen oder geändert wird.
---

**Before reading code or editing anything for an issue** — whenever the user names an issue ("#682", "mach #…",
"weiter mit …") — the **first message** states, unasked: (1) the recommended model (Sonnet / Opus plans + Sonnet
implements / Opus) with a one-line reason, taken from the issue's `## Grobplan`; (2) the switch instruction
("jetzt Opus + `/clear`, Start mit …"). The user switches the model themselves — never ask them to remind you.
Then wait; no code, no `Edit`, no sed before the user has confirmed or switched. The same announcement is due at
every later hand-over (plan done → Sonnet; Sonnet stuck twice or a red measurement to interpret → Opus at once;
Opus question answered → back to Sonnet) and the model used goes into the closing comment. Details and the model
selection table: memory `feedback_modellwahl_je_issue`. An issue without a `## Grobplan` gets one (with the
recommendation) before the start.



### Issue criteria

Written into the issue body as a section `## Kriterien` **before the code** (after the blocking/staleness check;
for effort `hard` together with the plan recap). Numbered K1, K2 …; behind each one, in brackets, the test that pins
it down (unit test class or GUI scenario) — or why there is none. Effort `simple`: one or two lines are enough.
A criterion found during the work is added and marked as *nachgetragen*.

| Issue type | Criteria |
|---|---|
| Feature with UI | Given/When/Then, including the states *before/in between* (placeholders, not yet drawn, old data, empty rounds); test per criterion, unit tests shown red locally first, GUI counter-check on a throwaway `ci/<nr>-gegenprobe` branch (#591) |
| Feature without UI / improvement | Given/When/Then, unit test per criterion |
| Bug | reproduction (given/when → wrong result), expected behaviour, cause proven (no candidate fixes), regression test shown red before the fix |
| Refactoring | target structure named; behaviour unchanged (existing tests green without modification); measured values before/after (complexity, lines) |
| Performance | measured value before, target value, measured value after (in the issue) |
| Documentation / chore | what lies where afterwards |


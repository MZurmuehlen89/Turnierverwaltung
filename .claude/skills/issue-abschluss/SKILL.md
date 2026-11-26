---
name: issue-abschluss
description: Abschluss eines Issues: /code-review high, Definition-of-Done-Block für den Schlusskommentar, Folge-Issues, Memory, Pause. Nutzen, bevor ein Issue geschlossen oder gepusht wird.
---

## Definition of Done (#665)

Two levels: the **issue criteria** say whether the problem is solved (different for every issue), the **general
DoD** says whether the work is properly finished (the same for every issue). An issue is closed only when both hold.

### General DoD

Checked off in the closing comment as a block `**Definition of Done**`, each point *erfüllt*, *entfällt* or skipped
with a reason:

1. **Criteria** — every issue criterion is met, each with its test (or the stated reason there is none).
2. **Build and unit tests** green, no pending model changes (enforced by the commit hook).
3. **Code review** — `/code-review high` on the unpushed changes, findings fixed or skipped with a reason
   (documentation-only pushes excepted).
4. **GUI tests** — the full run on the push/PR is green; new UI behaviour has a GUI scenario or is listed as a gap.
5. **Manual gaps** — whatever the GUI tests cannot see (visuals, paths without scenario, real database) has been
   named and confirmed by the user, or there is none.
6. **Code quality** — Clean Code rules above followed in touched code, no new analyzer warnings there; every new
   `RibbonAktion` has a verified `IconGlyph`.
7. **Documentation** — affected `CLAUDE.md` files, GUI coverage (#529), milestone description and seed checked and
   updated.
8. **Follow-ups** — every known follow-up topic exists as its own issue (prefix, labels, phase), not only as a
   sentence in the comment.
9. **Closing comment** — commit hashes, deviations from the issue text, reasons for non-obvious decisions, this
   DoD block.


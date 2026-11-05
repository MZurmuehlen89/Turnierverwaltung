using Fussballturnierverwaltung.Application.Abstraktionen;
using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Application.Turniere.Eingaben;
using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Validierung;

public class TurnierValidator
{
    /// <summary>Validiert die Runden-Eingaben vor dem Anlegen einer TurnierInstanz. Feldbezug der Meldungen (#601):
    /// <see cref="Feldfehler.Zeile"/> = Index der Runde, <see cref="Feldfehler.Feld"/> = Eigenschaft von
    /// <see cref="TurnierRundeEingabe"/> bzw. bei Tabellenbereichen von <see cref="TabellenBereichEingabe"/> mit dem
    /// Bereich als <see cref="Feldfehler.Unterzeile"/>. Die Rundenkette ist eine Kreuzprüfung (nur Zeile).</summary>
    public ValidationErgebnis RundenValidieren(IReadOnlyList<TurnierRundeEingabe> runden)
    {
        if (runden.Count == 0)
            return ValidationErgebnis.MitFehlern("Ein Turnier muss mindestens eine Runde haben.");

        Feldfehler[] fehler =
        [
            .. runden.SelectMany(RundeValidieren),
            .. RundenuebergaengePruefen(runden),
            .. FinalePruefen(runden),
        ];

        return ValidationErgebnis.Aus(fehler);
    }

    // ── Einzelrunde ─────────────────────────────────────────────────────────

    private static IEnumerable<Feldfehler> RundeValidieren(TurnierRundeEingabe r, int index) =>
        [
            .. AnzahlenPruefen(r, index),
            .. KoAnzahlenPruefen(r, index),
            .. ZusatzfelderPruefen(r, index),
            .. TabellenBereicheValidieren(r, index),
        ];

    private static IEnumerable<Feldfehler> AnzahlenPruefen(TurnierRundeEingabe r, int index)
    {
        if (string.IsNullOrWhiteSpace(r.Name))
            yield return new("Runde muss einen Namen haben.", nameof(r.Name), index);

        if (r.EingangsMannschaftsanzahl < 2)
            yield return new($"Runde '{r.Name}': EingangsMannschaftsanzahl muss mindestens 2 betragen (war: {r.EingangsMannschaftsanzahl}).",
                nameof(r.EingangsMannschaftsanzahl), index);

        if (r.AusgangsMannschaftsanzahl < 1)
            yield return new($"Runde '{r.Name}': AusgangsMannschaftsanzahl muss mindestens 1 betragen.",
                nameof(r.AusgangsMannschaftsanzahl), index);

        // Liga-Runden können mehr Ausgangsteams als erwartet haben (alle spielen alle). K.-o.-Rundenarten
        // prüft KoAnzahlenPruefen (dort auch der Split-Sonderfall, Issue #334).
        if (r.Rundenart != Rundenart.Liga && !IstKoRundenart(r.Rundenart) &&
            r.AusgangsMannschaftsanzahl >= r.EingangsMannschaftsanzahl)
            yield return new($"Runde '{r.Name}': Ausgangsteams ({r.AusgangsMannschaftsanzahl}) " +
                             $"müssen kleiner als Eingangsteams ({r.EingangsMannschaftsanzahl}) sein.",
                nameof(r.AusgangsMannschaftsanzahl), index);
    }

    /// <summary>KoRundeGenerator verlangt zwingend eine gerade Eingangszahl und bildet strikt n/2-Paarungen — jedes
    /// Spiel liefert genau einen Sieger und einen Verlierer (Issue #182). AusgangsMannschaftsanzahl ist entweder die
    /// Hälfte (Normalfall: nur Sieger steigen auf, Verlierer scheiden aus) oder die volle Eingangszahl (Sonderfall:
    /// Sieger UND Verlierer steigen auf, aber in zwei getrennte Zielrunden — z.B. Halbfinale → Finale + Spiel um
    /// Platz 3 über separate Übergangsregeln je Spielnummer/Platzierung, Issue #334). Ob ein deklarierter Split
    /// tatsächlich korrekt auf Zielrunden verteilt ist, prüft nicht dieser Validator, sondern die
    /// Ganzgraph-Summenprüfung in UebergangsregelValidator.</summary>
    private static IEnumerable<Feldfehler> KoAnzahlenPruefen(TurnierRundeEingabe r, int index)
    {
        if (!IstKoRundenart(r.Rundenart))
            yield break;

        int eingang = r.EingangsMannschaftsanzahl;
        if (eingang % 2 != 0)
            yield return new($"Runde '{r.Name}': K.-o.-Rundenarten benötigen eine gerade Eingangsmannschaftsanzahl (war: {eingang}).",
                nameof(r.EingangsMannschaftsanzahl), index);
        else if (r.AusgangsMannschaftsanzahl != eingang / 2 && r.AusgangsMannschaftsanzahl != eingang)
            yield return new($"Runde '{r.Name}': AusgangsMannschaftsanzahl muss bei K.-o.-Rundenarten entweder der Hälfte " +
                             $"der Eingangsmannschaftsanzahl entsprechen ({eingang / 2}, nur Sieger steigen auf) " +
                             $"oder der vollen Eingangsmannschaftsanzahl ({eingang}, Sieger und Verlierer steigen " +
                             $"in getrennte Zielrunden auf) — war: {r.AusgangsMannschaftsanzahl}.",
                nameof(r.AusgangsMannschaftsanzahl), index);
    }

    /// <summary>Auswärtstorregel, Überraschungsfaktor, Gruppenanzahl und feste Gruppenköpfe. Ein Widerspruch der
    /// Gruppenköpfe betrifft zwei Felder und die Gruppenanzahl – das Formular zeigt ihn selbst unter den Feldern.</summary>
    private static IEnumerable<Feldfehler> ZusatzfelderPruefen(TurnierRundeEingabe r, int index)
    {
        if (r.AuswaertstorRegelAktiv && !r.MitRueckspiel)
            yield return new($"Runde '{r.Name}': Auswärtstorregel ist aktiv, obwohl keine Hin- und Rückspiele gespielt werden.",
                nameof(r.AuswaertstorRegelAktiv), index);

        if (r.Ueberraschungsfaktor is < 0 or > 100)
            yield return new($"Runde '{r.Name}': Überraschungsfaktor muss zwischen 0 und 100 % liegen (war: {r.Ueberraschungsfaktor}).",
                nameof(r.Ueberraschungsfaktor), index);

        if (r.Rundenart == Rundenart.Gruppenphase && r.GruppenAnzahl is not >= 2)
            yield return new($"Runde '{r.Name}': Gruppenphase erfordert mindestens 2 Gruppen (GruppenAnzahl war: {r.GruppenAnzahl ?? 0}).",
                nameof(r.GruppenAnzahl), index);

        if (Gruppenkoepfe.Widerspruch(r.GastgeberGruppen ?? [], r.TitelverteidigerGruppe, r.GruppenAnzahl) is { } kopfFehler)
            yield return new($"Runde '{r.Name}': {kopfFehler}", Zeile: index);
    }

    // ── Rundenkette ─────────────────────────────────────────────────────────

    /// <summary>Ausgang der Vorrunde muss Eingang der Runde sein — außer der Eingang kommt über Übergangsregeln
    /// (separat geprüft, siehe UebergangsregelValidator).</summary>
    private static IEnumerable<Feldfehler> RundenuebergaengePruefen(IReadOnlyList<TurnierRundeEingabe> runden)
    {
        for (int i = 1; i < runden.Count; i++)
        {
            var runde = runden[i];
            if (runde.EingangUeberUebergangsregeln)
                continue;

            // K.o.-Geschwisterpaar (Finale + Spiel um Platz 3) speist sich beide aus derselben Vorrunde —
            // die zweite der beiden Runden wird nicht gegen die erste, sondern gegen deren gemeinsame
            // Vorrunde geprüft (Issue #178).
            int vorrundeIndex = i >= 2 && IstKoGeschwisterPaar(runde.Rundenart, runden[i - 1].Rundenart)
                ? i - 2
                : i - 1;
            var vorrunde = runden[vorrundeIndex];

            if (runde.EingangsMannschaftsanzahl != vorrunde.AusgangsMannschaftsanzahl)
                yield return new($"Runde {i + 1} ('{runde.Name}') erwartet {runde.EingangsMannschaftsanzahl} Mannschaften, " +
                                 $"Runde {vorrundeIndex + 1} ('{vorrunde.Name}') liefert aber {vorrunde.AusgangsMannschaftsanzahl}.",
                    Zeile: i);
        }
    }

    private static IEnumerable<Feldfehler> FinalePruefen(IReadOnlyList<TurnierRundeEingabe> runden)
    {
        int index = runden.ToList().FindIndex(r => r.Rundenart == Rundenart.Finale);
        if (index >= 0 && runden[index] is { AusgangsMannschaftsanzahl: not 1 } finale)
            yield return new($"Das Finale ('{finale.Name}') muss genau 1 Sieger liefern (AusgangsMannschaftsanzahl war: {finale.AusgangsMannschaftsanzahl}).",
                nameof(finale.AusgangsMannschaftsanzahl), index);
    }

    /// <summary>Prüft die Tabellenbereiche einer Runde (Issue #420): Bezeichnung, Platz von–bis innerhalb der
    /// Tabellengröße (bei der Gruppenphase die größte Gruppe), keine Überschneidung. <paramref name="rundenIndex"/>
    /// wird zur <see cref="Feldfehler.Zeile"/>, die Position des Bereichs in der Eingabe zur Unterzeile (#601); eine
    /// Überschneidung markiert „von“ des hinteren Bereichs.</summary>
    public static IEnumerable<Feldfehler> TabellenBereicheValidieren(TurnierRundeEingabe r, int rundenIndex = 0)
    {
        var bereiche = (r.TabellenBereiche ?? [])
            .Select((Bereich, Position) => (Bereich, Position))
            .OrderBy(x => x.Bereich.PlatzVon)
            .ToList();
        int tabellengroesse = r.Rundenart == Rundenart.Gruppenphase && r.GruppenAnzahl is > 0 and var gruppen
            ? (r.EingangsMannschaftsanzahl + gruppen - 1) / gruppen
            : r.EingangsMannschaftsanzahl;

        foreach (var (b, position) in bereiche)
        {
            string name = string.IsNullOrWhiteSpace(b.Bezeichnung) ? $"Platz {b.PlatzVon}–{b.PlatzBis}" : b.Bezeichnung.Trim();
            if (string.IsNullOrWhiteSpace(b.Bezeichnung))
                yield return new($"Runde '{r.Name}': Tabellenbereich {name} braucht eine Bezeichnung.",
                    nameof(b.Bezeichnung), rundenIndex, position);
            if (b.PlatzVon < 1 || b.PlatzBis < b.PlatzVon || b.PlatzBis > tabellengroesse)
                yield return new($"Runde '{r.Name}': Tabellenbereich „{name}“ muss zwischen Platz 1 und {tabellengroesse} " +
                                 $"liegen, „von“ höchstens „bis“ (war: {b.PlatzVon}–{b.PlatzBis}).",
                    b.PlatzVon < 1 ? nameof(b.PlatzVon) : nameof(b.PlatzBis), rundenIndex, position);
        }

        for (int i = 1; i < bereiche.Count; i++)
        {
            var vorne = bereiche[i - 1].Bereich;
            var (hinten, position) = bereiche[i];
            if (hinten.PlatzVon <= vorne.PlatzBis)
                yield return new($"Runde '{r.Name}': Tabellenbereiche „{vorne.Bezeichnung.Trim()}“ und " +
                                 $"„{hinten.Bezeichnung.Trim()}“ überschneiden sich.",
                    nameof(hinten.PlatzVon), rundenIndex, position);
        }
    }

    /// <summary>Finale und Spiel um Platz 3 speisen sich strukturell immer aus derselben Vorrunde
    /// (Sieger- bzw. Verliereranzahl einer K.-o.-Runde sind immer gleich groß) — unabhängig davon,
    /// in welcher Reihenfolge sie in der Rundenliste stehen.</summary>
    private static bool IstKoGeschwisterPaar(Rundenart a, Rundenart b) =>
        (a == Rundenart.Finale && b == Rundenart.SpielUmPlatzDrei) ||
        (a == Rundenart.SpielUmPlatzDrei && b == Rundenart.Finale);

    /// <summary>Rundenarten, die von KoRundeGenerator bedient werden — strikte n/2-Paarung, ein Sieger pro Spiel.</summary>
    private static bool IstKoRundenart(Rundenart rundenart) =>
        rundenart is Rundenart.KoRunde or Rundenart.Finale or Rundenart.SpielUmPlatzDrei;

    /// <summary>Prüft ob eine Rundenart mit dem verknüpften Regelwerk kompatibel ist.</summary>
    public ValidationErgebnis RundeUndRegelwerkKompatibel(TurnierRundeEingabe runde, Regelwerk regelwerk)
    {
        var fehler = new List<string>();

        if (IstKoRundenart(runde.Rundenart) && !regelwerk.Entscheidungsverfahren.Any())
            fehler.Add($"K.-o.-Runde '{runde.Name}' benötigt einen Sieger, aber Regelwerk '{regelwerk.Name}' " +
                       $"hat kein Entscheidungsverfahren definiert (z.B. Verlängerung, Elfmeter).");

        return fehler.Count == 0 ? ValidationErgebnis.Ok() : ValidationErgebnis.MitFehlern([.. fehler]);
    }

    /// <summary>Validiert Basisdaten einer TurnierInstanz (Issue #294 — vereinheitlicht die frühere
    /// entitätsbasierte <c>TurnierInstanzValidieren</c> mit dem primitiv-basierten
    /// <c>TurnierVorlageValidator.BasisdatenValidieren</c>, da eine Vorlage jetzt dieselbe Entität ist).
    /// Primitiv-basiert, damit sie auch als Live-Vorschau vor dem Speichern (ViewModel) nutzbar ist. Feldbezug der
    /// Meldungen (#595) = Name der Eigenschaft an <see cref="TurnierInstanz"/>.</summary>
#pragma warning disable S107 // einfache Formularwerte statt Parameterobjekt, damit die Live-Vorschau sie ohne Umweg prüfen kann (#499)
    public ValidationErgebnis BasisdatenValidieren(
        string name, string saisonOderJahr, int? zielMannschaftsanzahl,
        DateOnly? startdatum, DateOnly? enddatum, byte[]? logo = null)
#pragma warning restore S107
    {
        var fehler = new List<Feldfehler>();

        if (string.IsNullOrWhiteSpace(name))
            fehler.Add(new("Turniername darf nicht leer sein.", nameof(TurnierInstanz.Name)));

        if (string.IsNullOrWhiteSpace(saisonOderJahr))
            fehler.Add(new("Saison/Jahr darf nicht leer sein.", nameof(TurnierInstanz.SaisonOderJahr)));

        if (zielMannschaftsanzahl is not null && zielMannschaftsanzahl < 2)
            fehler.Add(new($"Mannschaftsanzahl muss mindestens 2 betragen (war: {zielMannschaftsanzahl}).",
                nameof(TurnierInstanz.ZielMannschaftsanzahl)));

        // Markiert wird nur das Endjahr – das Feld, das man in der Regel korrigiert (#595).
        if (startdatum.HasValue && enddatum.HasValue && enddatum < startdatum)
            fehler.Add(new("Endjahr darf nicht vor dem Startjahr liegen.", nameof(TurnierInstanz.Enddatum)));

        BildValidierung.GroessePruefen(logo, nameof(TurnierInstanz.Logo), fehler);

        return ValidationErgebnis.Aus(fehler);
    }

    /// <summary>Issue #367: Vorlagen tragen den Formatzusatz nicht mehr im Namen ("FIFA WM"), sondern in
    /// <see cref="TurnierInstanz.Variante"/> ("32 Teams · 1998–2022") — Name + Variante zusammen müssen daher unter
    /// allen Vorlagen eindeutig sein, sonst wären sie in Liste und Auswahl nicht unterscheidbar. Echte Turniere
    /// sind nicht betroffen (dort unterscheidet die Saison).</summary>
    public static ValidationErgebnis VorlagenEindeutigkeitValidieren(
        string name, string variante, int? eigeneId, IEnumerable<TurnierInstanz> vorhandeneVorlagen)
    {
        var n = name.Trim();
        var v = variante.Trim();
        bool doppelt = vorhandeneVorlagen.Any(x => x.Id != eigeneId
            && string.Equals(x.Name.Trim(), n, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.Variante.Trim(), v, StringComparison.OrdinalIgnoreCase));
        if (!doppelt) return ValidationErgebnis.Ok();

        var varianteText = v.Length == 0 ? "ohne Variante" : $"mit der Variante '{v}'";
        return ValidationErgebnis.MitFehlern(
            $"Es gibt bereits eine Vorlage '{n}' {varianteText} — Name und Variante einer Vorlage müssen zusammen eindeutig sein.");
    }

    /// <summary>Issue #294: ein Statuswechsel weg von <see cref="TurnierStatus.Vorlage"/> erfordert ein
    /// Regelwerk je Runde (eine Vorlage darf ohne Regelwerk existieren, ein echtes Turnier nicht). Ein
    /// Standard-Regelwerk der Instanz deckt Runden ohne eigenes Regelwerk ab (AD-026).</summary>
    public ValidationErgebnis RegelwerkPflichtValidieren(
        IReadOnlyList<TurnierRunde> runden, TurnierStatus neuerStatus, int? standardRegelwerkId = null)
    {
        if (neuerStatus == TurnierStatus.Vorlage || standardRegelwerkId is not null)
            return ValidationErgebnis.Ok();

        var fehler = runden
            .Where(r => r.RegelwerkId is null)
            .Select(r => $"Runde '{r.Name}' hat kein Regelwerk zugewiesen.")
            .ToList();

        return fehler.Count == 0 ? ValidationErgebnis.Ok() : ValidationErgebnis.MitFehlern([.. fehler]);
    }
}

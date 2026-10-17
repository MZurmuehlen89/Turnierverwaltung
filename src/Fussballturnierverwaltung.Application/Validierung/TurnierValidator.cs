using Fussballturnierverwaltung.Application.Abstraktionen;
using Fussballturnierverwaltung.Application.Turniere.Eingaben;
using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Validierung;

public class TurnierValidator
{
    /// <summary>Validiert die Runden-Eingaben vor dem Anlegen einer TurnierInstanz.</summary>
    public ValidationErgebnis RundenValidieren(IReadOnlyList<TurnierRundeEingabe> runden)
    {
        var fehler = new List<string>();

        if (runden.Count == 0)
        {
            fehler.Add("Ein Turnier muss mindestens eine Runde haben.");
            return ValidationErgebnis.MitFehlern([.. fehler]);
        }

        foreach (var r in runden)
        {
            if (string.IsNullOrWhiteSpace(r.Name))
                fehler.Add($"Runde muss einen Namen haben.");

            if (r.EingangsMannschaftsanzahl < 2)
                fehler.Add($"Runde '{r.Name}': EingangsMannschaftsanzahl muss mindestens 2 betragen (war: {r.EingangsMannschaftsanzahl}).");

            if (r.AusgangsMannschaftsanzahl < 1)
                fehler.Add($"Runde '{r.Name}': AusgangsMannschaftsanzahl muss mindestens 1 betragen.");

            // Liga-Runden können mehr Ausgangsteams als erwartet haben (alle spielen alle). K.-o.-Rundenarten
            // werden unten separat geprüft (dort auch der Split-Sonderfall, Issue #334).
            if (r.Rundenart != Rundenart.Liga && !IstKoRundenart(r.Rundenart) && r.AusgangsMannschaftsanzahl >= r.EingangsMannschaftsanzahl)
                fehler.Add($"Runde '{r.Name}': Ausgangsteams ({r.AusgangsMannschaftsanzahl}) " +
                           $"müssen kleiner als Eingangsteams ({r.EingangsMannschaftsanzahl}) sein.");

            // KoRundeGenerator verlangt zwingend eine gerade Eingangszahl und bildet strikt n/2-Paarungen —
            // jedes Spiel liefert genau einen Sieger und einen Verlierer (Issue #182). AusgangsMannschaftsanzahl
            // ist entweder die Hälfte (Normalfall: nur Sieger steigen auf, Verlierer scheiden aus) oder die
            // volle Eingangszahl (Sonderfall: Sieger UND Verlierer steigen auf, aber in zwei getrennte
            // Zielrunden — z.B. Halbfinale → Finale + Spiel um Platz 3 über separate Übergangsregeln je
            // Spielnummer/Platzierung, Issue #334). Ob ein deklarierter Split tatsächlich korrekt auf
            // Zielrunden verteilt ist, prüft nicht dieser Validator, sondern die Ganzgraph-Summenprüfung in
            // UebergangsregelValidator.
            if (IstKoRundenart(r.Rundenart))
            {
                if (r.EingangsMannschaftsanzahl % 2 != 0)
                    fehler.Add($"Runde '{r.Name}': K.-o.-Rundenarten benötigen eine gerade Eingangsmannschaftsanzahl (war: {r.EingangsMannschaftsanzahl}).");
                else if (r.AusgangsMannschaftsanzahl != r.EingangsMannschaftsanzahl / 2 &&
                         r.AusgangsMannschaftsanzahl != r.EingangsMannschaftsanzahl)
                    fehler.Add($"Runde '{r.Name}': AusgangsMannschaftsanzahl muss bei K.-o.-Rundenarten entweder der Hälfte " +
                               $"der Eingangsmannschaftsanzahl entsprechen ({r.EingangsMannschaftsanzahl / 2}, nur Sieger steigen auf) " +
                               $"oder der vollen Eingangsmannschaftsanzahl ({r.EingangsMannschaftsanzahl}, Sieger und Verlierer steigen " +
                               $"in getrennte Zielrunden auf) — war: {r.AusgangsMannschaftsanzahl}.");
            }

            if (r.AuswaertstorRegelAktiv && !r.MitRueckspiel)
                fehler.Add($"Runde '{r.Name}': Auswärtstorregel ist aktiv, obwohl keine Hin- und Rückspiele gespielt werden.");

            if (r.Ueberraschungsfaktor is < 0 or > 100)
                fehler.Add($"Runde '{r.Name}': Überraschungsfaktor muss zwischen 0 und 100 % liegen (war: {r.Ueberraschungsfaktor}).");

            if (r.Rundenart == Rundenart.Gruppenphase && (r.GruppenAnzahl == null || r.GruppenAnzahl < 2))
                fehler.Add($"Runde '{r.Name}': Gruppenphase erfordert mindestens 2 Gruppen (GruppenAnzahl war: {r.GruppenAnzahl ?? 0}).");

            fehler.AddRange(TabellenBereicheValidieren(r));
        }

        // Rundenübergänge: Ausgang[i] muss Eingang[i+1] sein
        for (int i = 1; i < runden.Count; i++)
        {
            var runde = runden[i];
            if (runde.EingangUeberUebergangsregeln)
                continue; // Eingang wird über Uebergangsregeln definiert (separat geprüft, siehe UebergangsregelValidator)

            // K.o.-Geschwisterpaar (Finale + Spiel um Platz 3) speist sich beide aus derselben Vorrunde —
            // die zweite der beiden Runden wird nicht gegen die erste, sondern gegen deren gemeinsame
            // Vorrunde geprüft (Issue #178).
            int vorrundeIndex = i >= 2 && IstKoGeschwisterPaar(runde.Rundenart, runden[i - 1].Rundenart)
                ? i - 2
                : i - 1;
            var vorrunde = runden[vorrundeIndex];

            if (runde.EingangsMannschaftsanzahl != vorrunde.AusgangsMannschaftsanzahl)
                fehler.Add($"Runde {i + 1} ('{runde.Name}') erwartet {runde.EingangsMannschaftsanzahl} Mannschaften, " +
                           $"Runde {vorrundeIndex + 1} ('{vorrunde.Name}') liefert aber {vorrunde.AusgangsMannschaftsanzahl}.");
        }

        // Finale muss genau 1 Sieger liefern
        var finale = runden.FirstOrDefault(r => r.Rundenart == Rundenart.Finale);
        if (finale is not null && finale.AusgangsMannschaftsanzahl != 1)
            fehler.Add($"Das Finale ('{finale.Name}') muss genau 1 Sieger liefern (AusgangsMannschaftsanzahl war: {finale.AusgangsMannschaftsanzahl}).");

        return fehler.Count == 0 ? ValidationErgebnis.Ok() : ValidationErgebnis.MitFehlern([.. fehler]);
    }

    /// <summary>Prüft die Tabellenbereiche einer Runde (Issue #420): Bezeichnung, Platz von–bis innerhalb der
    /// Tabellengröße (bei der Gruppenphase die größte Gruppe), keine Überschneidung. Auch für die Live-Prüfung im
    /// Formular.</summary>
    public static IEnumerable<string> TabellenBereicheValidieren(TurnierRundeEingabe r)
    {
        var bereiche = (r.TabellenBereiche ?? []).OrderBy(b => b.PlatzVon).ToList();
        int tabellengroesse = r.Rundenart == Rundenart.Gruppenphase && r.GruppenAnzahl is > 0 and var gruppen
            ? (r.EingangsMannschaftsanzahl + gruppen - 1) / gruppen
            : r.EingangsMannschaftsanzahl;

        foreach (var b in bereiche)
        {
            string name = string.IsNullOrWhiteSpace(b.Bezeichnung) ? $"Platz {b.PlatzVon}–{b.PlatzBis}" : b.Bezeichnung.Trim();
            if (string.IsNullOrWhiteSpace(b.Bezeichnung))
                yield return $"Runde '{r.Name}': Tabellenbereich {name} braucht eine Bezeichnung.";
            if (b.PlatzVon < 1 || b.PlatzBis < b.PlatzVon || b.PlatzBis > tabellengroesse)
                yield return $"Runde '{r.Name}': Tabellenbereich „{name}“ muss zwischen Platz 1 und {tabellengroesse} " +
                             $"liegen, „von“ höchstens „bis“ (war: {b.PlatzVon}–{b.PlatzBis}).";
        }

        for (int i = 1; i < bereiche.Count; i++)
        {
            if (bereiche[i].PlatzVon <= bereiche[i - 1].PlatzBis)
                yield return $"Runde '{r.Name}': Tabellenbereiche „{bereiche[i - 1].Bezeichnung.Trim()}“ und " +
                             $"„{bereiche[i].Bezeichnung.Trim()}“ überschneiden sich.";
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

        var brauchtEntscheidung = runde.Rundenart is
            Rundenart.KoRunde or Rundenart.Finale or Rundenart.SpielUmPlatzDrei;

        if (brauchtEntscheidung && !regelwerk.Entscheidungsverfahren.Any())
            fehler.Add($"K.-o.-Runde '{runde.Name}' benötigt einen Sieger, aber Regelwerk '{regelwerk.Name}' " +
                       $"hat kein Entscheidungsverfahren definiert (z.B. Verlängerung, Elfmeter).");

        return fehler.Count == 0 ? ValidationErgebnis.Ok() : ValidationErgebnis.MitFehlern([.. fehler]);
    }

    /// <summary>Validiert Basisdaten einer TurnierInstanz (Issue #294 — vereinheitlicht die frühere
    /// entitätsbasierte <c>TurnierInstanzValidieren</c> mit dem primitiv-basierten
    /// <c>TurnierVorlageValidator.BasisdatenValidieren</c>, da eine Vorlage jetzt dieselbe Entität ist).
    /// Primitiv-basiert, damit sie auch als Live-Vorschau vor dem Speichern (ViewModel) nutzbar ist.</summary>
    public ValidationErgebnis BasisdatenValidieren(
        string name, string saisonOderJahr, int? zielMannschaftsanzahl,
        DateOnly? startdatum, DateOnly? enddatum, byte[]? logo = null)
    {
        var fehler = new List<string>();

        if (string.IsNullOrWhiteSpace(name))
            fehler.Add("Turniername darf nicht leer sein.");

        if (string.IsNullOrWhiteSpace(saisonOderJahr))
            fehler.Add("Saison/Jahr darf nicht leer sein.");

        if (zielMannschaftsanzahl is not null && zielMannschaftsanzahl < 2)
            fehler.Add($"Mannschaftsanzahl muss mindestens 2 betragen (war: {zielMannschaftsanzahl}).");

        if (startdatum.HasValue && enddatum.HasValue && enddatum < startdatum)
            fehler.Add("Endjahr darf nicht vor dem Startjahr liegen.");

        BildValidierung.GroessePruefen(logo, fehler);

        return fehler.Count == 0 ? ValidationErgebnis.Ok() : ValidationErgebnis.MitFehlern([.. fehler]);
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

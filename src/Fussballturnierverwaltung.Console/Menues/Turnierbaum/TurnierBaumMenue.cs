using Fussballturnierverwaltung.Application.Turnierbaum;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Turnierbaum;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Konsole.Hilfsmittel;
using Spectre.Console;

namespace Fussballturnierverwaltung.Konsole.Menues.Turnierbaum;

public class TurnierBaumMenue(
    TurnierBaumService baumService,
    TurnierInstanzService instanzService)
{
    private enum Option
    {
        BaumGenerieren,
        BaumAnzeigen,
        SiegerFortsteiern,
        Zurueck
    }

    public async Task AnzeigenAsync(CancellationToken ct = default)
    {
        while (!ct.IsCancellationRequested)
        {
            KonsolenHilfsmittel.Kopf("🏆 Turnierbaum / Bracket");
            var auswahl = AnsiConsole.Prompt(
                new SelectionPrompt<Option>()
                    .UseConverter(o => o switch
                    {
                        Option.BaumGenerieren   => "Turnierbaum generieren / neu erstellen",
                        Option.BaumAnzeigen     => "Turnierbaum anzeigen",
                        Option.SiegerFortsteiern => "Sieger in nächste Runde fortschreiben",
                        Option.Zurueck          => "← Zurück",
                        _                       => o.ToString()
                    })
                    .AddChoices(Option.BaumGenerieren, Option.BaumAnzeigen,
                                Option.SiegerFortsteiern, Option.Zurueck));

            if (auswahl == Option.Zurueck) break;

            try { await AktionAusfuehrenAsync(auswahl, ct); }
            catch (Exception ex) { KonsolenHilfsmittel.FehlerAnzeigen(ex.Message); KonsolenHilfsmittel.Pause(); }
        }
    }

    private async Task AktionAusfuehrenAsync(Option auswahl, CancellationToken ct)
    {
        switch (auswahl)
        {
            case Option.BaumGenerieren:    await GenerierenAsync(ct);       break;
            case Option.BaumAnzeigen:      await AnzeigenAsync2(ct);        break;
            case Option.SiegerFortsteiern: await FortsteiernAsync(ct);      break;
        }
    }

    // ── Generieren ────────────────────────────────────────────────────────

    private async Task GenerierenAsync(CancellationToken ct)
    {
        KonsolenHilfsmittel.Kopf("Turnierbaum generieren");

        var instanz = await TurnierAuswaehlenAsync(ct);
        if (instanz is null) return;

        // Warnung falls Baum bereits existiert
        var bestehend = await baumService.AbrufenAsync(instanz.Id, ct);
        if (bestehend is not null &&
            !KonsolenHilfsmittel.Bestaetigen("Ein Turnierbaum existiert bereits. Überschreiben?"))
        {
            KonsolenHilfsmittel.WarnungAnzeigen("Abgebrochen.");
            KonsolenHilfsmittel.Pause();
            return;
        }

        var baum = await AnsiConsole.Status()
            .StartAsync("Turnierbaum wird generiert...", async _ =>
                await baumService.GenerierenAsync(instanz.Id, ct));

        KonsolenHilfsmittel.ErfolgAnzeigen(
            $"Turnierbaum generiert: {baum.Knoten.Count} Knoten.");
        KonsolenHilfsmittel.Pause();
    }

    // ── Anzeigen ──────────────────────────────────────────────────────────

    private async Task AnzeigenAsync2(CancellationToken ct)
    {
        KonsolenHilfsmittel.Kopf("Turnierbaum");

        var instanz = await TurnierAuswaehlenAsync(ct);
        if (instanz is null) return;

        var runden = await baumService.BracketLadenAsync(instanz.Id, ct);
        if (!runden.Any())
        {
            KonsolenHilfsmittel.WarnungAnzeigen("Kein Turnierbaum vorhanden. Bitte zuerst generieren.");
            KonsolenHilfsmittel.Pause();
            return;
        }

        KonsolenHilfsmittel.Kopf($"Bracket: {Markup.Escape(instanz.Name)} ({instanz.SaisonOderJahr})");
        BracketAnzeigen(runden, instanz);
        KonsolenHilfsmittel.Pause();
    }

    // ── Sieger fortschreiben ──────────────────────────────────────────────

    private async Task FortsteiernAsync(CancellationToken ct)
    {
        KonsolenHilfsmittel.Kopf("Sieger fortschreiben");

        var instanz = await TurnierAuswaehlenAsync(ct);
        if (instanz is null) return;

        int anzahl = await AnsiConsole.Status()
            .StartAsync("Sieger werden fortgeschrieben...", async _ =>
                await baumService.SiegerFortsteiernAsync(instanz.Id, ct));

        if (anzahl == 0)
            KonsolenHilfsmittel.WarnungAnzeigen(
                "Keine neuen Sieger gefunden. Entweder sind keine Spiele abgeschlossen " +
                "oder alle Sieger wurden bereits fortgeschrieben.");
        else
            KonsolenHilfsmittel.ErfolgAnzeigen($"{anzahl} Spiel(e) aktualisiert.");

        KonsolenHilfsmittel.Pause();
    }

    // ── Bracket-Darstellung ───────────────────────────────────────────────

    private static void BracketAnzeigen(IReadOnlyList<BracketRunde> runden, TurnierInstanz instanz)
    {
        // Rundennamen ableiten (R64 → R32 → R16 → QF → SF → Final)
        int gesamt = runden.Count;
        string[] rundenNamen = ErmittleRundenNamen(runden);

        // Breite jeder Spalte
        const int spaltenBreite = 28;

        // Kopfzeile
        AnsiConsole.WriteLine();
        for (int i = 0; i < runden.Count; i++)
        {
            var name = rundenNamen[i];
            var count = runden[i].Knoten.Count;
            var header = $"{name} ({count})";
            AnsiConsole.Markup($"[bold blue]{header.PadRight(spaltenBreite)}[/]");
        }
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule());

        // Maximale Knotenanzahl (erste Runde hat die meisten)
        int maxKnoten = runden[0].Knoten.Count;

        // Pro Zeile: jeder Knoten der ersten Runde braucht Platz
        // Knoten in Runde r: maxKnoten / 2^(r-1)  → Abstand = 2^(r-1)
        for (int knotenIdx = 0; knotenIdx < maxKnoten; knotenIdx++)
        {
            // Zeile 1: Heim-Mannschaft
            for (int r = 0; r < runden.Count; r++)
            {
                int abstand = (int)Math.Pow(2, r);
                // Dieser Knoten erscheint wenn knotenIdx % abstand == 0
                if (knotenIdx % abstand == 0)
                {
                    int kIdx = knotenIdx / abstand;
                    var knoten = kIdx < runden[r].Knoten.Count ? runden[r].Knoten[kIdx] : null;
                    AnsiConsole.Markup(FormatHeim(knoten).PadRight(spaltenBreite));
                }
                else
                {
                    AnsiConsole.Markup("".PadRight(spaltenBreite));
                }
            }
            AnsiConsole.WriteLine();

            // Zeile 2: Auswärts-Mannschaft + Ergebnis
            for (int r = 0; r < runden.Count; r++)
            {
                int abstand = (int)Math.Pow(2, r);
                if (knotenIdx % abstand == 0)
                {
                    int kIdx = knotenIdx / abstand;
                    var knoten = kIdx < runden[r].Knoten.Count ? runden[r].Knoten[kIdx] : null;
                    AnsiConsole.Markup(FormatAuswaerts(knoten).PadRight(spaltenBreite));
                }
                else
                {
                    AnsiConsole.Markup("".PadRight(spaltenBreite));
                }
            }
            AnsiConsole.WriteLine();

            // Trennzeile (außer nach dem letzten Knoten)
            if (knotenIdx < maxKnoten - 1)
                AnsiConsole.WriteLine();
        }

        AnsiConsole.WriteLine();

        // Legende
        AnsiConsole.MarkupLine("[grey]Legende:[/] [green]● Sieger[/]  [grey]○ Ausstehend  – Freilos[/]");
    }

    private static string FormatHeim(TurnierBaumKnoten? knoten)
    {
        if (knoten is null) return "[grey]–[/]";
        if (knoten.Spiel is null) return "[grey]Ausstehend[/]";

        var spiel  = knoten.Spiel;
        var heim   = spiel.HeimMannschaft?.Name ?? "?";
        var ergebnis = spiel.Ergebnis;

        if (ergebnis is null)
            return Markup.Escape($"○ {TrimName(heim, 18)}");

        bool heimSieger = ergebnis.SiegerMannschaftId == spiel.HeimMannschaftId;
        string prefix = heimSieger ? "[green]●[/] " : "[grey]○[/] ";
        string score  = $"{ergebnis.HeimToreGesamt}";
        return $"{prefix}{Markup.Escape(TrimName(heim, 16))} {score}";
    }

    private static string FormatAuswaerts(TurnierBaumKnoten? knoten)
    {
        if (knoten is null) return "";
        if (knoten.Spiel is null) return "[grey]   –[/]";

        var spiel     = knoten.Spiel;
        var auswaerts = spiel.AuswaertsMannschaft?.Name ?? "?";
        var ergebnis  = spiel.Ergebnis;

        string entscheidung = "";
        if (ergebnis?.EntscheidungArt is not null and not EntscheidungArt.RegulaereSpielzeit)
        {
            entscheidung = ergebnis.EntscheidungArt switch
            {
                EntscheidungArt.Verlaengerung   => " n.V.",
                EntscheidungArt.Elfmeterschiessen => $" i.E. ({ergebnis.HeimElfmetertore}:{ergebnis.AuswaertsElfmetertore})",
                EntscheidungArt.GoldenGoal      => " GG",
                EntscheidungArt.SilverGoal      => " SG",
                EntscheidungArt.Losentscheid    => " Los",
                _ => ""
            };
        }

        if (ergebnis is null)
            return Markup.Escape($"  {TrimName(auswaerts, 18)}");

        bool auswaertsSieger = ergebnis.SiegerMannschaftId == spiel.AuswaertsMannschaftId;
        string prefix = auswaertsSieger ? "[green]●[/] " : "[grey]○[/] ";
        string score  = $"{ergebnis.AuswaertsToreGesamt}";
        return $"{prefix}{Markup.Escape(TrimName(auswaerts, 14))} {score}{Markup.Escape(entscheidung)}";
    }

    private static string[] ErmittleRundenNamen(IReadOnlyList<BracketRunde> runden)
    {
        var namen = new string[runden.Count];
        int letzteIdx = runden.Count - 1;

        for (int i = letzteIdx; i >= 0; i--)
        {
            int spiele = runden[i].Knoten.Count;
            namen[i] = (i == letzteIdx) switch
            {
                true => "Finale",
                _ when i == letzteIdx - 1 && runden[letzteIdx].Knoten.Count == 1 => "Halbfinale",
                _ => spiele switch
                {
                    1  => "Finale",
                    2  => "Halbfinale",
                    4  => "Viertelfinale",
                    8  => "Achtelfinale",
                    16 => "Runde der 16",
                    32 => "Runde der 32",
                    _  => $"Runde {i + 1}"
                }
            };
        }

        return namen;
    }

    private static string TrimName(string name, int maxLen)
        => name.Length <= maxLen ? name : name[..(maxLen - 1)] + "…";

    // ── Hilfsmethoden ─────────────────────────────────────────────────────

    private async Task<TurnierInstanz?> TurnierAuswaehlenAsync(CancellationToken ct)
    {
        var instanzen = await instanzService.AlleAbrufenAsync(ct);
        if (!instanzen.Any())
        {
            KonsolenHilfsmittel.WarnungAnzeigen("Keine Turniere vorhanden.");
            KonsolenHilfsmittel.Pause();
            return null;
        }
        return AnsiConsole.Prompt(
            new SelectionPrompt<TurnierInstanz?>()
                .Title("Turnier:")
                .UseConverter(ti => ti is null ? "← Zurück" : $"{ti.Name} ({ti.SaisonOderJahr})")
                .EnableSearch()
                .AddChoices(instanzen.Cast<TurnierInstanz?>().Append(null)));
    }
}

using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Application.Turniere.Eingaben;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Konsole.Hilfsmittel;
using Spectre.Console;

namespace Fussballturnierverwaltung.Konsole.Menues.Turniere;

public class VerbandsQuoteMenue(
    TurnierInstanzService instanzService,
    VerbandService verbandService,
    VerbandsQuoteService quoteService)
{
    private enum Option { AlleAnzeigen, NeuAnlegen, Bearbeiten, Loeschen, Zurueck }

    public async Task AnzeigenAsync(CancellationToken ct = default)
    {
        while (!ct.IsCancellationRequested)
        {
            KonsolenHilfsmittel.Kopf("🌍 Verband-Quoten");

            var auswahl = AnsiConsole.Prompt(
                new SelectionPrompt<Option>()
                    .UseConverter(o => o switch
                    {
                        Option.AlleAnzeigen => "Alle anzeigen",
                        Option.NeuAnlegen   => "Neue Quote anlegen",
                        Option.Bearbeiten   => "Quote bearbeiten",
                        Option.Loeschen     => "Quote löschen",
                        Option.Zurueck      => "← Zurück",
                        _                   => o.ToString()
                    })
                    .AddChoices(Option.AlleAnzeigen, Option.NeuAnlegen, Option.Bearbeiten, Option.Loeschen, Option.Zurueck));

            if (auswahl == Option.Zurueck) break;

            try { await AktionAusfuehrenAsync(auswahl, ct); }
            catch (Exception ex) { KonsolenHilfsmittel.FehlerAnzeigen(ex.Message); KonsolenHilfsmittel.Pause(); }
        }
    }

    private async Task AktionAusfuehrenAsync(Option auswahl, CancellationToken ct)
    {
        switch (auswahl)
        {
            case Option.AlleAnzeigen: await AlleAnzeigenAsync(ct); break;
            case Option.NeuAnlegen:   await NeuAnlegenAsync(ct);   break;
            case Option.Bearbeiten:   await BearbeitenAsync(ct);   break;
            case Option.Loeschen:     await LoeschenAsync(ct);     break;
        }
    }

    private async Task AlleAnzeigenAsync(CancellationToken ct)
    {
        var instanz = await TurnierAuswaehlenAsync("Turnier:", ct);
        if (instanz is null) return;

        KonsolenHilfsmittel.Kopf($"Verband-Quoten – {instanz.Name}");
        var quoten = await quoteService.AlleNachTurnierInstanzIdAsync(instanz.Id, ct);

        if (quoten.Count == 0)
        {
            KonsolenHilfsmittel.WarnungAnzeigen("Keine Verband-Quoten definiert.");
            KonsolenHilfsmittel.Pause();
            return;
        }

        var tabelle = new Table().Border(TableBorder.Rounded)
            .AddColumn(new TableColumn("[grey]ID[/]").RightAligned())
            .AddColumn("Verband")
            .AddColumn(new TableColumn("Anzahl").Centered());

        foreach (var q in quoten.OrderBy(q => q.Verband.Name))
            tabelle.AddRow(
                $"[grey]{q.Id}[/]", Markup.Escape(q.Verband.Name), q.Anzahl.ToString());

        AnsiConsole.Write(tabelle);
        KonsolenHilfsmittel.Pause();
    }

    private async Task NeuAnlegenAsync(CancellationToken ct)
    {
        var instanz = await TurnierAuswaehlenAsync("Turnier:", ct);
        if (instanz is null) return;

        var verbaende = await verbandService.AlleAbrufenAsync(ct);
        var verband = VerbandAuswaehlen("Verband:", verbaende);
        if (verband is null) return;

        var anzahl = AnzahlAbfragen();

        var quote = await quoteService.ErstellenAsync(new VerbandsQuoteEingabe(
            instanz.Id, verband.Id, anzahl), ct);

        KonsolenHilfsmittel.ErfolgAnzeigen(
            $"Verband-Quote angelegt (ID {quote.Id}): '{verband.Name}' – Anzahl {anzahl}.");
        KonsolenHilfsmittel.Pause();
    }

    private async Task BearbeitenAsync(CancellationToken ct)
    {
        var instanz = await TurnierAuswaehlenAsync("Turnier:", ct);
        if (instanz is null) return;

        var quote = await QuoteAuswaehlenAsync(instanz.Id, ct);
        if (quote is null) return;

        var verbaende = await verbandService.AlleAbrufenAsync(ct);
        AnsiConsole.MarkupLine($"\n[grey]Aktueller Verband: {Markup.Escape(quote.Verband.Name)}[/]");
        var verband = VerbandAuswaehlen("Verband (leer = unverändert):", verbaende) ?? quote.Verband;

        var anzahl = AnzahlAbfragen(quote);

        await quoteService.AktualisierenAsync(quote.Id, new VerbandsQuoteEingabe(
            instanz.Id, verband.Id, anzahl), ct);

        KonsolenHilfsmittel.ErfolgAnzeigen($"Verband-Quote {quote.Id} gespeichert.");
        KonsolenHilfsmittel.Pause();
    }

    private async Task LoeschenAsync(CancellationToken ct)
    {
        var instanz = await TurnierAuswaehlenAsync("Turnier:", ct);
        if (instanz is null) return;

        var quote = await QuoteAuswaehlenAsync(instanz.Id, ct);
        if (quote is null) return;

        if (!KonsolenHilfsmittel.Bestaetigen($"[red]Quote {quote.Id}[/] ({Markup.Escape(quote.Verband.Name)}) wirklich löschen?"))
        { KonsolenHilfsmittel.WarnungAnzeigen("Abgebrochen."); KonsolenHilfsmittel.Pause(); return; }

        await quoteService.LoeschenAsync(quote.Id, ct);
        KonsolenHilfsmittel.ErfolgAnzeigen($"Quote {quote.Id} gelöscht.");
        KonsolenHilfsmittel.Pause();
    }

    // ── Hilfsmethoden ─────────────────────────────────────────────────────

    private async Task<TurnierInstanz?> TurnierAuswaehlenAsync(string titel, CancellationToken ct)
    {
        var instanzen = await instanzService.AlleAbrufenAsync(ct);
        if (instanzen.Count == 0)
        { KonsolenHilfsmittel.WarnungAnzeigen("Keine Turniere vorhanden."); KonsolenHilfsmittel.Pause(); return null; }

        return AnsiConsole.Prompt(new SelectionPrompt<TurnierInstanz?>()
            .Title(titel).UseConverter(ti => ti is null ? "← Zurück" : $"{ti.Name} ({ti.SaisonOderJahr})")
            .EnableSearch().AddChoices(instanzen.Cast<TurnierInstanz?>().Append(null)));
    }

    private static Verband? VerbandAuswaehlen(string titel, IReadOnlyList<Verband> verbaende)
    {
        if (verbaende.Count == 0)
        { KonsolenHilfsmittel.WarnungAnzeigen("Keine Verbände vorhanden."); KonsolenHilfsmittel.Pause(); return null; }

        return AnsiConsole.Prompt(new SelectionPrompt<Verband?>()
            .Title(titel)
            .UseConverter(v => v is null ? "← Zurück" : $"{Markup.Escape(v.Name)} ({Markup.Escape(v.Kurzname)})")
            .EnableSearch()
            .AddChoices(verbaende.OrderBy(v => v.Name).Cast<Verband?>().Append(null)));
    }

    private async Task<VerbandsQuote?> QuoteAuswaehlenAsync(int turnierInstanzId, CancellationToken ct)
    {
        var quoten = await quoteService.AlleNachTurnierInstanzIdAsync(turnierInstanzId, ct);
        if (quoten.Count == 0)
        { KonsolenHilfsmittel.WarnungAnzeigen("Keine Verband-Quoten vorhanden."); KonsolenHilfsmittel.Pause(); return null; }

        return AnsiConsole.Prompt(new SelectionPrompt<VerbandsQuote?>()
            .Title("Quote:")
            .UseConverter(q => q is null ? "← Zurück" :
                $"[{q.Id}] {Markup.Escape(q.Verband.Name)} (Anzahl {q.Anzahl})")
            .AddChoices(quoten.Cast<VerbandsQuote?>().Append(null)));
    }

    private static int AnzahlAbfragen(VerbandsQuote? bestehend = null) =>
        AnsiConsole.Prompt(new TextPrompt<int>("Anzahl:").DefaultValue(bestehend?.Anzahl ?? 1));
}

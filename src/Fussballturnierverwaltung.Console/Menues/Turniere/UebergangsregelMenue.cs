using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Application.Spielplan.Eingaben;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Konsole.Hilfsmittel;
using Spectre.Console;

namespace Fussballturnierverwaltung.Konsole.Menues.Turniere;

public class UebergangsregelMenue(
    TurnierInstanzService instanzService,
    UebergangsregelService regelService)
{
    private enum Option { AlleAnzeigen, NeuAnlegen, Bearbeiten, Loeschen, Zurueck }

    public async Task AnzeigenAsync(CancellationToken ct = default)
    {
        while (!ct.IsCancellationRequested)
        {
            KonsolenHilfsmittel.Kopf("🔀 Übergangsregeln");

            var auswahl = AnsiConsole.Prompt(
                new SelectionPrompt<Option>()
                    .UseConverter(o => o switch
                    {
                        Option.AlleAnzeigen => "Alle anzeigen",
                        Option.NeuAnlegen   => "Neue Regel anlegen",
                        Option.Bearbeiten   => "Regel bearbeiten",
                        Option.Loeschen     => "Regel löschen",
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

        KonsolenHilfsmittel.Kopf($"Übergangsregeln – {instanz.Name}");
        var regeln = await regelService.AlleNachTurnierInstanzIdAsync(instanz.Id, ct);

        if (regeln.Count == 0)
        {
            KonsolenHilfsmittel.WarnungAnzeigen("Keine Übergangsregeln definiert.");
            KonsolenHilfsmittel.Pause();
            return;
        }

        var tabelle = new Table().Border(TableBorder.Rounded)
            .AddColumn(new TableColumn("[grey]ID[/]").RightAligned())
            .AddColumn("Quellrunde")
            .AddColumn(new TableColumn("Gr./Spiel").Centered())
            .AddColumn(new TableColumn("Platz").Centered())
            .AddColumn("Zielrunde");

        foreach (var r in regeln.OrderBy(r => r.QuellRunde.Reihenfolge).ThenBy(r => r.GruppeIndex).ThenBy(r => r.PlatzierungVon))
            tabelle.AddRow(
                $"[grey]{r.Id}[/]", Markup.Escape(r.QuellRunde.Name),
                r.GruppeIndex?.ToString() ?? "[grey]–[/]",
                r.PlatzierungVon == r.PlatzierungBis ? r.PlatzierungVon.ToString() : $"{r.PlatzierungVon}–{r.PlatzierungBis}",
                Markup.Escape(r.ZielRunde.Name));

        AnsiConsole.Write(tabelle);
        KonsolenHilfsmittel.Pause();
    }

    private async Task NeuAnlegenAsync(CancellationToken ct)
    {
        var instanz = await TurnierAuswaehlenAsync("Turnier:", ct);
        if (instanz is null) return;

        var quellRunde = RundeAuswaehlen("Quellrunde:", instanz.Runden);
        if (quellRunde is null) return;

        var zielRunde = RundeAuswaehlen("Zielrunde:", instanz.Runden.Where(r => r.Id != quellRunde.Id));
        if (zielRunde is null) return;

        var werte = UebergangsregelWerteAbfragen(quellRunde);

        var regel = await regelService.ErstellenAsync(new UebergangsregelEingabe(
            quellRunde.Id, zielRunde.Id, werte.GruppeIndex, werte.PlatzierungVon, werte.PlatzierungBis), ct);

        KonsolenHilfsmittel.ErfolgAnzeigen(
            $"Übergangsregel angelegt (ID {regel.Id}): '{quellRunde.Name}' → '{zielRunde.Name}'.");
        KonsolenHilfsmittel.Pause();
    }

    private async Task BearbeitenAsync(CancellationToken ct)
    {
        var instanz = await TurnierAuswaehlenAsync("Turnier:", ct);
        if (instanz is null) return;

        var regel = await RegelAuswaehlenAsync(instanz.Id, ct);
        if (regel is null) return;

        var quellRunde = instanz.Runden.First(r => r.Id == regel.QuellRundeId);
        var bisherigeZielRunde = instanz.Runden.First(r => r.Id == regel.ZielRundeId);

        AnsiConsole.MarkupLine($"\n[grey]Quellrunde bleibt unverändert: {Markup.Escape(quellRunde.Name)}[/]");
        var zielRunde = RundeAuswaehlen(
            $"Zielrunde (aktuell: {bisherigeZielRunde.Name}):", instanz.Runden.Where(r => r.Id != quellRunde.Id))
            ?? bisherigeZielRunde;

        var werte = UebergangsregelWerteAbfragen(quellRunde, regel);

        await regelService.AktualisierenAsync(regel.Id, new UebergangsregelEingabe(
            quellRunde.Id, zielRunde.Id, werte.GruppeIndex, werte.PlatzierungVon, werte.PlatzierungBis), ct);

        KonsolenHilfsmittel.ErfolgAnzeigen($"Übergangsregel {regel.Id} gespeichert.");
        KonsolenHilfsmittel.Pause();
    }

    private async Task LoeschenAsync(CancellationToken ct)
    {
        var instanz = await TurnierAuswaehlenAsync("Turnier:", ct);
        if (instanz is null) return;

        var regel = await RegelAuswaehlenAsync(instanz.Id, ct);
        if (regel is null) return;

        if (!KonsolenHilfsmittel.Bestaetigen($"[red]Regel {regel.Id}[/] wirklich löschen?"))
        { KonsolenHilfsmittel.WarnungAnzeigen("Abgebrochen."); KonsolenHilfsmittel.Pause(); return; }

        await regelService.LoeschenAsync(regel.Id, ct);
        KonsolenHilfsmittel.ErfolgAnzeigen($"Regel {regel.Id} gelöscht.");
        KonsolenHilfsmittel.Pause();
    }

    // ── Hilfsmethoden ─────────────────────────────────────────────────────

    private async Task<TurnierInstanz?> TurnierAuswaehlenAsync(string titel, CancellationToken ct)
    {
        var instanzen = await instanzService.AlleAbrufenAsync(ct);
        if (instanzen.Count == 0)
        { KonsolenHilfsmittel.WarnungAnzeigen("Keine Turniere vorhanden."); KonsolenHilfsmittel.Pause(); return null; }

        var auswahl = AnsiConsole.Prompt(new SelectionPrompt<TurnierInstanz?>()
            .Title(titel).UseConverter(ti => ti is null ? "← Zurück" : $"{ti.Name} ({ti.SaisonOderJahr})")
            .EnableSearch().AddChoices(instanzen.Cast<TurnierInstanz?>().Append(null)));
        if (auswahl is null) return null;

        return await instanzService.FindenNachIdMitRundenAsync(auswahl.Id, ct);
    }

    private static TurnierRunde? RundeAuswaehlen(string titel, IEnumerable<TurnierRunde> runden)
    {
        var liste = runden.OrderBy(r => r.Reihenfolge).ToList();
        if (liste.Count == 0)
        { KonsolenHilfsmittel.WarnungAnzeigen("Keine passenden Runden vorhanden."); KonsolenHilfsmittel.Pause(); return null; }

        return AnsiConsole.Prompt(new SelectionPrompt<TurnierRunde?>()
            .Title(titel)
            .UseConverter(r => r is null ? "← Zurück" : $"{r.Reihenfolge}. {Markup.Escape(r.Name)} [[{r.Rundenart}]]")
            .AddChoices(liste.Cast<TurnierRunde?>().Append(null)));
    }

    private async Task<Uebergangsregel?> RegelAuswaehlenAsync(int turnierInstanzId, CancellationToken ct)
    {
        var regeln = await regelService.AlleNachTurnierInstanzIdAsync(turnierInstanzId, ct);
        if (regeln.Count == 0)
        { KonsolenHilfsmittel.WarnungAnzeigen("Keine Übergangsregeln vorhanden."); KonsolenHilfsmittel.Pause(); return null; }

        return AnsiConsole.Prompt(new SelectionPrompt<Uebergangsregel?>()
            .Title("Regel:")
            .UseConverter(r => r is null ? "← Zurück" :
                $"[{r.Id}] {Markup.Escape(r.QuellRunde.Name)} (Platz {r.PlatzierungVon}-{r.PlatzierungBis}) → {Markup.Escape(r.ZielRunde.Name)}")
            .AddChoices(regeln.Cast<Uebergangsregel?>().Append(null)));
    }

    private sealed record UebergangsregelWerte(int? GruppeIndex, int PlatzierungVon, int PlatzierungBis);

    /// <summary>Fragt Gruppenindex/Spielnummer und Platzierungsbereich ab. Die Bedeutung von "Gruppenindex" hängt
    /// von der Rundenart der Quellrunde ab (siehe UebergangsregelValidator).</summary>
    private static UebergangsregelWerte UebergangsregelWerteAbfragen(TurnierRunde quellRunde, Uebergangsregel? bestehend = null)
    {
        int? gruppeIndex = null;
        string? gruppeIndexLabel = quellRunde.Rundenart switch
        {
            Rundenart.Gruppenphase                                          => "Gruppennummer (1=A, 2=B, ...)",
            Rundenart.KoRunde or Rundenart.Finale or Rundenart.SpielUmPlatzDrei => "Spielnummer innerhalb der Runde",
            _                                                                => null
        };

        if (gruppeIndexLabel is not null)
            gruppeIndex = AnsiConsole.Prompt(
                new TextPrompt<int>($"{gruppeIndexLabel}:").DefaultValue(bestehend?.GruppeIndex ?? 1));

        string platzHinweis = quellRunde.Rundenart is Rundenart.KoRunde or Rundenart.Finale or Rundenart.SpielUmPlatzDrei
            ? " (1 = Sieger, 2 = Verlierer)"
            : "";

        var von = AnsiConsole.Prompt(new TextPrompt<int>($"Platzierung von{platzHinweis}:").DefaultValue(bestehend?.PlatzierungVon ?? 1));
        var bis = AnsiConsole.Prompt(new TextPrompt<int>($"Platzierung bis{platzHinweis}:").DefaultValue(bestehend?.PlatzierungBis ?? von));

        return new UebergangsregelWerte(gruppeIndex, von, bis);
    }
}

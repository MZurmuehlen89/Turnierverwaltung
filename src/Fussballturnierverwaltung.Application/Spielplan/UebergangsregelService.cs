using Fussballturnierverwaltung.Application.Spielplan.Eingaben;
using Fussballturnierverwaltung.Application.Validierung;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Microsoft.Extensions.Logging;

namespace Fussballturnierverwaltung.Application.Spielplan;

public class UebergangsregelService(
    IUebergangsregelRepository repository,
    ITurnierRundeRepository rundeRepository,
    UebergangsregelValidator validator,
    PlatzhalterAufloesung aufloesung,
    ILogger<UebergangsregelService> logger)
{
    public Task<IReadOnlyList<Uebergangsregel>> AlleNachQuellRundeIdAsync(int quellRundeId, CancellationToken ct = default)
        => repository.AlleNachQuellRundeIdAsync(quellRundeId, ct);

    public Task<IReadOnlyList<Uebergangsregel>> AlleNachZielRundeIdAsync(int zielRundeId, CancellationToken ct = default)
        => repository.AlleNachZielRundeIdAsync(zielRundeId, ct);

    public Task<IReadOnlyList<Uebergangsregel>> AlleNachTurnierInstanzIdAsync(int turnierInstanzId, CancellationToken ct = default)
        => repository.AlleNachTurnierInstanzIdAsync(turnierInstanzId, ct);

    /// <summary>Issue #153: Ausgangsmannschaftsanzahl aus den ausgehenden Übergangsregeln einer Runde — flache
    /// Summe der Platzierungsbereiche, keine Gruppen-Multiplikation nötig (der Runden-Editor expandiert eine
    /// Gruppenphase-Quellrunde immer in eine Zeile je Gruppe, siehe UebergangsregelBlock.OnQuellRundeChanged;
    /// GruppeIndex=null ist für Gruppenphase-Quellrunden ohnehin ungültig, siehe UebergangsregelValidator).</summary>
    public static int AusgangAbleiten(IReadOnlyList<Uebergangsregel> ausgehendeRegeln)
        => ausgehendeRegeln.Sum(r => r.AnzahlBeste ?? (r.PlatzierungBis - r.PlatzierungVon + 1));

    /// <summary>Spiegel von <see cref="AusgangAbleiten"/> für die Eingangsseite (Issue #186-Folgefund):
    /// identische Rechenformel, hier aus Sicht der Zielrunde über alle ihre eingehenden Übergangsregeln
    /// (ggf. aus mehreren Quellrunden) hinweg.</summary>
    public static int EingangAbleiten(IReadOnlyList<Uebergangsregel> eingehendeRegeln) => AusgangAbleiten(eingehendeRegeln);

    public async Task<Uebergangsregel> ErstellenAsync(UebergangsregelEingabe eingabe, CancellationToken ct = default)
    {
        var quellRunde = await QuellRundeLadenAsync(eingabe.QuellRundeId, ct);
        var zielRunde = await ZielRundeLadenAsync(eingabe.ZielRundeId, ct);
        var alleRegeln = await repository.AlleFindenAsync(ct);
        ValidationException.WerfenWennUngueltig(validator.Validieren(eingabe, quellRunde, zielRunde, alleRegeln));

        var regel = NeueRegel(eingabe);
        await repository.HinzufuegenAsync(regel, ct);
        logger.LogInformation(
            "Übergangsregel angelegt: Runde '{QuellRunde}' → Runde '{ZielRunde}' (Platz {Von}-{Bis}).",
            quellRunde.Name, eingabe.ZielRundeId, eingabe.PlatzierungVon, eingabe.PlatzierungBis);
        return regel;
    }

    public async Task AktualisierenAsync(int id, UebergangsregelEingabe eingabe, CancellationToken ct = default)
    {
        var regel = await RegelLadenAsync(id, ct);

        var quellRunde = await QuellRundeLadenAsync(eingabe.QuellRundeId, ct);
        var zielRunde = await ZielRundeLadenAsync(eingabe.ZielRundeId, ct);
        var alleRegeln = await repository.AlleFindenAsync(ct);
        ValidationException.WerfenWennUngueltig(validator.Validieren(eingabe, quellRunde, zielRunde, alleRegeln, bearbeiteteRegelId: id));

        regel.QuellRundeId = eingabe.QuellRundeId;
        regel.ZielRundeId  = eingabe.ZielRundeId;
        PlatzierungUebernehmen(regel, eingabe);

        await repository.AktualisierenAsync(regel, ct);
        logger.LogInformation("Übergangsregel {Id} aktualisiert.", id);
    }

    /// <summary>Speichert einen gesamten Übergangsregel-Block (alle Zeilen einer Quellrunde → Zielrunde) in einem
    /// Zug: validiert zuerst den vollständigen beabsichtigten Endzustand, erst danach werden Schreibzugriffe
    /// ausgeführt — kein Halbzustand mehr bei einem Validierungsfehler in der Mitte eines Blocks (Issue #180).</summary>
    public async Task<IReadOnlyList<Uebergangsregel>> BlockSpeichernAsync(
        int quellRundeId, int zielRundeId,
        IReadOnlyList<UebergangsregelZeileEingabe> zeilen,
        IReadOnlyList<int> zuLoeschendeIds,
        CancellationToken ct = default)
    {
        var quellRunde = await QuellRundeLadenAsync(quellRundeId, ct);
        var zielRunde  = await ZielRundeLadenAsync(zielRundeId, ct);
        var alleRegeln = await repository.AlleFindenAsync(ct);

        var eingaben = zeilen
            .Select(z => new UebergangsregelEingabe(quellRundeId, zielRundeId, z.GruppeIndex, z.PlatzierungVon, z.PlatzierungBis, z.AnzahlBeste))
            .ToList();
        ValidationException.WerfenWennUngueltig(validator.Validieren(eingaben, quellRunde, zielRunde, alleRegeln));

        await VorhandeneLoeschenAsync(zuLoeschendeIds, ct);

        var ergebnis = new List<Uebergangsregel>();
        foreach (var (zeile, eingabe) in zeilen.Zip(eingaben))
            ergebnis.Add(await ZeileSpeichernAsync(zeile.Id, eingabe, ct));

        logger.LogInformation(
            "Übergangsregel-Block '{QuellRunde}' → '{ZielRunde}' gespeichert ({Anzahl} Zeilen, {Geloescht} gelöscht).",
            quellRunde.Name, zielRunde.Name, zeilen.Count, zuLoeschendeIds.Count);
        return ergebnis;
    }

    /// <summary>Bereits gelöschte Regeln werden übergangen.</summary>
    private async Task VorhandeneLoeschenAsync(IReadOnlyList<int> ids, CancellationToken ct)
    {
        foreach (var id in ids)
        {
            var regel = await repository.FindenNachIdAsync(id, ct);
            if (regel is not null)
                await repository.LoeschenAsync(regel, ct);
        }
    }

    /// <summary>Eine Zeile ohne <paramref name="id"/> legt eine neue Regel an, sonst wird die bestehende aktualisiert.</summary>
    private async Task<Uebergangsregel> ZeileSpeichernAsync(int? id, UebergangsregelEingabe eingabe, CancellationToken ct)
    {
        if (id is null)
        {
            var neu = NeueRegel(eingabe);
            await repository.HinzufuegenAsync(neu, ct);
            return neu;
        }

        var regel = await RegelLadenAsync(id.Value, ct);
        PlatzierungUebernehmen(regel, eingabe);
        await repository.AktualisierenAsync(regel, ct);
        return regel;
    }

    private static Uebergangsregel NeueRegel(UebergangsregelEingabe eingabe)
    {
        var regel = new Uebergangsregel { QuellRundeId = eingabe.QuellRundeId, ZielRundeId = eingabe.ZielRundeId };
        PlatzierungUebernehmen(regel, eingabe);
        return regel;
    }

    /// <summary>Alles außer Quell- und Zielrunde.</summary>
    private static void PlatzierungUebernehmen(Uebergangsregel regel, UebergangsregelEingabe eingabe)
    {
        regel.GruppeIndex    = eingabe.GruppeIndex;
        regel.PlatzierungVon = eingabe.PlatzierungVon;
        regel.PlatzierungBis = eingabe.PlatzierungBis;
        regel.AnzahlBeste    = eingabe.AnzahlBeste;
    }

    /// <summary>Vollersatz-Semantik (Issue #294): ersetzt die kompletten Übergangsregeln über alle
    /// übergebenen <paramref name="runden"/> hinweg in einem Zug — fachlicher Nachfolger von
    /// <c>TurnierVorlageService.RundenSynchronisierenAsync</c>s Übergangsregel-Teil, jetzt auf echten
    /// <see cref="TurnierRunde"/>/<see cref="Uebergangsregel"/>-Entitäten statt Vorlagen-Pendants.</summary>
    public async Task<IReadOnlyList<Uebergangsregel>> AlleErsetzenAsync(
        IReadOnlyList<TurnierRunde> runden,
        IReadOnlyList<UebergangsregelPositionsEingabe> neueRegeln,
        CancellationToken ct = default)
    {
        ValidationException.WerfenWennUngueltig(validator.Validieren(runden, neueRegeln));
        var ergebnis = await repository.AlleErsetzenAsync(runden, neueRegeln, ct);
        logger.LogInformation("Übergangsregeln für {Anzahl} Runde(n) vollständig ersetzt ({Regeln} Regel(n)).",
            runden.Count, neueRegeln.Count);
        return ergebnis;
    }

    public async Task LoeschenAsync(int id, CancellationToken ct = default)
    {
        var regel = await RegelLadenAsync(id, ct);
        await repository.LoeschenAsync(regel, ct);
        logger.LogInformation("Übergangsregel {Id} gelöscht.", id);
    }

    /// <summary>Löst alle Platzierungen einer Regel auf. Werte sind <c>null</c>, solange die Quellrunde für die
    /// betreffende Platzierung noch nicht (vollständig) entschieden ist.</summary>
    public async Task<IReadOnlyDictionary<int, int?>> QualifikantenErmittelnAsync(int uebergangsregelId, CancellationToken ct = default)
    {
        var regel = await repository.FindenNachIdMitRundenAsync(uebergangsregelId, ct)
            ?? throw new InvalidOperationException($"Übergangsregel mit ID {uebergangsregelId} nicht gefunden.");

        return await aufloesung.UebergangsregelQualifikantenAsync(regel, ct);
    }

    private async Task<Uebergangsregel> RegelLadenAsync(int id, CancellationToken ct)
        => await repository.FindenNachIdAsync(id, ct)
            ?? throw new InvalidOperationException($"Übergangsregel mit ID {id} nicht gefunden.");

    private async Task<TurnierRunde> QuellRundeLadenAsync(int quellRundeId, CancellationToken ct)
        => await rundeRepository.FindenNachIdMitRegelwerkAsync(quellRundeId, ct)
            ?? throw new InvalidOperationException($"Quellrunde mit ID {quellRundeId} nicht gefunden.");

    private async Task<TurnierRunde> ZielRundeLadenAsync(int zielRundeId, CancellationToken ct)
        => await rundeRepository.FindenNachIdAsync(zielRundeId, ct)
            ?? throw new InvalidOperationException($"Zielrunde mit ID {zielRundeId} nicht gefunden.");
}

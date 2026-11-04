using Fussballturnierverwaltung.Application.Ranglisten.Eingaben;
using Fussballturnierverwaltung.Application.Validierung;
using Fussballturnierverwaltung.Domain.Entitaeten.Ranglisten;
using Microsoft.Extensions.Logging;

namespace Fussballturnierverwaltung.Application.Ranglisten;

public class RanglisteService(
    IRanglisteRepository repository,
    RanglisteValidator validator,
    ILogger<RanglisteService> logger)
{
    public Task<IReadOnlyList<Rangliste>> AlleAbrufenAsync(CancellationToken ct = default)
        => repository.AlleFindenMitEintraegenAsync(ct);

    /// <summary>Alle Ranglisten ohne Einträge — für Auswahllisten (Standard-Rangliste einer Runde, #393).</summary>
    public Task<IReadOnlyList<Rangliste>> AlleOhneEintraegeAbrufenAsync(CancellationToken ct = default)
        => repository.AlleFindenAsync(ct);

    /// <summary>Liga je Verein aus den Ligapyramiden — Untertitel im Spielkopf bei nationalen Vereinsduellen (#384).</summary>
    public async Task<Ligazugehoerigkeit> LigazugehoerigkeitAbrufenAsync(CancellationToken ct = default)
        => Ligazugehoerigkeit.Aus(await repository.LigapyramidenDerLaenderAsync(ct));

    public Task<Rangliste?> FindenNachIdAsync(int id, CancellationToken ct = default)
        => repository.FindenNachIdMitEintraegenAsync(id, ct);

    public async Task<Rangliste> ErstellenAsync(RanglisteErstellenEingabe eingabe, CancellationToken ct = default)
    {
        ValidationException.WerfenWennUngueltig(validator.Validieren(eingabe));

        var rangliste = new Rangliste
        {
            Name   = eingabe.Name.Trim(),
            Datum  = eingabe.Datum,
            Quelle = eingabe.Quelle.Trim(),
            ProfigrenzeSpielklasse = eingabe.ProfigrenzeSpielklasse,
            PokalsiegerId = eingabe.PokalsiegerId
        };

        foreach (var e in eingabe.Eintraege)
        {
            rangliste.Eintraege.Add(new RanglistenEintrag
            {
                MannschaftId = e.MannschaftId,
                Rang         = e.Rang,
                Punkte       = e.Punkte
            });
        }
        AbschnitteAbgleichen(rangliste, eingabe.Abschnitte ?? []);

        await repository.HinzufuegenAsync(rangliste, ct);
        logger.LogInformation("Rangliste '{Name}' angelegt (ID {Id}).", rangliste.Name, rangliste.Id);
        return rangliste;
    }

    public async Task AktualisierenAsync(int id, RanglisteAktualisierenEingabe eingabe, CancellationToken ct = default)
    {
        var rangliste = await repository.FindenNachIdMitEintraegenAsync(id, ct)
            ?? throw new InvalidOperationException($"Rangliste mit ID {id} nicht gefunden.");

        ValidationException.WerfenWennUngueltig(validator.Validieren(eingabe));

        rangliste.Name   = eingabe.Name.Trim();
        rangliste.Datum  = eingabe.Datum;
        rangliste.Quelle = eingabe.Quelle.Trim();
        // Der Editor gibt Profigrenze und Pokalsieger immer mit (#393, #583).
        rangliste.ProfigrenzeSpielklasse = eingabe.ProfigrenzeSpielklasse;
        rangliste.PokalsiegerId = eingabe.PokalsiegerId;

        rangliste.Eintraege.Clear();
        foreach (var e in eingabe.Eintraege)
        {
            rangliste.Eintraege.Add(new RanglistenEintrag
            {
                MannschaftId = e.MannschaftId,
                Rang         = e.Rang,
                Punkte       = e.Punkte
            });
        }

        // Wie beim Erstellen: ohne Abschnitte hat die Rangliste keine (#596, vorher blieben sie für den Import stehen).
        AbschnitteAbgleichen(rangliste, eingabe.Abschnitte ?? []);

        await repository.SpeichernAenderungenAsync(ct);
        logger.LogInformation("Rangliste '{Name}' (ID {Id}) aktualisiert.", rangliste.Name, rangliste.Id);
    }

    // Schlüssel-Abgleich statt Löschen und Neuanlegen: bestehende Abschnitte werden geändert, fehlende entfernt,
    // neue (ohne Id) angelegt.
    private static void AbschnitteAbgleichen(Rangliste rangliste, IReadOnlyList<RanglistenAbschnittEingabe> eingaben)
    {
        var behalten = eingaben.Where(e => e.Id is not null).Select(e => e.Id!.Value).ToHashSet();
        foreach (var entfernt in rangliste.Abschnitte.Where(a => !behalten.Contains(a.Id)).ToList())
            rangliste.Abschnitte.Remove(entfernt);

        foreach (var eingabe in eingaben)
        {
            var abschnitt = eingabe.Id is int id ? rangliste.Abschnitte.FirstOrDefault(a => a.Id == id) : null;
            if (abschnitt is null)
            {
                abschnitt = new RanglistenAbschnitt();
                rangliste.Abschnitte.Add(abschnitt);
            }
            abschnitt.Name = eingabe.Name.Trim();
            abschnitt.Spielklasse = eingabe.Spielklasse;
            abschnitt.BisPlatz = eingabe.BisPlatz;
        }
    }

    public async Task LoeschenAsync(int id, CancellationToken ct = default)
    {
        var rangliste = await repository.FindenNachIdAsync(id, ct)
            ?? throw new InvalidOperationException($"Rangliste mit ID {id} nicht gefunden.");
        await repository.LoeschenAsync(rangliste, ct);
        logger.LogInformation("Rangliste '{Name}' (ID {Id}) gelöscht.", rangliste.Name, rangliste.Id);
    }
}

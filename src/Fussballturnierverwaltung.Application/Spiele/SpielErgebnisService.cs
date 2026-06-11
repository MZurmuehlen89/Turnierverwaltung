using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Application.Spiele.Eingaben;
using Fussballturnierverwaltung.Application.Validierung;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Fussballturnierverwaltung.Application.Spiele;

public class SpielErgebnisService(
    ISpielRepository spielRepository,
    SpielErgebnisValidator validator,
    ILogger<SpielErgebnisService> logger)
{
    public Task<IReadOnlyList<Spiel>> AlleNachRundeAsync(int turnierRundeId, CancellationToken ct = default)
        => spielRepository.AlleNachRundeAsync(turnierRundeId, ct);

    public Task<Spiel?> FindenNachIdAsync(int spielId, CancellationToken ct = default)
        => spielRepository.FindenNachIdMitErgebnisAsync(spielId, ct);

    public async Task ErgebnisErfassenAsync(int spielId, ErgebnisErfassenEingabe eingabe, CancellationToken ct = default)
    {
        var spiel = await spielRepository.FindenNachIdMitRundeUndHinspielAsync(spielId, ct)
            ?? throw new InvalidOperationException($"Spiel {spielId} nicht gefunden.");

        // Auswärtstorregel: bei Rückspiel mit aktivierter Regel Aggregat + Auswärtstore prüfen (AD-001)
        SpielErgebnis? hinspielErgebnis = spiel.IstRueckspiel
            && spiel.TurnierRunde.AuswaertstorRegelAktiv
            && spiel.Hinspiel?.Ergebnis is not null
            ? spiel.Hinspiel.Ergebnis
            : null;

        var (sieger, entscheidungArt) = BestimmeSieger(
            spiel.HeimMannschaftId, spiel.AuswaertsMannschaftId, eingabe, hinspielErgebnis);

        var vollEingabe = new SpielErgebnisEingabe(
            spiel.HeimMannschaftId, spiel.AuswaertsMannschaftId,
            eingabe.HeimToreRegulaer, eingabe.AuswaertsToreRegulaer,
            eingabe.HeimToreVerlaengerung, eingabe.AuswaertsToreVerlaengerung,
            eingabe.HeimElfmetertore, eingabe.AuswaertsElfmetertore,
            entscheidungArt, sieger);

        ValidationException.WerfenWennUngueltig(validator.Validieren(vollEingabe));

        var ergebnis = new SpielErgebnis
        {
            HeimToreRegulaer           = eingabe.HeimToreRegulaer,
            AuswaertsToreRegulaer      = eingabe.AuswaertsToreRegulaer,
            HeimToreVerlaengerung      = eingabe.HeimToreVerlaengerung,
            AuswaertsToreVerlaengerung = eingabe.AuswaertsToreVerlaengerung,
            HeimElfmetertore           = eingabe.HeimElfmetertore,
            AuswaertsElfmetertore      = eingabe.AuswaertsElfmetertore,
            SiegerMannschaftId         = sieger,
            EntscheidungArt            = entscheidungArt
        };

        await spielRepository.ErgebnisSetzenAsync(spielId, ergebnis, ct);

        logger.LogInformation(
            "Ergebnis für Spiel {Id} erfasst: {Heim}:{Auswaerts} ({Art}).",
            spielId, eingabe.HeimToreRegulaer, eingabe.AuswaertsToreRegulaer, entscheidungArt);
    }

    public async Task ErgebnisLoeschenAsync(int spielId, CancellationToken ct = default)
    {
        await spielRepository.ErgebnisLoeschenAsync(spielId, ct);
        logger.LogInformation("Ergebnis für Spiel {Id} gelöscht.", spielId);
    }

    public async Task<int> MehrereErgebnisseLoeschenAsync(IReadOnlyList<int> spielIds, CancellationToken ct = default)
    {
        foreach (var id in spielIds)
            await spielRepository.ErgebnisLoeschenAsync(id, ct);
        logger.LogInformation("{Anzahl} Ergebnis(se) gelöscht.", spielIds.Count);
        return spielIds.Count;
    }

    // Bestimmt Sieger und EndgültigeEntscheidungsArt.
    // Wenn hinspielErgebnis übergeben wird (Rückspiel mit AuswaertstorRegelAktiv), wird das
    // Aggregat und — bei Gleichstand — die Auswärtstore vor dem Entscheidungsverfahren geprüft (AD-001).
    private static (int? sieger, EntscheidungArt art) BestimmeSieger(
        int heimId, int auswaertsId,
        ErgebnisErfassenEingabe e,
        SpielErgebnis? hinspielErgebnis)
    {
        if (hinspielErgebnis is not null)
        {
            // Rückspiel:  HeimId = Team B (war Auswärts im Hinspiel)
            //             AuswaertsId = Team A (war Heim im Hinspiel)
            int teamA_gesamt = hinspielErgebnis.HeimToreRegulaer       + e.AuswaertsToreRegulaer;
            int teamB_gesamt = hinspielErgebnis.AuswaertsToreRegulaer  + e.HeimToreRegulaer;

            if (teamA_gesamt != teamB_gesamt)
                return teamA_gesamt > teamB_gesamt
                    ? (auswaertsId, EntscheidungArt.RegulaereSpielzeit)
                    : (heimId,      EntscheidungArt.RegulaereSpielzeit);

            // Aggregat gleich → Auswärtstore vergleichen
            int teamA_weg = e.AuswaertsToreRegulaer;               // Team A als Auswärts im Rückspiel
            int teamB_weg = hinspielErgebnis.AuswaertsToreRegulaer; // Team B als Auswärts im Hinspiel

            if (teamA_weg != teamB_weg)
                return teamA_weg > teamB_weg
                    ? (auswaertsId, EntscheidungArt.Auswaertstorregel)
                    : (heimId,      EntscheidungArt.Auswaertstorregel);

            // Auch Auswärtstore gleich → Entscheidungsverfahren (Verlängerung, Elfmeter…)
        }

        return (SiegerAbleiten(heimId, auswaertsId, e), e.EntscheidungArt);
    }

    private static int? SiegerAbleiten(int heimId, int auswaertsId, ErgebnisErfassenEingabe e)
    {
        return e.EntscheidungArt switch
        {
            EntscheidungArt.RegulaereSpielzeit when e.HeimToreRegulaer > e.AuswaertsToreRegulaer
                => heimId,
            EntscheidungArt.RegulaereSpielzeit when e.AuswaertsToreRegulaer > e.HeimToreRegulaer
                => auswaertsId,
            EntscheidungArt.RegulaereSpielzeit
                => null,
            EntscheidungArt.Verlaengerung or EntscheidungArt.GoldenGoal or EntscheidungArt.SilverGoal
                when e.HeimToreVerlaengerung.HasValue && e.AuswaertsToreVerlaengerung.HasValue
                => (e.HeimToreRegulaer + e.HeimToreVerlaengerung.Value) >
                   (e.AuswaertsToreRegulaer + e.AuswaertsToreVerlaengerung.Value)
                    ? heimId : auswaertsId,
            EntscheidungArt.Elfmeterschiessen
                when e.HeimElfmetertore.HasValue && e.AuswaertsElfmetertore.HasValue
                => e.HeimElfmetertore.Value > e.AuswaertsElfmetertore.Value ? heimId : auswaertsId,
            _ => null
        };
    }
}

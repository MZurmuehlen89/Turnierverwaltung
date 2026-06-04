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
        var spiel = await spielRepository.FindenNachIdMitErgebnisAsync(spielId, ct)
            ?? throw new InvalidOperationException($"Spiel {spielId} nicht gefunden.");

        var sieger = SiegerAbleiten(spiel.HeimMannschaftId, spiel.AuswaertsMannschaftId, eingabe);

        var vollEingabe = new SpielErgebnisEingabe(
            spiel.HeimMannschaftId, spiel.AuswaertsMannschaftId,
            eingabe.HeimToreRegulaer, eingabe.AuswaertsToreRegulaer,
            eingabe.HeimToreVerlaengerung, eingabe.AuswaertsToreVerlaengerung,
            eingabe.HeimElfmetertore, eingabe.AuswaertsElfmetertore,
            eingabe.EntscheidungArt, sieger);

        ValidationException.WerfenWennUngueltig(validator.Validieren(vollEingabe));

        var ergebnis = new SpielErgebnis
        {
            HeimToreRegulaer          = eingabe.HeimToreRegulaer,
            AuswaertsToreRegulaer     = eingabe.AuswaertsToreRegulaer,
            HeimToreVerlaengerung     = eingabe.HeimToreVerlaengerung,
            AuswaertsToreVerlaengerung = eingabe.AuswaertsToreVerlaengerung,
            HeimElfmetertore          = eingabe.HeimElfmetertore,
            AuswaertsElfmetertore     = eingabe.AuswaertsElfmetertore,
            SiegerMannschaftId        = sieger,
            EntscheidungArt           = eingabe.EntscheidungArt
        };

        await spielRepository.ErgebnisSetzenAsync(spielId, ergebnis, ct);

        logger.LogInformation(
            "Ergebnis für Spiel {Id} erfasst: {Heim}:{Auswaerts} ({Art}).",
            spielId, eingabe.HeimToreRegulaer, eingabe.AuswaertsToreRegulaer, eingabe.EntscheidungArt);
    }

    public async Task ErgebnisLoeschenAsync(int spielId, CancellationToken ct = default)
    {
        await spielRepository.ErgebnisLoeschenAsync(spielId, ct);
        logger.LogInformation("Ergebnis für Spiel {Id} gelöscht.", spielId);
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

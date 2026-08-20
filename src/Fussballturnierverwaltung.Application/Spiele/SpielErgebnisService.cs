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
    SpielplanService spielplanService,
    ILogger<SpielErgebnisService> logger)
{
    public Task<IReadOnlyList<Spiel>> AlleNachRundeAsync(int turnierRundeId, CancellationToken ct = default)
        => spielRepository.AlleNachRundeAsync(turnierRundeId, ct);

    public Task<Spiel?> FindenNachIdAsync(int spielId, CancellationToken ct = default)
        => spielRepository.FindenNachIdMitErgebnisAsync(spielId, ct);

    public Task<Spiel?> FindenNachIdMitEreignissenAsync(int spielId, CancellationToken ct = default)
        => spielRepository.FindenNachIdMitEreignissenAsync(spielId, ct);

    public async Task ErgebnisErfassenAsync(int spielId, ErgebnisErfassenEingabe eingabe, CancellationToken ct = default)
    {
        var spiel = await spielRepository.FindenNachIdMitRundeUndHinspielAsync(spielId, ct)
            ?? throw new InvalidOperationException($"Spiel {spielId} nicht gefunden.");

        if (spiel.HeimMannschaftId is null || spiel.AuswaertsMannschaftId is null)
            throw new InvalidOperationException(
                "Das Spiel enthält Platzhalter, die noch nicht aufgelöst wurden.");

        // Auswärtstorregel: bei Rückspiel mit aktivierter Regel Aggregat + Auswärtstore prüfen (AD-001)
        SpielErgebnis? hinspielErgebnis = spiel.IstRueckspiel
            && spiel.TurnierRunde.AuswaertstorRegelAktiv
            && spiel.Hinspiel?.Ergebnis is not null
            ? spiel.Hinspiel.Ergebnis
            : null;

        var (sieger, entscheidungArt) = BestimmeSieger(
            spiel.HeimMannschaftId.Value, spiel.AuswaertsMannschaftId.Value, eingabe, hinspielErgebnis);

        var vollEingabe = new SpielErgebnisEingabe(
            spiel.HeimMannschaftId.Value, spiel.AuswaertsMannschaftId.Value,
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

        await PlatzhalterAutomatischAufloesenAsync(spielId, ct);
    }

    /// <summary>
    /// Löst nach jedem gespeicherten Ergebnis automatisch die Platzhalter aller Runden der
    /// Turnierinstanz auf, die dadurch (ganz oder teilweise, Issue #206) ermittelbar geworden sind —
    /// Vorwärts-Gegenstück zur Rückwärts-Kaskade in <see cref="KaskadeAusloesenAsync"/> (Issue #88,
    /// hier #90). Läuft bewusst ohne Vorprüfung "ist die ganze Runde jetzt komplett": eine Runde kann
    /// mehrere unabhängige Gruppen enthalten (z.B. Gruppenphase mit GruppenAnzahl &gt; 1) — eine bereits
    /// fertige Gruppe soll sofort auflösen, auch wenn eine andere Gruppe derselben Runde noch offen ist.
    /// Eine frühere Version prüfte hier "ist die GESAMTE Runde abgeschlossen" und verpasste dadurch genau
    /// diesen Fall (Fund beim manuellen #206-Test). <see cref="SpielplanService.PlatzhalterAufloesenAsync"/>
    /// ist für Runden ohne offene Platzhalter-Spiele bereits günstig (früher Return), daher ist der
    /// Verzicht auf die Vorprüfung unkritisch. Auch von <see cref="Simulation.SimulationsService"/>
    /// genutzt, da simulierte Ergebnisse den Spielrepository-Zugriff direkt nutzen und nicht über
    /// <see cref="ErgebnisErfassenAsync"/> laufen.
    /// </summary>
    public async Task PlatzhalterAutomatischAufloesenAsync(int spielId, CancellationToken ct = default)
    {
        var spiel = await spielRepository.FindenNachIdMitErgebnisAsync(spielId, ct);
        if (spiel is null) return;

        await spielplanService.PlatzhalterAlleRundenAufloesenAsync(spiel.TurnierInstanzId, ct);
    }

    public async Task ErgebnisLoeschenAsync(int spielId, CancellationToken ct = default)
    {
        await spielRepository.ErgebnisLoeschenAsync(spielId, ct);
        logger.LogInformation("Ergebnis für Spiel {Id} gelöscht.", spielId);
        await KaskadeAusloesenAsync([spielId], ct);
    }

    public async Task<int> MehrereErgebnisseLoeschenAsync(IReadOnlyList<int> spielIds, CancellationToken ct = default)
    {
        foreach (var id in spielIds)
            await spielRepository.ErgebnisLoeschenAsync(id, ct);
        logger.LogInformation("{Anzahl} Ergebnis(se) gelöscht.", spielIds.Count);
        await KaskadeAusloesenAsync(spielIds, ct);
        return spielIds.Count;
    }

    /// <summary>
    /// Prüft nach jedem Ergebnis-Reset (einzeln oder Batch, gleich über welchen Aufrufer — WPF oder
    /// Konsole), ob automatisch generierte Folgerunden-Inhalte zurückgenommen oder neu generiert werden
    /// müssen. Zentral hier statt an jeder Aufrufstelle (Issue #125) — betrifft dadurch automatisch auch
    /// die Konsole, die dieselben Methoden nutzt. Zwei Mechanismen: (1) die enge #125-Logik für ohne
    /// Auslosung generierte Zielrunden mit genau zwei Eingangsteams, (2) die breitere #88-Kaskade für über
    /// Auslosung + Platzhalter aufgelöste Folgerunden beliebiger Größe. Läuft mit leerem Ignorier-Set, da
    /// die Löschung zu diesem Zeitpunkt bereits real erfolgt ist. (1) läuft bewusst zweimal — vor UND nach
    /// (2): die #88-Kaskade kann eine Zwischenrunde (z.B. Halbfinale) selbst korrigieren/entwerten, wovon
    /// wiederum eine #125-Zielrunde (z.B. Finale) abhängt — ohne den zweiten Lauf bliebe eine solche
    /// Zielrunde inkonsistent zum tatsächlichen Endergebnis der Kaskade stehen.
    /// </summary>
    private async Task KaskadeAusloesenAsync(IReadOnlyList<int> spielIds, CancellationToken ct)
    {
        var instanzIds = await spielRepository.TurnierInstanzIdsAsync(spielIds, ct);
        foreach (var instanzId in instanzIds)
        {
            await spielplanService.FinaleAutomatischGenerierenWennBereitAsync(instanzId, ct);

            var plan = await spielplanService.PlatzhalterKaskadePlanenAsync(instanzId, new HashSet<int>(), ct);
            await spielplanService.PlatzhalterKaskadeAusfuehrenAsync(plan, ct);

            await spielplanService.FinaleAutomatischGenerierenWennBereitAsync(instanzId, ct);
        }
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

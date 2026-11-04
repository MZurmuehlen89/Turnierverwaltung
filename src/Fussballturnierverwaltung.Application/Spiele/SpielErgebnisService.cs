using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Application.Spiele.Eingaben;
using Fussballturnierverwaltung.Application.Validierung;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Fussballturnierverwaltung.Application.Spiele;

public class SpielErgebnisService(
    ISpielRepository spielRepository,
    SpielErgebnisValidator validator,
    PlatzhalterAufloesung aufloesung,
    IPlatzhalterKaskadeService kaskade,
    ILogger<SpielErgebnisService> logger)
{
    public Task<IReadOnlyList<Spiel>> AlleNachRundeAsync(int turnierRundeId, CancellationToken ct = default)
        => spielRepository.AlleNachRundeAsync(turnierRundeId, ct);

    public Task<IReadOnlyList<Spiel>> AlleNachMannschaftAsync(
        int turnierInstanzId, int mannschaftId, CancellationToken ct = default)
        => spielRepository.AlleNachMannschaftAsync(turnierInstanzId, mannschaftId, ct);

    public Task<IReadOnlyList<Mannschaft>> MannschaftenNachInstanzAsync(int turnierInstanzId, CancellationToken ct = default)
        => spielRepository.MannschaftenNachInstanzAsync(turnierInstanzId, ct);

    public Task<Spiel?> FindenNachIdAsync(int spielId, CancellationToken ct = default)
        => spielRepository.FindenNachIdMitErgebnisAsync(spielId, ct);

    public Task<Spiel?> FindenNachIdMitEreignissenAsync(int spielId, CancellationToken ct = default)
        => spielRepository.FindenNachIdMitEreignissenAsync(spielId, ct);

    /// <param name="platzhalterAufloesen">Issue #386: <c>false</c> nur für Massensimulationen, die die Vorwärts-Auflösung
    /// (<see cref="PlatzhalterAutomatischAufloesenAsync"/>) einmal je Block statt je Spiel selbst anstoßen.</param>
    public async Task ErgebnisErfassenAsync(
        int spielId, ErgebnisErfassenEingabe eingabe, CancellationToken ct = default, bool platzhalterAufloesen = true)
    {
        var spiel = await spielRepository.FindenNachIdMitRundeUndHinspielAsync(spielId, ct)
            ?? throw new InvalidOperationException($"Spiel {spielId} nicht gefunden.");

        if (spiel.HeimMannschaftId is null || spiel.AuswaertsMannschaftId is null)
            throw new InvalidOperationException(
                "Das Spiel enthält Platzhalter, die noch nicht aufgelöst wurden.");

        // Issue #346: in K.-o.-Runden mit Hin-/Rückspiel entscheidet das Rückspiel über das Gesamtergebnis
        // beider Spiele (Auswärtstorregel nur bei aktivem Flag, AD-001) — nicht mehr nur bei aktiver
        // Auswärtstorregel wie zuvor, wodurch ohne Regel schlicht der Rückspielsieger weiterkam.
        bool istKoRueckspiel = HinRueckspielWertung.IstKoRueckspiel(spiel, spiel.TurnierRunde);
        bool istKoHinspiel   = HinRueckspielWertung.IstKoHinspiel(spiel, spiel.TurnierRunde);

        SpielErgebnis? hinspielErgebnis = null;
        if (istKoRueckspiel)
        {
            hinspielErgebnis = spiel.Hinspiel?.Ergebnis
                ?? throw new InvalidOperationException(
                    "Das Hinspiel dieser Paarung hat noch kein Ergebnis. Bitte zuerst das Hinspiel erfassen.");
        }

        var (sieger, entscheidungArt) = SiegerErmittlung.BestimmeSieger(
            spiel.HeimMannschaftId.Value, spiel.AuswaertsMannschaftId.Value, eingabe,
            hinspielErgebnis, spiel.TurnierRunde.AuswaertstorRegelAktiv);

        var vollEingabe = new SpielErgebnisEingabe(
            spiel.HeimMannschaftId.Value, spiel.AuswaertsMannschaftId.Value,
            eingabe.HeimToreRegulaer, eingabe.AuswaertsToreRegulaer,
            eingabe.HeimToreVerlaengerung, eingabe.AuswaertsToreVerlaengerung,
            eingabe.HeimElfmetertore, eingabe.AuswaertsElfmetertore,
            entscheidungArt, sieger,
            eingabe.HalbzeitHeimTore, eingabe.HalbzeitAuswaertsTore,
            IstGesamtwertung: istKoRueckspiel,
            IstKoHinspiel: istKoHinspiel);

        ValidationException.WerfenWennUngueltig(validator.Validieren(vollEingabe));

        var ergebnis = new SpielErgebnis
        {
            HeimToreRegulaer           = eingabe.HeimToreRegulaer,
            AuswaertsToreRegulaer      = eingabe.AuswaertsToreRegulaer,
            HeimToreVerlaengerung      = eingabe.HeimToreVerlaengerung,
            AuswaertsToreVerlaengerung = eingabe.AuswaertsToreVerlaengerung,
            HeimElfmetertore           = eingabe.HeimElfmetertore,
            AuswaertsElfmetertore      = eingabe.AuswaertsElfmetertore,
            HalbzeitHeimTore           = eingabe.HalbzeitHeimTore,
            HalbzeitAuswaertsTore      = eingabe.HalbzeitAuswaertsTore,
            SiegerMannschaftId         = sieger,
            EntscheidungArt            = entscheidungArt
        };

        bool ueberschreibt = spiel.Ergebnis is not null;

        // Ein neues Hinspiel-Ergebnis entzieht einem bereits gespielten Rückspiel die Grundlage seiner
        // Gesamtwertung (Sieger, ggf. Verlängerung) — dessen Ergebnis wird daher zurückgesetzt.
        var rueckspielIds = istKoHinspiel
            ? await RueckspieleMitErgebnisAsync([spiel], ct)
            : [];
        await spielRepository.ErgebnisseLoeschenAsync(rueckspielIds, ct);

        await spielRepository.ErgebnisSetzenAsync(spielId, ergebnis, ct);

        // Ohne Platzhalter-Auflösung ruft nur die Simulation auf — dort je Spiel eine Zeile würde das
        // Ablaufprotokoll (#413) bei einer Massensimulation fluten.
        logger.Log(platzhalterAufloesen ? LogLevel.Information : LogLevel.Debug,
            "Ergebnis für Spiel {Id} erfasst: {Heim}:{Auswaerts} ({Art}).",
            spielId, eingabe.HeimToreRegulaer, eingabe.AuswaertsToreRegulaer, entscheidungArt);

        // Ein überschriebenes Ergebnis kann den Sieger ändern — bereits damit aufgelöste Folgepaarungen
        // korrigiert erst die Rückwärts-Kaskade (Issue #88); die Vorwärts-Auflösung unten füllt nur noch
        // offene Platzhalter (Issue #346-Folgefund: Rückspiel per Elfmeterschießen umentschieden, das
        // Halbfinale behielt den alten Sieger).
        if (ueberschreibt || rueckspielIds.Count > 0)
            await KaskadeAusloesenAsync([spielId, .. rueckspielIds], ct);

        if (platzhalterAufloesen)
            await PlatzhalterAutomatischAufloesenAsync(spielId, ct);
    }

    /// <summary>Issue #346: Rückspiele (mit Ergebnis) der übergebenen K.-o.-Hinspiele — deren Gesamtwertung
    /// hängt am Hinspiel-Ergebnis und muss mit ihm zusammen zurückgesetzt werden.</summary>
    private async Task<IReadOnlyList<int>> RueckspieleMitErgebnisAsync(
        IReadOnlyList<Spiel> hinspiele, CancellationToken ct)
    {
        var ergebnis = new List<int>();
        foreach (var rundeId in hinspiele.Select(s => s.TurnierRundeId).Distinct())
        {
            var hinIds = hinspiele.Where(s => s.TurnierRundeId == rundeId).Select(s => s.Id).ToHashSet();
            ergebnis.AddRange((await spielRepository.AlleNachRundeAsync(rundeId, ct))
                .Where(s => s.IstRueckspiel && s.HinspielId is int h && hinIds.Contains(h) && s.Ergebnis is not null)
                .Select(s => s.Id));
        }
        return ergebnis;
    }

    /// <summary>
    /// Löst nach jedem gespeicherten Ergebnis automatisch die Platzhalter aller Runden der
    /// Turnierinstanz auf, die dadurch (ganz oder teilweise, Issue #206) ermittelbar geworden sind —
    /// Vorwärts-Gegenstück zur Rückwärts-Kaskade in <see cref="KaskadeAusloesenAsync"/> (Issue #88,
    /// hier #90). Läuft bewusst ohne Vorprüfung "ist die ganze Runde jetzt komplett": eine Runde kann
    /// mehrere unabhängige Gruppen enthalten (z.B. Gruppenphase mit GruppenAnzahl &gt; 1) — eine bereits
    /// fertige Gruppe soll sofort auflösen, auch wenn eine andere Gruppe derselben Runde noch offen ist.
    /// Eine frühere Version prüfte hier "ist die GESAMTE Runde abgeschlossen" und verpasste dadurch genau
    /// diesen Fall (Fund beim manuellen #206-Test). <see cref="PlatzhalterAufloesung.RundeAufloesenAsync"/>
    /// ist für Runden ohne offene Platzhalter-Spiele bereits günstig (früher Return), daher ist der
    /// Verzicht auf die Vorprüfung unkritisch. Auch von <see cref="Simulation.SimulationsService"/>
    /// genutzt, da simulierte Ergebnisse den Spielrepository-Zugriff direkt nutzen und nicht über
    /// <see cref="ErgebnisErfassenAsync"/> laufen.
    /// </summary>
    public async Task PlatzhalterAutomatischAufloesenAsync(int spielId, CancellationToken ct = default)
    {
        var spiel = await spielRepository.FindenNachIdMitErgebnisAsync(spielId, ct);
        if (spiel is null) return;

        await aufloesung.AlleRundenAufloesenAsync(spiel.TurnierInstanzId, ct);
    }

    public async Task ErgebnisLoeschenAsync(int spielId, CancellationToken ct = default)
    {
        var ids = await MitAbhaengigenRueckspielenAsync([spielId], ct);
        await spielRepository.ErgebnisseLoeschenAsync(ids, ct);
        logger.LogInformation("Ergebnis für Spiel {Id} gelöscht.", spielId);
        await KaskadeAusloesenAsync(ids, ct);
    }

    public async Task<int> MehrereErgebnisseLoeschenAsync(IReadOnlyList<int> spielIds, CancellationToken ct = default)
    {
        var ids = await MitAbhaengigenRueckspielenAsync(spielIds, ct);
        await spielRepository.ErgebnisseLoeschenAsync(ids, ct);
        logger.LogInformation("{Anzahl} Ergebnis(se) gelöscht.", ids.Count);
        await KaskadeAusloesenAsync(ids, ct);
        return spielIds.Count;
    }

    /// <summary>Ergänzt die übergebenen Spiel-IDs um die Rückspiele (mit Ergebnis) enthaltener
    /// K.-o.-Hinspiele — ohne Hinspiel-Ergebnis ist deren Gesamtwertung hinfällig (Issue #346).</summary>
    private async Task<IReadOnlyList<int>> MitAbhaengigenRueckspielenAsync(
        IReadOnlyList<int> spielIds, CancellationToken ct)
    {
        var hinspiele = new List<Spiel>();
        foreach (var id in spielIds)
        {
            var spiel = await spielRepository.FindenNachIdMitRundeUndHinspielAsync(id, ct);
            if (spiel is not null && HinRueckspielWertung.IstKoHinspiel(spiel, spiel.TurnierRunde))
                hinspiele.Add(spiel);
        }
        if (hinspiele.Count == 0) return spielIds;

        var rueckspiele = await RueckspieleMitErgebnisAsync(hinspiele, ct);
        return [.. spielIds.Concat(rueckspiele).Distinct()];
    }

    /// <summary>
    /// Prüft nach jedem Ergebnis-Reset (einzeln oder Batch, gleich über welchen Aufrufer — WPF oder
    /// Konsole), ob automatisch generierte Folgerunden-Inhalte zurückgenommen oder neu generiert werden
    /// müssen. Zentral hier statt an jeder Aufrufstelle (Issue #125) — betrifft dadurch automatisch auch
    /// die Konsole, die dieselben Methoden nutzt. Delegiert an <see cref="IPlatzhalterKaskadeService.KaskadeAusloesenAsync"/>,
    /// das denselben zentralen Trigger auch für <see cref="Auslosungen.AuslosungService"/> bereitstellt
    /// (Issue #306).
    /// </summary>
    private async Task KaskadeAusloesenAsync(IReadOnlyList<int> spielIds, CancellationToken ct)
    {
        var instanzIds = await spielRepository.TurnierInstanzIdsAsync(spielIds, ct);
        foreach (var instanzId in instanzIds)
            await kaskade.KaskadeAusloesenAsync(instanzId, ct);
    }
}

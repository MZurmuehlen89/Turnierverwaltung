using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Application.Diagnose;
using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Fussballturnierverwaltung.Application.Spielplan;

/// <summary>Kaskaden-Reset für Folgerunden (Issue #88): bringt Spiele, in die schon Mannschaften aus einer
/// Vorrunde eingetragen wurden, wieder in Einklang mit deren aktuellem Stand — etwa nachdem dort ein Ergebnis
/// gelöscht oder der Spielplan ersetzt wurde.</summary>
public class PlatzhalterKaskadeService(
    ITurnierRundeRepository rundeRepository,
    ISpielRepository spielRepository,
    IAuslosungRepository auslosungRepository,
    TabellenZwischenspeicher zwischenspeicher,
    PlatzhalterAufloesung aufloesung,
    SpielplanService spielplanService,
    FinaleAutomatik finaleAutomatik,
    ILogger<PlatzhalterKaskadeService> logger) : IPlatzhalterKaskadeService
{
    // ── Planen ────────────────────────────────────────────────────────────

    /// <summary>
    /// Ermittelt (reine Leseoperation, keine Schreibzugriffe) auf Spiel-Ebene, welche bereits
    /// aufgelösten Platzhalter-Zuordnungen nicht mehr zum aktuellen Stand ihrer Quellrunde(n) passen.
    /// Bewusst pro Spiel statt pro Runde — eine Runde kann mehrere unabhängige Platzhalter-Paarungen
    /// enthalten (z.B. zwei Halbfinal-Spiele aus zwei verschiedenen Gruppen), von denen nur eine
    /// betroffen ist; die andere darf inklusive ihres Ergebnisses nicht angefasst werden.
    /// <paramref name="zusaetzlichIgnorierteSpielIds"/> sind Spiele, deren Ergebnis (real oder nur
    /// hypothetisch für eine Vorschau) als bereits entfernt gilt. Runden werden in aufsteigender
    /// <see cref="TurnierRunde.Reihenfolge"/> geprüft — eine Quellrunde hat in diesem Domain-Modell
    /// immer eine niedrigere Reihenfolge als ihre Zielrunde, ein einziger Durchlauf reicht.
    /// </summary>
    public Task<KaskadenPlan> PlanenAsync(
        int turnierInstanzId, IReadOnlySet<int> zusaetzlichIgnorierteSpielIds, CancellationToken ct = default)
        => zwischenspeicher.DurchlaufAsync(() => ImDurchlaufPlanenAsync(turnierInstanzId, zusaetzlichIgnorierteSpielIds, ct));

    /// <summary>Der entstehende Plan. Ein Spiel, dessen Ergebnis der Plan löscht, gilt für alle späteren Runden
    /// als <see cref="Ignoriert"/> — so setzt sich die Kaskade über mehrere Runden fort.</summary>
    private sealed record Planung(IReadOnlyList<TurnierRunde> AlleRunden, HashSet<int> Ignoriert)
    {
        public List<KaskadenSchritt> Schritte { get; } = [];
    }

    private async Task<KaskadenPlan> ImDurchlaufPlanenAsync(
        int turnierInstanzId, IReadOnlySet<int> zusaetzlichIgnorierteSpielIds, CancellationToken ct)
    {
        var alleRunden = (await rundeRepository.AlleNachInstanzAsync(turnierInstanzId, ct))
            .OrderBy(r => r.Reihenfolge)
            .ToList();

        var planung = new Planung(alleRunden, [.. zusaetzlichIgnorierteSpielIds]);
        foreach (var runde in alleRunden)
            await RundePruefenAsync(runde, planung, ct);

        return new KaskadenPlan(planung.Schritte);
    }

    private async Task RundePruefenAsync(TurnierRunde runde, Planung planung, CancellationToken ct)
    {
        var auslosung = await auslosungRepository.FindenAbgeschlosseneNachRundeAsync(runde.TurnierInstanzId, runde.Id, ct);
        if (auslosung is null) return;

        // IstFreilos ausgeschlossen: die Generierung erzeugt dafür kein Spiel — ohne denselben Filter hier wäre
        // die positionale Zuordnung in PaarungenPruefenAsync um ein Freilos verschoben.
        var platzhalterEintraege = auslosung.Ergebnisse
            .Where(e => !e.IstFreilos && (e.HeimPlatzhalterName is not null || e.AuswaertsPlatzhalterName is not null))
            .OrderBy(e => e.PaarungsNummer)
            .ToList();
        if (platzhalterEintraege.Count == 0) return;

        var spiele = await zwischenspeicher.RundenSpieleAsync(runde.Id, ct);

        // Eine Auslosung-basierte Platzhalter-Runde ohne ein einziges Spiel muss komplett neu generiert
        // werden — z.B. wenn die FinaleAutomatik (#125) das einzige, noch nicht gespielte Spiel bereits entfernt
        // hat, weil ihre eigene (Auslosung-unabhängige) Heuristik die Quelle als nicht mehr entschieden einstuft,
        // aber mangels erkanntem Nachfolger nicht selbst neu generiert. Kein Ergebnisverlust möglich, da nichts
        // vorhanden ist.
        if (spiele.Count == 0)
        {
            planung.Schritte.Add(new KaskadenRundeNeuGenerierenSchritt(runde.Id, runde.Name));
            return;
        }

        // Issue #395: Gruppen-Auslosungen haben einen Eintrag pro Team, nicht pro Spiel — die
        // Eintrag-zu-Spiel-Zuordnung der Paarungen passt dort nicht.
        if (runde.Rundenart == Rundenart.Gruppenphase)
            await GruppenphasePruefenAsync(runde, auslosung, spiele, planung, ct);
        else
            await PaarungenPruefenAsync(runde, platzhalterEintraege, spiele, planung, ct);
    }

    /// <summary>Baut den Soll-Spielplan mit demselben Berger-Plan wie bei der Generierung nach und vergleicht
    /// Spiel für Spiel (Issue #395).</summary>
    private async Task GruppenphasePruefenAsync(
        TurnierRunde runde, Auslosung auslosung, IReadOnlyList<Spiel> spiele, Planung planung, CancellationToken ct)
    {
        var geplant = GruppenSpielplan.Planen(GruppenSpielplan.AusAuslosung(auslosung.Ergebnisse), runde.MitRueckspiel);
        var spieleNachNummer = spiele.OrderBy(s => s.Spielnummer).ToList();
        if (!GruppenSpielplan.PasstZu(geplant, spieleNachNummer))
        {
            // Unbekannte Struktur (z.B. vor #395 ohne Platzhalter generierte gemischte Gruppe) — lieber
            // nicht anfassen als falsche Spiele zurücksetzen.
            logger.LogWarning(
                "Kaskade: Spielplan der Gruppenphase '{Runde}' passt nicht zur Auslosung, Runde wird übersprungen.",
                runde.Name);
            return;
        }

        for (int i = 0; i < geplant.Count; i++)
        {
            var (heim, auswaerts) = (geplant[i].Heim, geplant[i].Auswaerts);
            var sollHeim      = heim.MannschaftId      ?? await SollAsync(heim.PlatzhalterName, runde, planung, ct);
            var sollAuswaerts = auswaerts.MannschaftId ?? await SollAsync(auswaerts.PlatzhalterName, runde, planung, ct);

            SpielPruefen(spieleNachNummer[i], runde, new Soll(sollHeim, sollAuswaerts, heim.PlatzhalterName, auswaerts.PlatzhalterName), planung);
        }
    }

    /// <summary>Positionale Zuordnung Auslosungs-Eintrag ↔ Spiel — exakt die Reihenfolge, in der die Generierung
    /// die Spiele aus den Platzhalter-Paarungen erzeugt hat.</summary>
    private async Task PaarungenPruefenAsync(
        TurnierRunde runde, IReadOnlyList<AuslosungsErgebnis> platzhalterEintraege, IReadOnlyList<Spiel> spiele,
        Planung planung, CancellationToken ct)
    {
        // Issue #346: in K.o.-Runden mit Rückspiel gehört zu jedem Auslosungs-Eintrag ein Hinspiel plus
        // dessen gespiegeltes Rückspiel — positional gezählt wird nur über die Hinspiele, sonst
        // verrutscht die Zuordnung ab der zweiten Paarung.
        bool paarweise = runde.MitRueckspiel &&
            runde.Rundenart is Rundenart.KoRunde or Rundenart.Finale or Rundenart.SpielUmPlatzDrei;
        var rueckspielJeHinspiel = paarweise ? KoPaarungen.RueckspielJeHinspiel(spiele) : [];
        var paarungsSpiele = spiele
            .Where(s => !paarweise || !s.IstRueckspiel)
            .OrderBy(s => s.Spielnummer)
            .ToList();

        for (int i = 0; i < platzhalterEintraege.Count && i < paarungsSpiele.Count; i++)
        {
            var eintrag = platzhalterEintraege[i];
            var spiel   = paarungsSpiele[i];

            var sollHeim = eintrag.HeimPlatzhalterName is { } heimName
                ? await SollAsync(heimName, runde, planung, ct)
                : eintrag.HeimMannschaftId;
            var sollAuswaerts = eintrag.AuswaertsPlatzhalterName is { } auswaertsName
                ? await SollAsync(auswaertsName, runde, planung, ct)
                : eintrag.AuswaertsMannschaftId;

            var soll = new Soll(sollHeim, sollAuswaerts, eintrag.HeimPlatzhalterName, eintrag.AuswaertsPlatzhalterName);
            SpielPruefen(spiel, runde, soll, planung);

            // Rückspiel mit vertauschten Rollen
            if (rueckspielJeHinspiel.GetValueOrDefault(spiel.Id) is { } rueckspiel)
                SpielPruefen(rueckspiel, runde, soll.Gespiegelt(), planung);
        }
    }

    /// <summary>Welche Mannschaft nach dem geplanten Stand hinter dem Platzhalter stehen müsste.</summary>
    private async Task<int?> SollAsync(string? platzhalterName, TurnierRunde runde, Planung planung, CancellationToken ct) =>
        platzhalterName is null
            ? null
            : await aufloesung.NameAufloesenAsync(platzhalterName, runde, planung.AlleRunden, planung.Ignoriert, ct);

    /// <summary>Welche Mannschaften nach dem geplanten Stand in einem Spiel stehen müssten, mit den
    /// Platzhalternamen beider Seiten.</summary>
    private sealed record Soll(int? Heim, int? Auswaerts, string? HeimName, string? AuswaertsName)
    {
        /// <summary>Das Soll des Rückspiels: Heim und Auswärts getauscht.</summary>
#pragma warning disable S2234 // Rückspiel: Heim und Auswärts bewusst getauscht
        public Soll Gespiegelt() => new(Auswaerts, Heim, AuswaertsName, HeimName);
#pragma warning restore S2234
    }

    private static void SpielPruefen(Spiel spiel, TurnierRunde runde, Soll soll, Planung planung)
    {
        // Nur ein bereits konkret aufgelöster Slot, der nicht mehr zum neu ermittelten Soll-Wert
        // passt, ist ein Kaskaden-Fall — ein noch nie aufgelöster Platzhalter ist normales,
        // ausstehendes Auflösen und kein Rücksetz-Fall.
        bool heimBetroffen      = spiel.HeimMannschaftId.HasValue && spiel.HeimMannschaftId != soll.Heim;
        bool auswaertsBetroffen = spiel.AuswaertsMannschaftId.HasValue && spiel.AuswaertsMannschaftId != soll.Auswaerts;
        if (!heimBetroffen && !auswaertsBetroffen) return;

        bool hatteErgebnis = spiel.Ergebnis is not null;
        planung.Schritte.Add(new KaskadenSpielSchritt(
            spiel.Id, runde.Id, runde.Name, hatteErgebnis,
            soll.Heim, soll.Auswaerts, soll.HeimName, soll.AuswaertsName));

        if (hatteErgebnis) planung.Ignoriert.Add(spiel.Id);
    }

    // ── Ausführen ─────────────────────────────────────────────────────────

    /// <summary>Führt einen zuvor mit <see cref="PlanenAsync"/> berechneten Plan aus.
    /// Betroffene Einzelspiele werden gezielt korrigiert (Ergebnis ggf. löschen, dann entweder direkt auf
    /// die neu ermittelten Teams setzen oder — falls noch nicht vollständig auflösbar — auf Platzhalter
    /// zurücksetzen), ohne andere, nicht betroffene Spiele derselben Runde anzufassen. Nur echt leere
    /// Runden werden komplett neu aus ihrer Auslosung generiert.</summary>
    public async Task AusfuehrenAsync(KaskadenPlan plan, CancellationToken ct = default)
    {
        // Spielkorrekturen gebündelt und vor den Neu-Generierungen (#411/#414, vorher zwei Datenbankzugriffe je
        // Spiel): ihre Soll-Werte stehen im Plan fest, und eine neu generierte Runde liest beim Auflösen ihrer
        // Platzhalter den Stand der Vorrunden — der muss dann schon korrigiert sein.
        await SpieleKorrigierenAsync([.. plan.Schritte.OfType<KaskadenSpielSchritt>()], ct);

        foreach (var schritt in plan.Schritte.OfType<KaskadenRundeNeuGenerierenSchritt>())
            await RundeNeuGenerierenAsync(schritt, ct);
    }

    private async Task SpieleKorrigierenAsync(IReadOnlyList<KaskadenSpielSchritt> schritte, CancellationToken ct)
    {
        if (schritte.Count == 0) return;

        await spielRepository.ErgebnisseLoeschenAsync(
            [.. schritte.Where(s => s.HatteErgebnis).Select(s => s.SpielId)], ct);
        await spielRepository.PlatzhalterKorrigierenAsync(
            [.. schritte.Select(s => new PlatzhalterKorrektur(
                s.SpielId,
                s.NeuesHeimMannschaftId, s.HeimPlatzhalterName,
                s.NeuesAuswaertsMannschaftId, s.AuswaertsPlatzhalterName))],
            ct);

        foreach (var rundenSchritte in schritte.GroupBy(s => s.RundeName))
            logger.LogInformation(
                "{Anzahl} Spiel(e) in Runde '{Runde}' im Rahmen der Kaskade korrigiert.",
                rundenSchritte.Count(), rundenSchritte.Key);
    }

    private async Task RundeNeuGenerierenAsync(KaskadenRundeNeuGenerierenSchritt schritt, CancellationToken ct)
    {
        var runde = await rundeRepository.FindenNachIdMitRegelwerkAsync(schritt.RundeId, ct);
        if (runde is null) return;

        var auslosung = await auslosungRepository.FindenAbgeschlosseneNachRundeAsync(
            runde.TurnierInstanzId, runde.Id, ct);
        if (auslosung is null) return;

        await spielplanService.GenerierenAusAuslosungAsync(schritt.RundeId, [.. auslosung.Ergebnisse], ct);

        logger.LogInformation(
            "Runde '{Runde}' im Rahmen der Kaskade neu generiert.", schritt.RundeName);
    }

    // ── Auslösen ──────────────────────────────────────────────────────────

    /// <summary>Kombiniert Rückwärts-Kaskade (<see cref="PlanenAsync"/>/<see cref="AusfuehrenAsync"/>, Issue #88)
    /// und automatische Finale-Generierung (<see cref="FinaleAutomatik"/>, Issue #113) zu einem einzigen Aufruf.
    /// Zentraler Trigger-Punkt für jede Stelle, die den Stand einer Turnierinstanz nachträglich ändert und
    /// dadurch Folgerunden ungültig machen kann — <see cref="Spiele.SpielErgebnisService"/> (Ergebnis-Löschen)
    /// und <see cref="AuslosungService"/> (Auslosung ausführen/zurücksetzen, Issue #306). Idempotent und
    /// günstig, wenn nichts betroffen ist.</summary>
    public async Task KaskadeAusloesenAsync(int turnierInstanzId, CancellationToken ct = default)
    {
        using var vorgang = logger.Vorgang($"Kaskade (Instanz {turnierInstanzId})");

        await finaleAutomatik.AbgleichenAsync(turnierInstanzId, ct);

        var plan = await PlanenAsync(turnierInstanzId, new HashSet<int>(), ct);
        await AusfuehrenAsync(plan, ct);

        await finaleAutomatik.AbgleichenAsync(turnierInstanzId, ct);
    }

    public Task RundenSpielplanOhneKaskadeLoeschenAsync(int turnierRundeId, CancellationToken ct = default)
        => spielplanService.RundenSpielplanOhneKaskadeLoeschenAsync(turnierRundeId, ct);

    /// <summary>Issue #411: Spielplan einer Runde in einem Zug ersetzen — alten Spielplan kaskadenlos löschen, aus
    /// den Auslosungsergebnissen neu generieren, dann einmal die Kaskade für die Folgerunden. Vorher lief die
    /// Kaskade schon nach dem Abschließen der Auslosung und generierte eine Platzhalter-Runde dabei selbst
    /// (leere Runde mit abgeschlossener Auslosung), woraufhin der Aufrufer löschte, erneut generierte und die
    /// Kaskade ein zweites Mal auslöste. Die Kaskade läuft auch, wenn die Generierung scheitert: Folgerunden
    /// dürfen nicht auf dem gerade gelöschten Spielplan stehen bleiben (#306).</summary>
    public async Task RundenSpielplanAusAuslosungErsetzenAsync(
        int turnierRundeId, IReadOnlyList<AuslosungsErgebnis> ergebnisse, CancellationToken ct = default)
    {
        using var vorgang = logger.Vorgang($"Spielplan aus Auslosung ersetzen (Runde {turnierRundeId})");

        var runde = await spielplanService.RundenSpielplanOhneKaskadeLoeschenAsync(turnierRundeId, ct);
        try
        {
            await spielplanService.GenerierenAusAuslosungAsync(turnierRundeId, ergebnisse, ct);
        }
        finally
        {
            await KaskadeAusloesenAsync(runde.TurnierInstanzId, ct);
        }
    }
}

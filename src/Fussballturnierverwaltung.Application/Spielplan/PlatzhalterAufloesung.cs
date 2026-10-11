using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Application.Tabelle;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Fussballturnierverwaltung.Application.Spielplan;

/// <summary>Ermittelt, welche Mannschaft hinter einem Platzhalter wie "Sieger Gruppe A" oder hinter der
/// Platzierung einer <see cref="Uebergangsregel"/> steht, und trägt sie in die Spiele ein, sobald sie feststeht.</summary>
public class PlatzhalterAufloesung(
    ITurnierRundeRepository rundeRepository,
    ISpielRepository spielRepository,
    IMannschaftRepository mannschaftRepository,
    TabellenService tabellenService,
    TabellenZwischenspeicher zwischenspeicher,
    KoQuellverknuepfung quellverknuepfung,
    ILogger<PlatzhalterAufloesung> logger)
{
    /// <summary>Die Runde, deren Platzhalter aufgelöst werden — Quellrunden liegen immer davor.</summary>
    private sealed record Zielrunde(TurnierRunde Runde, IReadOnlyList<TurnierRunde> AlleRunden);

    /// <summary>Auf welchen Stand der Quellrunden sich die Auflösung stützt. <see cref="IgnorierteSpielIds"/>
    /// sind Spiele, deren Ergebnis als bereits entfernt gilt (Kaskade, Issue #88). Ohne
    /// <see cref="NurSicherPositioniert"/> zählt der aktuelle Tabellenstand, auch wenn er sich noch ändern kann
    /// (Vorschau, Issue #208).</summary>
    private sealed record Sicht(IReadOnlySet<int>? IgnorierteSpielIds = null, bool NurSicherPositioniert = true)
    {
        public static readonly Sicht Sicher = new();
        public static readonly Sicht Vorschau = new(NurSicherPositioniert: false);
    }

    /// <summary>Die Spiele einer Runde mit mindestens einer offenen Seite. Jeder Platzhaltername und jedes
    /// Quellspiel wird je Runde nur einmal aufgelöst.</summary>
    private sealed record OffeneRunde(Zielrunde Ziel, Sicht Sicht, IReadOnlyList<Spiel> Spiele)
    {
        public Dictionary<string, int?> MannschaftJeName { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<int, Spiel?> QuellSpiele { get; } = [];
    }

    // ── Runden auflösen ───────────────────────────────────────────────────

    /// <summary>
    /// Löst die Platzhalter einer Runde auf: sucht in Vorrunden-Tabellen und K.o.-Ergebnissen nach den realen
    /// Teams und aktualisiert die Spiele. Gibt die Anzahl aufgelöster Spiele zurück.
    /// </summary>
    public Task<int> RundeAufloesenAsync(int turnierRundeId, CancellationToken ct = default)
        => zwischenspeicher.DurchlaufAsync(() => RundeImDurchlaufAufloesenAsync(turnierRundeId, ct));

    private async Task<int> RundeImDurchlaufAufloesenAsync(int turnierRundeId, CancellationToken ct)
    {
        // Aufschlüsselung für das Ablaufprotokoll (#414): Laden der Runde und ihrer Spiele, Auflösen der Namen
        // (Tabellen und Quellspiele), Speichern — als Werte in der Abschlusszeile statt eigener Vorgänge, weil
        // die Auflösung nach jedem Ergebnis für jede Runde läuft.
        var uhr = Stopwatch.StartNew();

        var offene = await OffeneRundeLadenAsync(turnierRundeId, Sicht.Sicher, ct);
        if (offene.Spiele.Count == 0) return 0;
        long ladenMs = uhr.ElapsedMilliseconds;

        // Gesammelt und am Ende in einem Zug gespeichert (#411) — die Auflösung liest nur Quellrunden, keine
        // Korrektur hängt von einer anderen derselben Runde ab. Für eine noch nicht auflösbare Seite wird der
        // unveränderte Platzhaltertext zurückgeschrieben.
        var korrekturen = (await OffeneSeitenAufloesenAsync(offene, ct))
            .Select(a => new PlatzhalterKorrektur(
                a.Spiel.Id,
                a.Spiel.HeimMannschaftId ?? a.Heim, a.Spiel.HeimPlatzhalterName,
                a.Spiel.AuswaertsMannschaftId ?? a.Auswaerts, a.Spiel.AuswaertsPlatzhalterName))
            .ToList();
        long aufloesenMs = uhr.ElapsedMilliseconds - ladenMs;

        if (korrekturen.Count > 0)
        {
            await spielRepository.PlatzhalterKorrigierenAsync(korrekturen, ct);
            zwischenspeicher.Verwerfen(turnierRundeId);
        }
        long speichernMs = uhr.ElapsedMilliseconds - ladenMs - aufloesenMs;

        logger.LogInformation(
            "Platzhalter in Runde '{Runde}' aufgelöst: {Aufgeloest}/{Gesamt} Spiele " +
            "(Laden {Laden} ms, Auflösen {Aufloesen} ms, Speichern {Speichern} ms).",
            offene.Ziel.Runde.Name, korrekturen.Count, offene.Spiele.Count, ladenMs, aufloesenMs, speichernMs);

        return korrekturen.Count;
    }

    /// <summary>Löst die Platzhalter aller Runden der Turnierinstanz auf (Issue #90, Vorwärts-Gegenstück
    /// zur Rückwärts-Kaskade aus #88). Idempotent und günstig: eine Runde ohne offene Spiele kostet nur das
    /// Laden ihrer Spiele. Gibt die Gesamtanzahl neu aufgelöster Spiele zurück.</summary>
    public Task<int> AlleRundenAufloesenAsync(int turnierInstanzId, CancellationToken ct = default)
        => zwischenspeicher.DurchlaufAsync(() => AlleRundenImDurchlaufAufloesenAsync(turnierInstanzId, ct));

    private async Task<int> AlleRundenImDurchlaufAufloesenAsync(int turnierInstanzId, CancellationToken ct)
    {
        await quellverknuepfung.AlleRundenVerknuepfenAsync(turnierInstanzId, ct);

        var runden = await rundeRepository.AlleNachInstanzAsync(turnierInstanzId, ct);
        int gesamt = 0;
        foreach (var runde in runden.OrderBy(r => r.Reihenfolge))
            gesamt += await RundeImDurchlaufAufloesenAsync(runde.Id, ct);
        return gesamt;
    }

    /// <summary>Rein lesende Vorschau (Issue #208, Ergänzung zu #202): welcher Mannschaftsname würde nach
    /// dem aktuellen, noch nicht zwingend sicheren Tabellenstand jede noch offene Platzhalter-Seite einer
    /// Runde besetzen? Schreibt nichts in die Datenbank. Liefert nur Einträge für Spiele, bei denen
    /// mindestens eine Seite ermittelbar ist; bereits real zugewiesene Seiten liefern keinen Vorschau-Wert
    /// (null).</summary>
    public Task<IReadOnlyDictionary<int, (string? Heim, string? Auswaerts)>> VorschauAsync(
        int turnierRundeId, CancellationToken ct = default)
        => zwischenspeicher.DurchlaufAsync(() => VorschauImDurchlaufAsync(turnierRundeId, ct));

    private async Task<IReadOnlyDictionary<int, (string? Heim, string? Auswaerts)>> VorschauImDurchlaufAsync(
        int turnierRundeId, CancellationToken ct)
    {
        var ergebnis = new Dictionary<int, (string? Heim, string? Auswaerts)>();

        var offene = await OffeneRundeLadenAsync(turnierRundeId, Sicht.Vorschau, ct);
        var aufgeloest = await OffeneSeitenAufloesenAsync(offene, ct);
        if (aufgeloest.Count == 0) return ergebnis;

        var teamIds = aufgeloest.SelectMany(a => new[] { a.Heim, a.Auswaerts })
            .Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
        var teams = (await mannschaftRepository.FindenNachIdsAsync(teamIds, ct)).ToDictionary(m => m.Id, m => m.Name);

        foreach (var (spiel, heimId, auswaertsId) in aufgeloest)
            ergebnis[spiel.Id] = (
                heimId.HasValue ? teams.GetValueOrDefault(heimId.Value) : null,
                auswaertsId.HasValue ? teams.GetValueOrDefault(auswaertsId.Value) : null);

        return ergebnis;
    }

    private async Task<OffeneRunde> OffeneRundeLadenAsync(int turnierRundeId, Sicht sicht, CancellationToken ct)
    {
        var runde = await rundeRepository.FindenNachIdMitRegelwerkAsync(turnierRundeId, ct)
            ?? throw new InvalidOperationException($"Runde mit ID {turnierRundeId} nicht gefunden.");

        // Alle Runden derselben TurnierInstanz laden (für Suche nach Vorrunden)
        var alleRunden = await rundeRepository.AlleNachInstanzAsync(runde.TurnierInstanzId, ct);

        var offeneSpiele = (await spielRepository.AlleNachRundeAsync(turnierRundeId, ct))
            .Where(s => s.HeimMannschaftId is null || s.AuswaertsMannschaftId is null)
            .ToList();

        return new OffeneRunde(new Zielrunde(runde, alleRunden), sicht, offeneSpiele);
    }

    /// <summary>Issue #206: jede Seite unabhängig auflösen (TV-Präsentation-Stil) — eine Seite kann schon
    /// feststehen, während die andere noch als Platzhaltertext offen bleibt. Liefert die Spiele mit mindestens
    /// einer jetzt ermittelbaren Seite; eine bereits besetzte oder nicht ermittelbare Seite ist <c>null</c>.</summary>
    private async Task<List<(Spiel Spiel, int? Heim, int? Auswaerts)>> OffeneSeitenAufloesenAsync(
        OffeneRunde offene, CancellationToken ct)
    {
        var aufgeloest = new List<(Spiel Spiel, int? Heim, int? Auswaerts)>();
        foreach (var spiel in offene.Spiele)
        {
            int? heim = spiel.HeimMannschaftId is null
                ? await SeiteAufloesenAsync(spiel.HeimQuellSpielId, spiel.HeimQuellErgebnis, spiel.HeimPlatzhalterName, offene, ct)
                : null;
            int? auswaerts = spiel.AuswaertsMannschaftId is null
                ? await SeiteAufloesenAsync(spiel.AuswaertsQuellSpielId, spiel.AuswaertsQuellErgebnis, spiel.AuswaertsPlatzhalterName, offene, ct)
                : null;

            if (heim is not null || auswaerts is not null) aufgeloest.Add((spiel, heim, auswaerts));
        }
        return aufgeloest;
    }

    // Issue #241: eine gesetzte Quell-ID (K.o.-zu-K.o.) geht direkt, ohne Namenserkennung, vor.
    private async Task<int?> SeiteAufloesenAsync(
        int? quellSpielId, SpielQuellErgebnisTyp? quellTyp, string? platzhalterName, OffeneRunde offene, CancellationToken ct)
        => quellSpielId is int id && quellTyp is { } typ
            ? await QuellSpielAufloesenAsync(id, typ, offene, ct)
            : await NameEinmalAufloesenAsync(platzhalterName, offene, ct);

    /// <summary>Löst eine K.o.-Quellverknüpfung (<see cref="Spiel.HeimQuellSpielId"/>/-<c>Ergebnis</c>) direkt
    /// zur Mannschafts-ID auf — Gegenstück zu <see cref="KoSpielRangAsync"/>, aber ohne dessen Filter auf
    /// bereits besetzte Spiele, da die Quell-ID schon eindeutig auf ein konkretes Spiel zeigt.</summary>
    private async Task<int?> QuellSpielAufloesenAsync(
        int quellSpielId, SpielQuellErgebnisTyp typ, OffeneRunde offene, CancellationToken ct)
    {
        if (!offene.QuellSpiele.TryGetValue(quellSpielId, out var quellSpiel))
        {
            quellSpiel = await spielRepository.FindenNachIdMitErgebnisAsync(quellSpielId, ct);
            offene.QuellSpiele[quellSpielId] = quellSpiel;
        }
        if (quellSpiel is null) return null;

        return KoPaarungen.SiegerOderVerliererId(quellSpiel, typ == SpielQuellErgebnisTyp.Sieger ? 1 : 2);
    }

    private async Task<int?> NameEinmalAufloesenAsync(string? name, OffeneRunde offene, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        if (offene.MannschaftJeName.TryGetValue(name, out var gemerkt)) return gemerkt;

        var mannschaftId = await NameAufloesenAsync(name, offene.Ziel, offene.Sicht, ct);
        offene.MannschaftJeName[name] = mannschaftId;
        return mannschaftId;
    }

    // ── Platzhalternamen auflösen ─────────────────────────────────────────

    /// <summary>Welche Mannschaft steht hinter <paramref name="name"/> in einem Spiel von
    /// <paramref name="runde"/>, wenn die Ergebnisse von <paramref name="ignorierteSpielIds"/> als entfernt
    /// gelten? Für die Kaskade (Issue #88), die damit den Soll-Stand einer Folgerunde ermittelt.</summary>
    public Task<int?> NameAufloesenAsync(
        string name, TurnierRunde runde, IReadOnlyList<TurnierRunde> alleRunden,
        IReadOnlySet<int> ignorierteSpielIds, CancellationToken ct)
        => NameAufloesenAsync(name, new Zielrunde(runde, alleRunden), new Sicht(ignorierteSpielIds), ct);

    private async Task<int?> NameAufloesenAsync(string name, Zielrunde ziel, Sicht sicht, CancellationToken ct) =>
        PlatzhalterNamen.Erkennen(name) switch
        {
            PlatzhalterBezug.GruppenRang gruppenRang => await GruppenRangAufloesenAsync(gruppenRang, ziel, sicht, ct),
            PlatzhalterBezug.KombinationsPlatz platz => await KombinationsPlatzAufloesenAsync(name, platz, ziel, sicht, ct),
            PlatzhalterBezug.BestePlatzierung beste  => await BestePlatzierungAufloesenAsync(beste, ziel, sicht, ct),
            PlatzhalterBezug.KoSpiel koSpiel         => await KoSpielAufloesenAsync(koSpiel, ziel, sicht, ct),
            PlatzhalterBezug.TabellenPlatz platz     => await TabellenPlatzAufloesenAsync(platz, ziel, sicht, ct),
            _ => null,
        };

    // Bei mehreren Gruppenphase-Runden im selben Turnier (z.B. "Gruppenphase 1" + "Gruppenphase 2") ist die
    // nächstgelegene VORHERGEHENDE Gruppenphase gemeint, nicht immer die erste.
    private static TurnierRunde? VorhergehendeGruppenphase(Zielrunde ziel) =>
        ziel.AlleRunden
            .Where(r => r.Rundenart == Rundenart.Gruppenphase && r.Reihenfolge < ziel.Runde.Reihenfolge)
            .OrderByDescending(r => r.Reihenfolge)
            .FirstOrDefault();

    private async Task<int?> GruppenRangAufloesenAsync(
        PlatzhalterBezug.GruppenRang bezug, Zielrunde ziel, Sicht sicht, CancellationToken ct)
    {
        // Gruppenkennung → Gruppenindex (A=1, B=2, ... oder direkte Zahl)
        string kennung = bezug.GruppenKennung;
        int gruppenNummer = kennung.Length == 1 && char.IsLetter(kennung[0])
            ? char.ToUpper(kennung[0]) - 'A' + 1
            : int.TryParse(kennung, out int n) ? n : 0;
        if (gruppenNummer <= 0) return null;

        var gruppenphase = VorhergehendeGruppenphase(ziel);
        if (gruppenphase is null) return null;

        return await RangAsync(gruppenphase, gruppenNummer, bezug.Rang, sicht, ct);
    }

    /// <summary>Issue #372: löst einen Platz wie "3. Gruppe A/D/E/F" über die Kombinationstabelle der Zielrunde
    /// auf — erst die besten Dritten über alle Gruppen ermitteln, deren Gruppenbuchstaben als Schlüssel
    /// nachschlagen, dann den Dritten der dort eingetragenen Gruppe liefern. Wie bei
    /// <see cref="BestePlatzierungAufloesenAsync"/> erst nach vollständig gespielter Gruppenphase: vorher
    /// steht die Menge der qualifizierten Gruppen nicht fest.</summary>
    private async Task<int?> KombinationsPlatzAufloesenAsync(
        string name, PlatzhalterBezug.KombinationsPlatz bezug, Zielrunde ziel, Sicht sicht, CancellationToken ct)
    {
        if (Kombinationstabelle.AusJson(ziel.Runde.KombinationstabelleJson) is not { AnzahlBeste: > 0 } tabelle) return null;

        var gruppenphase = VorhergehendeGruppenphase(ziel);
        if (gruppenphase is null) return null;

        if (await zwischenspeicher.EnthaeltIgnoriertesSpielAsync(gruppenphase.Id, sicht.IgnorierteSpielIds, ct)) return null;
        var spiele = await zwischenspeicher.RundenSpieleAsync(gruppenphase.Id, ct);
        if (spiele.Count == 0 || spiele.Any(s => s.Status != SpielStatus.Abgeschlossen))
            return null;

        var rangfolge = await tabellenService.UebergreifendeRangfolgeBerechnenAsync(gruppenphase.Id, bezug.Platzierung, ct);
        if (rangfolge.Count < tabelle.AnzahlBeste) return null;
        var besteIds = rangfolge.Take(tabelle.AnzahlBeste).Select(e => e.Mannschaft.Id).ToHashSet();

        var gruppenTabellen = await zwischenspeicher.GruppenTabellenAsync(gruppenphase.Id, ct);
        var qualifizierteNachGruppe = new Dictionary<string, int>();
        foreach (var gruppe in gruppenTabellen)
        {
            var mannschaftId = gruppe.Tabelle.FirstOrDefault(e => e.Rang == bezug.Platzierung)?.Mannschaft.Id;
            if (mannschaftId is { } id && besteIds.Contains(id) && gruppe.GruppenNummer is >= 1 and <= 26)
                qualifizierteNachGruppe[((char)('A' + gruppe.GruppenNummer - 1)).ToString()] = id;
        }
        if (qualifizierteNachGruppe.Count != tabelle.AnzahlBeste) return null;

        return tabelle.GruppeFuer(name, qualifizierteNachGruppe.Keys) is { } zielGruppe
            && qualifizierteNachGruppe.TryGetValue(zielGruppe, out var ergebnis)
            ? ergebnis
            : null;
    }

    /// <summary>Löst "N. bester X. Platz" auf (Issue #213): sucht die nächstgelegene vorhergehende
    /// Gruppenphase-Runde, prüft dasselbe konservative All-or-nothing-Gate wie <see cref="UebergangsregelBesteNAufloesenAsync"/>
    /// (alle Spiele der Gruppenphase abgeschlossen), und liefert das N-te Team der über
    /// <see cref="TabellenService.UebergreifendeRangfolgeBerechnenAsync"/> ermittelten Cross-Group-Rangfolge an
    /// Platzierung X. Funktioniert bewusst auch ohne konfigurierte <see cref="Uebergangsregel"/>
    /// (Freitext-Turniere).</summary>
    private async Task<int?> BestePlatzierungAufloesenAsync(
        PlatzhalterBezug.BestePlatzierung bezug, Zielrunde ziel, Sicht sicht, CancellationToken ct)
    {
        var gruppenphase = VorhergehendeGruppenphase(ziel);
        if (gruppenphase is null) return null;

        if (await zwischenspeicher.EnthaeltIgnoriertesSpielAsync(gruppenphase.Id, sicht.IgnorierteSpielIds, ct)) return null;
        if (await HatOffeneBesetzteSpieleAsync(gruppenphase.Id, ct)) return null;

        var rangfolge = await tabellenService.UebergreifendeRangfolgeBerechnenAsync(gruppenphase.Id, bezug.Platzierung, ct);
        return bezug.Rang <= rangfolge.Count ? rangfolge[bezug.Rang - 1].Mannschaft.Id : null;
    }

    private async Task<bool> HatOffeneBesetzteSpieleAsync(int rundeId, CancellationToken ct) =>
        (await zwischenspeicher.RundenSpieleAsync(rundeId, ct))
            .Any(s => s.HeimMannschaftId.HasValue && s.AuswaertsMannschaftId.HasValue && s.Status != SpielStatus.Abgeschlossen);

    private async Task<int?> KoSpielAufloesenAsync(
        PlatzhalterBezug.KoSpiel bezug, Zielrunde ziel, Sicht sicht, CancellationToken ct)
    {
        var koRunde = PlatzhalterNamen.KoQuellrunde(bezug, ziel.Runde, ziel.AlleRunden);
        if (koRunde is null) return null;

        return await RangAsync(koRunde, bezug.SpielNummer, bezug.Verlierer ? 2 : 1, sicht, ct);
    }

    private async Task<int?> TabellenPlatzAufloesenAsync(
        PlatzhalterBezug.TabellenPlatz bezug, Zielrunde ziel, Sicht sicht, CancellationToken ct)
    {
        var quellRunde = ziel.AlleRunden
            .Where(r => r.Reihenfolge < ziel.Runde.Reihenfolge
                     && string.Equals(r.Name, bezug.RundenName, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(r => r.Reihenfolge)
            .FirstOrDefault();
        if (quellRunde is null) return null;

        return await RangAsync(quellRunde, null, bezug.Platz, sicht, ct);
    }

    // ── Regelbasierte Auflösung (Issue #130) ──────────────────────────────

    /// <summary>Löst eine Platzierung einer <see cref="Uebergangsregel"/> zum tatsächlichen Mannschaft-Id auf.
    /// Liefert <c>null</c>, solange die Quellrunde noch nicht (für diese Platzierung) entschieden ist.</summary>
    public Task<int?> UebergangsregelPlatzAufloesenAsync(Uebergangsregel regel, int platzierung, CancellationToken ct = default)
    {
        if (platzierung < regel.PlatzierungVon || platzierung > regel.PlatzierungBis)
            throw new ArgumentOutOfRangeException(nameof(platzierung),
                $"Platzierung {platzierung} liegt außerhalb des Regel-Bereichs {regel.PlatzierungVon}-{regel.PlatzierungBis}.");

        return RangAsync(regel.QuellRunde, regel.GruppeIndex, platzierung, Sicht.Sicher, ct);
    }

    /// <summary>Löst alle Platzierungen einer <see cref="Uebergangsregel"/> auf (Platzierung → Mannschaft-Id oder
    /// <c>null</c>, solange die Quellrunde für diese Platzierung noch nicht entschieden ist). Bei einer
    /// gruppenübergreifenden Rangfolge-Regel (<see cref="Uebergangsregel.AnzahlBeste"/> gesetzt, Issue #184) ist der
    /// Schlüssel der Rang innerhalb des Cross-Group-Pools (1..AnzahlBeste), nicht die absolute Gruppenplatzierung.</summary>
    public async Task<IReadOnlyDictionary<int, int?>> UebergangsregelQualifikantenAsync(
        Uebergangsregel regel, CancellationToken ct = default)
    {
        if (regel.AnzahlBeste is not null)
        {
            var beste = await UebergangsregelBesteNAufloesenAsync(regel, ct);
            return beste.Select((teamId, i) => (Rang: i + 1, TeamId: teamId))
                .ToDictionary(x => x.Rang, x => x.TeamId);
        }

        var ergebnis = new Dictionary<int, int?>();
        for (int platz = regel.PlatzierungVon; platz <= regel.PlatzierungBis; platz++)
            ergebnis[platz] = await RangAsync(regel.QuellRunde, regel.GruppeIndex, platz, Sicht.Sicher, ct);
        return ergebnis;
    }

    /// <summary>Löst eine gruppenübergreifende Rangfolge-Regel (Issue #184) auf: nimmt das Team an
    /// <see cref="Uebergangsregel.PlatzierungVon"/> aus jeder Gruppe der Quellrunde, vergleicht diese Kandidaten
    /// nach den Ranking-Regeln des Regelwerks und liefert die <see cref="Uebergangsregel.AnzahlBeste"/> besten.
    /// Bewusst konservatives Gate: löst erst auf, sobald ALLE Spiele der Quellrunde (alle Gruppen) abgeschlossen
    /// sind — kein Extremrang-artiger Optimistic-Preview wie bei #202/#206, das wäre eine kombinatorische
    /// Erweiterung über Gruppengrenzen hinweg und würde den Aufwand dieses Features deutlich sprengen.</summary>
    public async Task<IReadOnlyList<int?>> UebergangsregelBesteNAufloesenAsync(Uebergangsregel regel, CancellationToken ct = default)
    {
        int n = regel.AnzahlBeste!.Value;
        if (await HatOffeneBesetzteSpieleAsync(regel.QuellRundeId, ct)) return [.. Enumerable.Repeat((int?)null, n)];

        var rangfolge = await tabellenService.UebergreifendeRangfolgeBerechnenAsync(regel.QuellRundeId, regel.PlatzierungVon, ct);
        return Enumerable.Range(0, n)
            .Select(i => i < rangfolge.Count ? (int?)rangfolge[i].Mannschaft.Id : null)
            .ToList();
    }

    // ── Platzierung einer Quellrunde ──────────────────────────────────────

    /// <summary>
    /// Kern-Auflösung "Platzierung X einer Quellrunde → Mannschaft-Id", unabhängig davon, ob die Koordinaten
    /// (Quellrunde/Gruppenindex/Platzierung) aus einer <see cref="Uebergangsregel"/> oder — für per Freitext
    /// angelegte Turniere — aus einem Platzhalternamen stammen. Die Bedeutung von <paramref name="gruppeIndex"/>
    /// hängt von der Rundenart der Quellrunde ab: Gruppenphase → Gruppennummer; Liga/TopfLiga/Schweizer System →
    /// nicht verwendet; K.-o.-Runde/Finale → Spielnummer innerhalb der Runde (Platzierung 1 = Sieger,
    /// 2 = Verlierer).
    /// </summary>
    private Task<int?> RangAsync(TurnierRunde quellRunde, int? gruppeIndex, int platzierung, Sicht sicht, CancellationToken ct) =>
        quellRunde.Rundenart switch
        {
            Rundenart.Gruppenphase
                => GruppenRangAsync(quellRunde, gruppeIndex, platzierung, sicht, ct),
            Rundenart.Liga or Rundenart.TopfLiga or Rundenart.SchweizerSystem
                => TabellenRangAsync(quellRunde, platzierung, sicht, ct),
            Rundenart.KoRunde or Rundenart.Finale or Rundenart.SpielUmPlatzDrei
                => KoSpielRangAsync(quellRunde, gruppeIndex, platzierung, sicht, ct),
            _ => Task.FromResult<int?>(null),
        };

    /// <summary>Tabellenrang in einer Gruppe. Issue #202: der aktuelle Rang allein reicht nicht — er darf durch
    /// kein Ergebnis der noch offenen Spiele mehr verdrängbar sein (<see cref="TabellenEintrag.SicherPositioniert"/>,
    /// siehe Qualifikationsstatus-Ampel in der Gruppentabelle, Issue #70/#201), sonst würde die Auflösung
    /// optimistisch vorgreifen — außer der Aufrufer will genau das als Vorschau (Issue #208).</summary>
    private async Task<int?> GruppenRangAsync(
        TurnierRunde quellRunde, int? gruppenNummer, int platzierung, Sicht sicht, CancellationToken ct)
    {
        if (gruppenNummer is null) return null;
        // Issue #88: sobald ein Ergebnis dieser Gruppe zur Löschung ansteht, gilt sie vorübergehend als
        // nicht ermittelbar. Seit #397 trägt das Spiel seine Gruppennummer — ohne sie (Altdaten) bleibt
        // es bei der ganzen Runde.
        if (await zwischenspeicher.EnthaeltIgnoriertesSpielAsync(quellRunde.Id, sicht.IgnorierteSpielIds, ct, gruppenNummer.Value))
            return null;

        var gruppenTabellen = await zwischenspeicher.GruppenTabellenAsync(quellRunde.Id, ct);
        var gruppe = gruppenTabellen.FirstOrDefault(g => g.GruppenNummer == gruppenNummer.Value);
        return gruppe?.Tabelle.FirstOrDefault(e =>
            e.Rang == platzierung && (!sicht.NurSicherPositioniert || e.SicherPositioniert))?.Mannschaft.Id;
    }

    /// <summary>Tabellenrang einer Liga-, TopfLiga- oder Schweizer-Runde — sie teilen sich eine Gesamttabelle über
    /// alle Spiele der Runde (Issue #25-Nachlieferung). Issue #343: wie bei der Gruppenphase (#202) nur fest
    /// auflösen, wenn der Rang durch die verbleibenden Spiele nicht mehr verdrängbar ist (Punkte-Schranken aus
    /// <see cref="TabellenService.LigaTabelleMitSicherheitBerechnenAsync"/>) — die Vorschau (#208) nutzt den
    /// aktuellen Tabellenstand.</summary>
    private async Task<int?> TabellenRangAsync(TurnierRunde quellRunde, int platzierung, Sicht sicht, CancellationToken ct)
    {
        if (await zwischenspeicher.EnthaeltIgnoriertesSpielAsync(quellRunde.Id, sicht.IgnorierteSpielIds, ct)) return null;

        var tabelle = await zwischenspeicher.LigaTabelleAsync(quellRunde.Id, sicht.NurSicherPositioniert, ct);
        return tabelle.FirstOrDefault(e =>
            e.Rang == platzierung && (!sicht.NurSicherPositioniert || e.SicherPositioniert))?.Mannschaft.Id;
    }

    /// <summary>Sieger (Platzierung 1) oder Verlierer (2) der Paarung <paramref name="spielNummer"/> einer
    /// K.o.-Runde — über das eingetragene Ergebnis exakt abgesichert. Issue #346: gezählt wird je Paarung
    /// (Hinspiel) unter den bereits mit beiden Mannschaften besetzten; entschieden wird bei Hin-/Rückspiel im
    /// Rückspiel, das den Gesamtsieger trägt.</summary>
    private async Task<int?> KoSpielRangAsync(
        TurnierRunde quellRunde, int? spielNummer, int platzierung, Sicht sicht, CancellationToken ct)
    {
        if (spielNummer is not { } nummer) return null;

        var alleSpiele = await zwischenspeicher.RundenSpieleAsync(quellRunde.Id, ct);
        var rueckspielJeHinspiel = KoPaarungen.RueckspielJeHinspiel(alleSpiele);
        var paarungen = alleSpiele
            .Where(s => !s.IstRueckspiel && s.HeimMannschaftId.HasValue && s.AuswaertsMannschaftId.HasValue)
            .OrderBy(s => s.Spielnummer)
            .ToList();
        if (paarungen.Count < nummer) return null;

        var hinspiel = paarungen[nummer - 1];
        var spiel    = rueckspielJeHinspiel.GetValueOrDefault(hinspiel.Id) ?? hinspiel;
        if (sicht.IgnorierteSpielIds is { } ignoriert &&
            (ignoriert.Contains(hinspiel.Id) || ignoriert.Contains(spiel.Id)))
            return null;

        return KoPaarungen.SiegerOderVerliererId(spiel, platzierung);
    }
}

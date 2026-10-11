using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Application.Tabelle;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Fussballturnierverwaltung.Application.Spielplan;

public class PlatzhalterAufloesung(
    ITurnierRundeRepository rundeRepository,
    ISpielRepository spielRepository,
    IMannschaftRepository mannschaftRepository,
    TabellenService tabellenService,
    TabellenZwischenspeicher zwischenspeicher,
    KoQuellverknuepfung quellverknuepfung,
    ILogger<PlatzhalterAufloesung> logger)
{
    /// <summary>
    /// Löst Platzhalternamen in einer KO-Runde auf: sucht in Vorrunden-Tabellen und KO-Ergebnissen
    /// nach den realen Teams und aktualisiert die Spiele. Gibt die Anzahl aufgelöster Spiele zurück.
    /// </summary>
    public Task<int> PlatzhalterAufloesenAsync(int turnierRundeId, CancellationToken ct = default)
        => zwischenspeicher.DurchlaufAsync(() => PlatzhalterAufloesenOhneZwischenspeicherAsync(turnierRundeId, ct));

    private async Task<int> PlatzhalterAufloesenOhneZwischenspeicherAsync(int turnierRundeId, CancellationToken ct)
    {
        // Aufschlüsselung für das Ablaufprotokoll (#414): Laden der Runde und ihrer Spiele, Auflösen der Namen
        // (Tabellen und Quellspiele), Speichern — als Werte in der Abschlusszeile statt eigener Vorgänge, weil
        // die Auflösung nach jedem Ergebnis für jede Runde läuft.
        var uhr = Stopwatch.StartNew();

        var runde = await rundeRepository.FindenNachIdMitRegelwerkAsync(turnierRundeId, ct)
            ?? throw new InvalidOperationException($"Runde mit ID {turnierRundeId} nicht gefunden.");

        // Alle Runden derselben TurnierInstanz laden (für Suche nach Vorrunden)
        var alleRunden = await rundeRepository.AlleNachInstanzAsync(runde.TurnierInstanzId, ct);

        var platzhalterSpiele = (await spielRepository.AlleNachRundeAsync(turnierRundeId, ct))
            .Where(s => s.HeimMannschaftId is null || s.AuswaertsMannschaftId is null)
            .ToList();

        if (platzhalterSpiele.Count == 0) return 0;
        long ladenMs = uhr.ElapsedMilliseconds;

        // Platzhalter-Cache aufbauen (jeder eindeutige Name nur einmal auflösen)
        var cache = new Dictionary<string, int?>(StringComparer.OrdinalIgnoreCase);
        var quellSpielCache = new Dictionary<int, Spiel?>();

        async Task<int?> AufloessenAsync(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            if (cache.TryGetValue(name, out var cached)) return cached;
            var result = await PlatzhalterNameAufloessenAsync(name, runde, alleRunden, ct);
            cache[name] = result;
            return result;
        }

        // Issue #241: eine gesetzte Quell-ID (K.o.-zu-K.o.) geht direkt, ohne Text-Regex, vor.
        async Task<int?> AufloesenSeiteAsync(int? quellSpielId, SpielQuellErgebnisTyp? quellTyp, string? platzhalterName)
            => quellSpielId is int qId && quellTyp is { } typ
                ? await QuellSpielAufloesenAsync(qId, typ, quellSpielCache, ct)
                : await AufloessenAsync(platzhalterName);

        // Gesammelt und am Ende in einem Zug gespeichert (#411) — die Auflösung liest nur Quellrunden, keine
        // Korrektur hängt von einer anderen derselben Runde ab.
        var korrekturen = new List<PlatzhalterKorrektur>();
        foreach (var spiel in platzhalterSpiele)
        {
            // Issue #206: jede Seite unabhängig auflösen (TV-Präsentation-Stil) — eine Seite kann
            // schon feststehen, während die andere noch als Platzhaltertext offen bleibt, statt beide
            // Seiten erst gemeinsam zu schreiben, wenn beide auflösbar sind. PlatzhalterKorrigierenAsync
            // (aus #88) unterstützt unabhängige Seiten bereits — für die jeweils noch nicht auflösbare
            // Seite wird schlicht der unveränderte Platzhaltertext zurückgeschrieben (No-Op).
            var heimId      = spiel.HeimMannschaftId      ?? await AufloesenSeiteAsync(spiel.HeimQuellSpielId, spiel.HeimQuellErgebnis, spiel.HeimPlatzhalterName);
            var auswaertsId = spiel.AuswaertsMannschaftId ?? await AufloesenSeiteAsync(spiel.AuswaertsQuellSpielId, spiel.AuswaertsQuellErgebnis, spiel.AuswaertsPlatzhalterName);

            bool heimNeuAufgeloest      = heimId is not null && spiel.HeimMannschaftId is null;
            bool auswaertsNeuAufgeloest = auswaertsId is not null && spiel.AuswaertsMannschaftId is null;
            if (!heimNeuAufgeloest && !auswaertsNeuAufgeloest) continue;

            korrekturen.Add(new PlatzhalterKorrektur(
                spiel.Id, heimId, spiel.HeimPlatzhalterName, auswaertsId, spiel.AuswaertsPlatzhalterName));
        }

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
            runde.Name, korrekturen.Count, platzhalterSpiele.Count, ladenMs, aufloesenMs, speichernMs);

        return korrekturen.Count;
    }

    /// <summary>Rein lesende Vorschau (Issue #208, Ergänzung zu #202): welcher Mannschaftsname würde nach
    /// dem aktuellen, noch nicht zwingend sicheren Tabellenstand jede noch offene Platzhalter-Seite einer
    /// Runde besetzen? Schreibt nichts in die Datenbank. Liefert nur Einträge für Spiele, bei denen
    /// mindestens eine Seite optimistisch (ohne SicherPositioniert-Gate) ermittelbar ist; bereits real
    /// zugewiesene Seiten liefern keinen Vorschau-Wert (null).</summary>
    public Task<IReadOnlyDictionary<int, (string? Heim, string? Auswaerts)>> PlatzhalterVorschauAsync(
        int turnierRundeId, CancellationToken ct = default)
        => zwischenspeicher.DurchlaufAsync(() => PlatzhalterVorschauOhneZwischenspeicherAsync(turnierRundeId, ct));

    private async Task<IReadOnlyDictionary<int, (string? Heim, string? Auswaerts)>> PlatzhalterVorschauOhneZwischenspeicherAsync(
        int turnierRundeId, CancellationToken ct)
    {
        var ergebnis = new Dictionary<int, (string? Heim, string? Auswaerts)>();

        var runde = await rundeRepository.FindenNachIdMitRegelwerkAsync(turnierRundeId, ct)
            ?? throw new InvalidOperationException($"Runde mit ID {turnierRundeId} nicht gefunden.");
        var alleRunden = await rundeRepository.AlleNachInstanzAsync(runde.TurnierInstanzId, ct);

        var platzhalterSpiele = (await spielRepository.AlleNachRundeAsync(turnierRundeId, ct))
            .Where(s => s.HeimMannschaftId is null || s.AuswaertsMannschaftId is null)
            .ToList();
        if (platzhalterSpiele.Count == 0) return ergebnis;

        var cache = new Dictionary<string, int?>(StringComparer.OrdinalIgnoreCase);
        var quellSpielCache = new Dictionary<int, Spiel?>();

        async Task<int?> VorschauAufloessenAsync(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            if (cache.TryGetValue(name, out var cached)) return cached;
            var result = await PlatzhalterNameAufloessenAsync(name, runde, alleRunden, ct, nurSicherPositioniert: false);
            cache[name] = result;
            return result;
        }

        // Issue #241: ID-Fastpath auch in der Vorschau, für Konsistenz mit der tatsächlichen Auflösung.
        async Task<int?> VorschauSeiteAsync(int? quellSpielId, SpielQuellErgebnisTyp? quellTyp, string? platzhalterName)
            => quellSpielId is int qId && quellTyp is { } typ
                ? await QuellSpielAufloesenAsync(qId, typ, quellSpielCache, ct)
                : await VorschauAufloessenAsync(platzhalterName);

        var vorschauIds = new Dictionary<int, (int? Heim, int? Auswaerts)>();
        foreach (var spiel in platzhalterSpiele)
        {
            int? heimVorschau      = spiel.HeimMannschaftId      is null ? await VorschauSeiteAsync(spiel.HeimQuellSpielId, spiel.HeimQuellErgebnis, spiel.HeimPlatzhalterName)           : null;
            int? auswaertsVorschau = spiel.AuswaertsMannschaftId is null ? await VorschauSeiteAsync(spiel.AuswaertsQuellSpielId, spiel.AuswaertsQuellErgebnis, spiel.AuswaertsPlatzhalterName) : null;
            if (heimVorschau is not null || auswaertsVorschau is not null)
                vorschauIds[spiel.Id] = (heimVorschau, auswaertsVorschau);
        }
        if (vorschauIds.Count == 0) return ergebnis;

        var teamIds = vorschauIds.Values.SelectMany(v => new[] { v.Heim, v.Auswaerts })
            .Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
        var teams = (await mannschaftRepository.FindenNachIdsAsync(teamIds, ct)).ToDictionary(m => m.Id, m => m.Name);

        foreach (var (spielId, (heimId, auswaertsId)) in vorschauIds)
            ergebnis[spielId] = (
                heimId.HasValue ? teams.GetValueOrDefault(heimId.Value) : null,
                auswaertsId.HasValue ? teams.GetValueOrDefault(auswaertsId.Value) : null);

        return ergebnis;
    }

    /// <summary>Löst die Platzhalter aller Runden der Turnierinstanz auf (Issue #90, Vorwärts-Gegenstück
    /// zur Rückwärts-Kaskade aus #88). Idempotent und günstig für Runden ohne offene Platzhalter-Spiele —
    /// <see cref="PlatzhalterAufloesenAsync"/> selbst kehrt dafür sofort zurück (früher Return bei
    /// <c>platzhalterSpiele.Count == 0</c>). Gibt die Gesamtanzahl neu aufgelöster Spiele zurück.</summary>
    public Task<int> PlatzhalterAlleRundenAufloesenAsync(int turnierInstanzId, CancellationToken ct = default)
        => zwischenspeicher.DurchlaufAsync(() => PlatzhalterAlleRundenAufloesenOhneZwischenspeicherAsync(turnierInstanzId, ct));

    private async Task<int> PlatzhalterAlleRundenAufloesenOhneZwischenspeicherAsync(int turnierInstanzId, CancellationToken ct)
    {
        await quellverknuepfung.AlleRundenVerknuepfenAsync(turnierInstanzId, ct);

        var runden = await rundeRepository.AlleNachInstanzAsync(turnierInstanzId, ct);
        int gesamt = 0;
        foreach (var runde in runden.OrderBy(r => r.Reihenfolge))
            gesamt += await PlatzhalterAufloesenAsync(runde.Id, ct);
        return gesamt;
    }

    /// <summary>Löst eine K.o.-Quellverknüpfung (<see cref="Spiel.HeimQuellSpielId"/>/-<c>Ergebnis</c>) direkt
    /// zur Mannschafts-ID auf, ohne Text-Regex — Gegenstück zum entsprechenden Zweig in
    /// <see cref="RangAufloesenAsync"/>, aber ohne dessen "nur bereits aufgelöste Spiele"-Filter, da die
    /// Quell-ID bereits eindeutig auf ein konkretes Spiel zeigt.</summary>
    private async Task<int?> QuellSpielAufloesenAsync(
        int quellSpielId, SpielQuellErgebnisTyp typ, Dictionary<int, Spiel?> cache, CancellationToken ct)
    {
        if (!cache.TryGetValue(quellSpielId, out var quellSpiel))
        {
            quellSpiel = await spielRepository.FindenNachIdMitErgebnisAsync(quellSpielId, ct);
            cache[quellSpielId] = quellSpiel;
        }
        if (quellSpiel is null) return null;

        return KoPaarungen.SiegerOderVerliererId(quellSpiel, typ == SpielQuellErgebnisTyp.Sieger ? 1 : 2);
    }

    // ── Platzhalter-Auflösungslogik ───────────────────────────────────────

    public async Task<int?> PlatzhalterNameAufloessenAsync(
        string name, TurnierRunde runde, IReadOnlyList<TurnierRunde> alleRunden, CancellationToken ct,
        IReadOnlySet<int>? ignorierteSpielIds = null, bool nurSicherPositioniert = true) =>
        PlatzhalterNamen.Erkennen(name) switch
        {
            PlatzhalterBezug.GruppenRang g => await GruppenrangAufloessenAsync(
                g.GruppenKennung, g.Rang, runde, alleRunden, ct, ignorierteSpielIds, nurSicherPositioniert),
            PlatzhalterBezug.KombinationsPlatz k => await KombinationsPlatzAufloesenAsync(
                name, k.Platzierung, runde, alleRunden, ct, ignorierteSpielIds),
            PlatzhalterBezug.BestePlatzierung b => await BestePlatzierungAufloessenAsync(
                b.Rang, b.Platzierung, runde, alleRunden, ct, ignorierteSpielIds),
            PlatzhalterBezug.KoSpiel k => await KoSpielAufloesenAsync(k, runde, alleRunden, ct, ignorierteSpielIds),
            PlatzhalterBezug.TabellenPlatz t => await TabellenPlatzAufloesenAsync(
                t, runde, alleRunden, ct, ignorierteSpielIds, nurSicherPositioniert),
            _ => null,
        };

    private async Task<int?> TabellenPlatzAufloesenAsync(
        PlatzhalterBezug.TabellenPlatz bezug, TurnierRunde runde, IReadOnlyList<TurnierRunde> alleRunden, CancellationToken ct,
        IReadOnlySet<int>? ignorierteSpielIds, bool nurSicherPositioniert)
    {
        var quellRunde = alleRunden
            .Where(r => r.Reihenfolge < runde.Reihenfolge
                     && string.Equals(r.Name, bezug.RundenName, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(r => r.Reihenfolge)
            .FirstOrDefault();
        if (quellRunde is null) return null;

        return await RangAufloesenAsync(quellRunde, null, bezug.Platz, ct, ignorierteSpielIds, nurSicherPositioniert);
    }

    private async Task<int?> GruppenrangAufloessenAsync(
        string gruppenKennung, int rang, TurnierRunde runde, IReadOnlyList<TurnierRunde> alleRunden, CancellationToken ct,
        IReadOnlySet<int>? ignorierteSpielIds = null, bool nurSicherPositioniert = true)
    {
        // Gruppenkennung → Gruppenindex (A=1, B=2, ... oder direkte Zahl)
        int gruppenNummer = gruppenKennung.Length == 1 && char.IsLetter(gruppenKennung[0])
            ? char.ToUpper(gruppenKennung[0]) - 'A' + 1
            : int.TryParse(gruppenKennung, out int n) ? n : 0;

        if (gruppenNummer <= 0) return null;

        // Bei mehreren Gruppenphase-Runden im selben Turnier (z.B. "Gruppenphase 1" + "Gruppenphase 2")
        // muss die naechstgelegene VORHERGEHENDE Gruppenphase verwendet werden, nicht immer die erste.
        var gruppenphaseRunde = alleRunden
            .Where(r => r.Rundenart == Rundenart.Gruppenphase && r.Reihenfolge < runde.Reihenfolge)
            .OrderByDescending(r => r.Reihenfolge)
            .FirstOrDefault();
        if (gruppenphaseRunde is null) return null;

        return await RangAufloesenAsync(gruppenphaseRunde, gruppenNummer, rang, ct, ignorierteSpielIds, nurSicherPositioniert);
    }

    /// <summary>Löst "N. bester X. Platz" auf (Issue #213): sucht die nächstgelegene vorhergehende
    /// Gruppenphase-Runde, prüft dasselbe konservative All-or-nothing-Gate wie <see cref="BesteNAufloesenAsync"/>
    /// (alle Spiele der Gruppenphase abgeschlossen), und liefert das N-te Team der über
    /// <see cref="Fussballturnierverwaltung.Application.Tabelle.TabellenService.UebergreifendeRangfolgeBerechnenAsync"/>
    /// ermittelten Cross-Group-Rangfolge an Platzierung X. Funktioniert bewusst auch ohne konfigurierte
    /// <see cref="Uebergangsregel"/> (Freitext-Turniere).</summary>
    /// <summary>Issue #372: löst einen Platz wie "3. Gruppe A/D/E/F" über die Kombinationstabelle der Zielrunde
    /// auf — erst die besten Dritten über alle Gruppen ermitteln, deren Gruppenbuchstaben als Schlüssel
    /// nachschlagen, dann den Dritten der dort eingetragenen Gruppe liefern. Wie bei
    /// <see cref="BestePlatzierungAufloessenAsync"/> erst nach vollständig gespielter Gruppenphase: vorher
    /// steht die Menge der qualifizierten Gruppen nicht fest.</summary>
    private async Task<int?> KombinationsPlatzAufloesenAsync(
        string name, int platzierung, TurnierRunde runde, IReadOnlyList<TurnierRunde> alleRunden, CancellationToken ct,
        IReadOnlySet<int>? ignorierteSpielIds)
    {
        if (Kombinationstabelle.AusJson(runde.KombinationstabelleJson) is not { AnzahlBeste: > 0 } tabelle) return null;

        var gruppenphaseRunde = alleRunden
            .Where(r => r.Rundenart == Rundenart.Gruppenphase && r.Reihenfolge < runde.Reihenfolge)
            .OrderByDescending(r => r.Reihenfolge)
            .FirstOrDefault();
        if (gruppenphaseRunde is null) return null;

        if (await zwischenspeicher.EnthaeltIgnoriertesSpielAsync(gruppenphaseRunde.Id, ignorierteSpielIds, ct)) return null;
        var spiele = await zwischenspeicher.RundenSpieleAsync(gruppenphaseRunde.Id, ct);
        if (spiele.Count == 0 || spiele.Any(s => s.Status != SpielStatus.Abgeschlossen))
            return null;

        var rangfolge = await tabellenService.UebergreifendeRangfolgeBerechnenAsync(gruppenphaseRunde.Id, platzierung, ct);
        if (rangfolge.Count < tabelle.AnzahlBeste) return null;
        var besteIds = rangfolge.Take(tabelle.AnzahlBeste).Select(e => e.Mannschaft.Id).ToHashSet();

        var gruppenTabellen = await zwischenspeicher.GruppenTabellenAsync(gruppenphaseRunde.Id, ct);
        var qualifizierteNachGruppe = new Dictionary<string, int>();
        foreach (var gruppe in gruppenTabellen)
        {
            var mannschaftId = gruppe.Tabelle.FirstOrDefault(e => e.Rang == platzierung)?.Mannschaft.Id;
            if (mannschaftId is { } id && besteIds.Contains(id) && gruppe.GruppenNummer is >= 1 and <= 26)
                qualifizierteNachGruppe[((char)('A' + gruppe.GruppenNummer - 1)).ToString()] = id;
        }
        if (qualifizierteNachGruppe.Count != tabelle.AnzahlBeste) return null;

        return tabelle.GruppeFuer(name, qualifizierteNachGruppe.Keys) is { } zielGruppe
            && qualifizierteNachGruppe.TryGetValue(zielGruppe, out var ergebnis)
            ? ergebnis
            : null;
    }

    private async Task<int?> BestePlatzierungAufloessenAsync(
        int rang, int platzierung, TurnierRunde runde, IReadOnlyList<TurnierRunde> alleRunden, CancellationToken ct,
        IReadOnlySet<int>? ignorierteSpielIds = null)
    {
        var gruppenphaseRunde = alleRunden
            .Where(r => r.Rundenart == Rundenart.Gruppenphase && r.Reihenfolge < runde.Reihenfolge)
            .OrderByDescending(r => r.Reihenfolge)
            .FirstOrDefault();
        if (gruppenphaseRunde is null) return null;

        if (await zwischenspeicher.EnthaeltIgnoriertesSpielAsync(gruppenphaseRunde.Id, ignorierteSpielIds, ct)) return null;
        var spiele = await zwischenspeicher.RundenSpieleAsync(gruppenphaseRunde.Id, ct);
        if (spiele.Any(s => s.HeimMannschaftId.HasValue && s.AuswaertsMannschaftId.HasValue && s.Status != SpielStatus.Abgeschlossen))
            return null;

        var rangfolge = await tabellenService.UebergreifendeRangfolgeBerechnenAsync(gruppenphaseRunde.Id, platzierung, ct);
        return rang <= rangfolge.Count ? rangfolge[rang - 1].Mannschaft.Id : null;
    }

    private async Task<int?> KoSpielAufloesenAsync(
        PlatzhalterBezug.KoSpiel bezug, TurnierRunde runde, IReadOnlyList<TurnierRunde> alleRunden, CancellationToken ct,
        IReadOnlySet<int>? ignorierteSpielIds)
    {
        var koRunde = PlatzhalterNamen.KoQuellrunde(bezug, runde, alleRunden);
        if (koRunde is null) return null;

        return await RangAufloesenAsync(koRunde, bezug.SpielNummer, bezug.Verlierer ? 2 : 1, ct, ignorierteSpielIds);
    }

    // ── Regelbasierte Auflösung (Issue #130) ──────────────────────────────

    /// <summary>Löst eine Platzierung einer <see cref="Uebergangsregel"/> zum tatsächlichen Mannschaft-Id auf.
    /// Liefert <c>null</c>, solange die Quellrunde noch nicht (für diese Platzierung) entschieden ist.</summary>
    public Task<int?> UebergangsregelPlatzAufloesenAsync(Uebergangsregel regel, int platzierung, CancellationToken ct = default)
    {
        if (platzierung < regel.PlatzierungVon || platzierung > regel.PlatzierungBis)
            throw new ArgumentOutOfRangeException(nameof(platzierung),
                $"Platzierung {platzierung} liegt außerhalb des Regel-Bereichs {regel.PlatzierungVon}-{regel.PlatzierungBis}.");

        return RangAufloesenAsync(regel.QuellRunde, regel.GruppeIndex, platzierung, ct);
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
            var beste = await BesteNAufloesenAsync(regel, ct);
            return beste.Select((teamId, i) => (Rang: i + 1, TeamId: teamId))
                .ToDictionary(x => x.Rang, x => x.TeamId);
        }

        var ergebnis = new Dictionary<int, int?>();
        for (int platz = regel.PlatzierungVon; platz <= regel.PlatzierungBis; platz++)
            ergebnis[platz] = await RangAufloesenAsync(regel.QuellRunde, regel.GruppeIndex, platz, ct);
        return ergebnis;
    }

    /// <summary>Löst eine gruppenübergreifende Rangfolge-Regel (Issue #184) auf: nimmt das Team an
    /// <see cref="Uebergangsregel.PlatzierungVon"/> aus jeder Gruppe der Quellrunde, vergleicht diese Kandidaten
    /// nach den Ranking-Regeln des Regelwerks und liefert die <see cref="Uebergangsregel.AnzahlBeste"/> besten.
    /// Bewusst konservatives Gate: löst erst auf, sobald ALLE Spiele der Quellrunde (alle Gruppen) abgeschlossen
    /// sind — kein Extremrang-artiger Optimistic-Preview wie bei #202/#206, das wäre eine kombinatorische
    /// Erweiterung über Gruppengrenzen hinweg und würde den Aufwand dieses Features deutlich sprengen.</summary>
    public Task<IReadOnlyList<int?>> UebergangsregelBesteNAufloesenAsync(Uebergangsregel regel, CancellationToken ct = default)
        => BesteNAufloesenAsync(regel, ct);

    private async Task<IReadOnlyList<int?>> BesteNAufloesenAsync(Uebergangsregel regel, CancellationToken ct)
    {
        int n = regel.AnzahlBeste!.Value;
        bool offene = (await zwischenspeicher.RundenSpieleAsync(regel.QuellRundeId, ct))
            .Any(s => s.HeimMannschaftId.HasValue && s.AuswaertsMannschaftId.HasValue && s.Status != SpielStatus.Abgeschlossen);
        if (offene) return [.. Enumerable.Repeat((int?)null, n)];

        var rangfolge = await tabellenService.UebergreifendeRangfolgeBerechnenAsync(regel.QuellRundeId, regel.PlatzierungVon, ct);
        return Enumerable.Range(0, n)
            .Select(i => i < rangfolge.Count ? (int?)rangfolge[i].Mannschaft.Id : null)
            .ToList();
    }

    /// <summary>
    /// Kern-Auflösung "Platzierung X einer Quellrunde → Mannschaft-Id", regelbasiert und unabhängig davon, ob die
    /// Koordinaten (Quellrunde/Gruppenindex/Platzierung) aus einer <see cref="Uebergangsregel"/> oder — für
    /// bestehende, per Freitext angelegte Turniere — aus dem Regex-Parser stammen. Die Bedeutung von
    /// <paramref name="gruppeIndex"/> hängt von der Rundenart der Quellrunde ab:
    /// Gruppenphase → Gruppennummer (Platzierung = Tabellenrang in dieser Gruppe), löst erst auf, sobald der
    /// Rang laut <see cref="TabellenEintrag.SicherPositioniert"/> nicht mehr verdrängbar ist (Issue #202);
    /// Liga/TopfLiga/Schweizer System → nicht verwendet (Platzierung = Tabellenrang der Gesamttabelle), mit
    /// Punkte-Schranken-Gate aus <see cref="TabellenService.LigaTabelleMitSicherheitBerechnenAsync"/> (Issue #343);
    /// K.-o.-Runde/Finale → Spielnummer innerhalb der Runde (Platzierung 1 = Sieger, 2 = Verlierer), bereits
    /// exakt über ein eingetragenes Ergebnis abgesichert, keine Simulation nötig.
    /// </summary>
    private async Task<int?> RangAufloesenAsync(
        TurnierRunde quellRunde, int? gruppeIndex, int platzierung, CancellationToken ct,
        IReadOnlySet<int>? ignorierteSpielIds = null, bool nurSicherPositioniert = true)
    {
        switch (quellRunde.Rundenart)
        {
            case Rundenart.Gruppenphase:
            {
                if (gruppeIndex is null) return null;
                // Issue #88: sobald ein Ergebnis dieser Gruppe zur Löschung ansteht, gilt sie vorübergehend als
                // nicht ermittelbar. Seit #397 trägt das Spiel seine Gruppennummer — ohne sie (Altdaten) bleibt
                // es bei der ganzen Runde.
                if (await zwischenspeicher.EnthaeltIgnoriertesSpielAsync(quellRunde.Id, ignorierteSpielIds, ct, gruppeIndex.Value))
                    return null;
                var gruppenTabellen = await zwischenspeicher.GruppenTabellenAsync(quellRunde.Id, ct);
                var gruppe = gruppenTabellen.FirstOrDefault(g => g.GruppenNummer == gruppeIndex.Value);
                // Issue #202: der aktuelle Rang allein reicht nicht - er darf durch kein Ergebnis der noch
                // offenen Spiele mehr verdrängbar sein, sonst würde die Platzhalter-Auflösung optimistisch
                // vorgreifen (siehe Qualifikationsstatus-Ampel in der Gruppentabelle, Issue #70/#201) — außer
                // die aufrufende Stelle will bewusst genau diese Optimistisch-Auflösung als Vorschau
                // (Issue #208, nurSicherPositioniert=false), die nichts persistiert.
                return gruppe?.Tabelle.FirstOrDefault(e =>
                    e.Rang == platzierung && (!nurSicherPositioniert || e.SicherPositioniert))?.Mannschaft.Id;
            }

            // Liga/TopfLiga/SchweizerSystem teilen sich eine Gesamttabelle über alle Spiele der Runde (Issue
            // #25-Nachlieferung). Issue #343: wie bei der Gruppenphase (#202) nur fest auflösen, wenn der Rang
            // durch die verbleibenden Spiele nicht mehr verdrängbar ist — die optimistische Vorschau (#208,
            // nurSicherPositioniert=false) nutzt weiter den aktuellen Tabellenstand.
            case Rundenart.Liga:
            case Rundenart.TopfLiga:
            case Rundenart.SchweizerSystem:
            {
                if (await zwischenspeicher.EnthaeltIgnoriertesSpielAsync(quellRunde.Id, ignorierteSpielIds, ct)) return null;
                var tabelle = await zwischenspeicher.LigaTabelleAsync(quellRunde.Id, nurSicherPositioniert, ct);
                return tabelle.FirstOrDefault(e =>
                    e.Rang == platzierung && (!nurSicherPositioniert || e.SicherPositioniert))?.Mannschaft.Id;
            }

            case Rundenart.KoRunde:
            case Rundenart.Finale:
            case Rundenart.SpielUmPlatzDrei:
            {
                if (gruppeIndex is not { } spielNummer) return null;
                // Issue #346: gezählt wird je Paarung (Hinspiel); entschieden wird bei Hin-/Rückspiel im
                // Rückspiel, das den Gesamtsieger trägt.
                var alleSpiele = await zwischenspeicher.RundenSpieleAsync(quellRunde.Id, ct);
                var rueckspielJeHinspiel = KoPaarungen.RueckspielJeHinspiel(alleSpiele);
                var paarungen = alleSpiele
                    .Where(s => !s.IstRueckspiel && s.HeimMannschaftId.HasValue && s.AuswaertsMannschaftId.HasValue)
                    .OrderBy(s => s.Spielnummer)
                    .ToList();
                if (paarungen.Count < spielNummer) return null;

                var hinspiel = paarungen[spielNummer - 1];
                var spiel    = rueckspielJeHinspiel.GetValueOrDefault(hinspiel.Id) ?? hinspiel;
                if (ignorierteSpielIds is not null &&
                    (ignorierteSpielIds.Contains(hinspiel.Id) || ignorierteSpielIds.Contains(spiel.Id)))
                    return null;

                return KoPaarungen.SiegerOderVerliererId(spiel, platzierung);
            }

            default:
                return null;
        }
    }
}

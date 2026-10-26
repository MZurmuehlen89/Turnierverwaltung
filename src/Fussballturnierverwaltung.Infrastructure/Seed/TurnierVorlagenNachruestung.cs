using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Fussballturnierverwaltung.Infrastructure.Seed.Daten;
using Microsoft.EntityFrameworkCore;

namespace Fussballturnierverwaltung.Infrastructure.Seed;

/// <summary>Zieht bestehende Turniervorlagen auf neue Seed-Stände nach (Issue #440: aus
/// <see cref="SeedDatenService"/> herausgelöst, weil dieser Teil mit jedem Vorlagen-Issue wächst). Ein Objekt
/// gilt für genau einen Lauf: Vorlagen und Seed-Stand werden einmal geladen, alle Schritte arbeiten auf denselben
/// Objekten und sehen damit die Änderungen der vorherigen. Jeder Schritt ändert nur, was noch einem bekannten
/// alten Seed-Stand entspricht — was der Nutzer an einer Vorlage angepasst hat, bleibt erhalten.</summary>
internal sealed class TurnierVorlagenNachruestung
{
    // Seed-Stand der WM-24-Vorlage vor #350 (damals als "1982–1994" geführt).
    private const string ALTE_BESCHREIBUNG_WM24 = "6 Dreiergruppen + Achtelfinale-Runde → K.-o.";
    private static readonly DateOnly _altesEnddatumWm24 = new(1994, 12, 31);

    // Seed-Stand der EM-Vorlage mit 8 Teams vor #375 (damals als "1960–1992" geführt).
    private static readonly DateOnly _altesStartdatumEm8 = new(1960, 1, 1);

    // Seed-Stand der WM-Vorlage mit 16 Teams vor #465 (damals als "1934–1978" geführt).
    private static readonly DateOnly _altesEnddatumWm16 = new(1978, 12, 31);
    private static readonly (string Name, Rundenart Rundenart)[] _alteRundenEm8 =
    [
        ("Gruppenphase",     Rundenart.Gruppenphase),
        ("Halbfinale",       Rundenart.KoRunde),
        ("Spiel um Platz 3", Rundenart.SpielUmPlatzDrei),
        ("Finale",           Rundenart.Finale),
    ];

    private readonly TurnierverwaltungDbContext _context;
    private readonly Action<int, string> _protokollieren;
    private readonly IReadOnlyDictionary<string, int> _regelwerkIds;
    private readonly List<TurnierInstanz> _vorlagen;
    private readonly Dictionary<(string Name, string Variante), TurnierInstanz> _seedStand;
    // Eigene Objekte neben dem Vergleichsstand: was hiervon fehlt, wird gespeichert und gehört danach zum Bestand.
    private readonly List<TurnierInstanz> _seedVorlagenZumAnlegen;

    private TurnierVorlagenNachruestung(
        TurnierverwaltungDbContext context, Action<int, string> protokollieren,
        IReadOnlyDictionary<string, int> regelwerkIds, List<TurnierInstanz> vorlagen,
        List<TurnierInstanz> seedStand, List<TurnierInstanz> seedVorlagenZumAnlegen)
    {
        _context = context;
        _protokollieren = protokollieren;
        _regelwerkIds = regelwerkIds;
        _vorlagen = vorlagen;
        _seedStand = seedStand.ToDictionary(Schluessel);
        _seedVorlagenZumAnlegen = seedVorlagenZumAnlegen;
    }

    /// <summary>Verlangt, dass Regelwerke, Verbände, Länder und Ranglisten-Quellen schon nachgerüstet sind — die
    /// Vorlagen aus dem Seed verweisen darauf.</summary>
    public static async Task<TurnierVorlagenNachruestung> LadenAsync(
        TurnierverwaltungDbContext context, Action<int, string> protokollieren, CancellationToken ct)
    {
        var regelwerkIds = await context.Regelwerke.ToDictionaryAsync(r => r.Name, r => r.Id, ct);
        var verbandIds = await context.Verbaende.ToDictionaryAsync(v => v.Kurzname, v => v.Id, ct);
        var landIds = await context.Laender.ToDictionaryAsync(l => l.FifaCode, l => l.Id, ct);
        var ranglisteJeQuelle = await NeuesteRanglisteJeQuelleAsync(context, ct);
        var ranglisteJeName = await NeuesteRanglisteJeNameAsync(context, ct);
        List<TurnierInstanz> SeedVorlagen() =>
            TurnierVorlagenSeedDaten.Erstellen(regelwerkIds, verbandIds, landIds, ranglisteJeQuelle, ranglisteJeName);

        // Issue #294: Vorlagen sind TurnierInstanz-Zeilen mit Status Vorlage — der Status gehört zur Identität,
        // damit ein echtes Turnier mit zufällig gleichem Namen nicht als "bereits vorhanden" zählt.
        var vorlagen = await context.TurnierInstanzen
            .Include(i => i.Runden)
                .ThenInclude(r => r.TabellenBereiche)
            .Include(i => i.Auslosungen)
            .Where(i => i.Status == TurnierStatus.Vorlage)
            .ToListAsync(ct);

        return new(context, protokollieren, regelwerkIds, vorlagen, SeedVorlagen(), SeedVorlagen());
    }

    public async Task AusfuehrenAsync(CancellationToken ct)
    {
        // Vor dem Ergänzen: sonst entstünde unter dem neuen Namen + Variante ein Duplikat (Issue #350/#367).
        await SpeichernUndProtokollieren(Umbenennen(), "Turniervorlagen umbenannt.", ct);
        await SpeichernUndProtokollieren(FehlendeErgaenzen(), "neue Turniervorlagen nachgerüstet.", ct);
        await SpeichernUndProtokollieren(
            FilterErgaenzen(), "Turniervorlagen um Verband-/Land-/Mannschaftsartfilter ergänzt.", ct);
        await SpeichernUndProtokollieren(
            SchweizerLigaphaseAufTopfLigaKorrigieren(), "Vorlagen-Runden von SchweizerSystem auf TopfLiga korrigiert.", ct);
        await SpeichernUndProtokollieren(
            LigaphaseRegelwerkUmstellen(), "Vorlagen-Runden auf das Regelwerk der UEFA-Ligaphase umgestellt.", ct);
        await SpeichernUndProtokollieren(
            CopaRegelwerkUmstellen(), "Vorlagen-Runden der Copa América auf direktes Elfmeterschießen umgestellt.", ct);
        // Vor dem Regelwerk: die Umstellung ordnet Runden über Reihenfolge + Name zu (Issue #375).
        await SpeichernUndProtokollieren(
            await Em8SpielUmPlatzDreiEntfernenAsync(ct),
            "Spiel um Platz 3 aus der Vorlage „UEFA EM · 8 Teams · 1984–1992“ entfernt.", ct);
        await SpeichernUndProtokollieren(
            Em8RegelwerkUmstellen(),
            "Vorlagen-Runden der EM mit 8 Teams auf das 2-Punkte-Regelwerk mit Verlängerung + Elfmeterschießen umgestellt.", ct);
        await SpeichernUndProtokollieren(
            Wm16RegelwerkUmstellen(),
            "Vorlagen-Runden der WM 1934–1938 auf Verlängerung + Wiederholungsspiel + Los umgestellt.", ct);
        await SpeichernUndProtokollieren(
            BeschreibungenAktualisieren(), "Beschreibungen von Turniervorlagen aktualisiert.", ct);
        await SpeichernUndProtokollieren(
            await StrukturErgaenzenAsync(ct), "Struktur-Ergänzungen an Turniervorlagen nachgerüstet.", ct);
        await SpeichernUndProtokollieren(FesteVorbelegungenErgaenzen(), "Turniervorlagen um feste Vorbelegungen ergänzt.", ct);
        await SpeichernUndProtokollieren(
            TabellenBereicheErgaenzen(), "Turniervorlagen um Tabellenbereiche (Europapokal, Relegation, Abstieg) ergänzt.", ct);
        await SpeichernUndProtokollieren(
            StandardRanglistenErgaenzen(), "Vorlagen-Runden um ihre Standard-Rangliste ergänzt.", ct);
        await SpeichernUndProtokollieren(
            UnterklassigenHeimrechtUebernehmen(), "Vorlagen-Runden auf „Heimrecht für Unterklassige“ umgestellt.", ct);
        await SpeichernUndProtokollieren(
            GegnerJeTopfUebernehmen(), "Ligaphasen von Vorlagen auf einen Gegner je Topf umgestellt.", ct);
    }

    // Je Quellenname die neueste daraus importierte Rangliste (nach Datum, bei Gleichstand die zuletzt angelegte) —
    // Vorlagen verweisen seit #393 auf eine Rangliste. Quellennamen sind nicht eindeutig erzwungen, Dubletten
    // zählen zusammen.
    private static async Task<Dictionary<string, int>> NeuesteRanglisteJeQuelleAsync(
        TurnierverwaltungDbContext context, CancellationToken ct) =>
        (await context.Ranglisten
            .Where(r => r.RanglistenQuelle != null)
            .Select(r => new { Quelle = r.RanglistenQuelle!.Name, r.Id, r.Datum })
            .ToListAsync(ct))
            .GroupBy(r => r.Quelle)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.Datum).ThenByDescending(r => r.Id).First().Id);

    // Je Ranglistenname die neueste Rangliste — Ranglistennamen sind nicht eindeutig erzwungen (Issue #393).
    private static async Task<Dictionary<string, int>> NeuesteRanglisteJeNameAsync(
        TurnierverwaltungDbContext context, CancellationToken ct) =>
        (await context.Ranglisten.Select(r => new { r.Name, r.Id, r.Datum }).ToListAsync(ct))
            .GroupBy(r => r.Name)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.Datum).ThenByDescending(r => r.Id).First().Id);

    // ── Gemeinsame Bausteine ───────────────────────────────────────────────────

    // Issue #367: Vorlagen sind über Name + Variante identifiziert (mehrere "FIFA WM" mit verschiedener Variante).
    private static (string Name, string Variante) Schluessel(TurnierInstanz vorlage) => (vorlage.Name, vorlage.Variante);

    private bool IstVorhanden((string Name, string Variante) schluessel) =>
        _vorlagen.Any(v => Schluessel(v) == schluessel);

    /// <summary>Bestands-Vorlagen, die es im Seed gibt, je mit ihrem Seed-Stand — vom Nutzer angelegte Vorlagen
    /// bleiben außen vor.</summary>
    private IEnumerable<(TurnierInstanz Vorlage, TurnierInstanz Soll)> MitSeedStand()
    {
        foreach (var vorlage in _vorlagen)
            if (_seedStand.TryGetValue(Schluessel(vorlage), out var soll)) yield return (vorlage, soll);
    }

    private async Task SpeichernUndProtokollieren(int anzahl, string text, CancellationToken ct)
    {
        if (anzahl == 0) return;
        await _context.SaveChangesAsync(ct);
        _protokollieren(anzahl, text);
    }

    // ── Umbenennen und Ergänzen ────────────────────────────────────────────────

    /// <summary>Issue #350/#367/#375: benennt Bestands-Vorlagen um, deren Name oder Variante sich im Seed geändert
    /// hat — nur, solange unter dem neuen Namen + Variante noch keine Vorlage existiert.</summary>
    private int Umbenennen()
    {
        int umbenannt = KlammerzusaetzeInVarianteVerschieben();
        umbenannt += VariantenUmstellen();
        return umbenannt;
    }

    // Seit #367 steht der Klammerzusatz des Namens im Feld Variante. Beschreibung und Daten ziehen nur mit, wenn
    // sie noch dem alten Seed-Stand entsprechen.
    private int KlammerzusaetzeInVarianteVerschieben()
    {
        int umbenannt = 0;
        foreach (var (alterName, neu) in TurnierVorlagenSeedDaten.UmbenennungenNachAltemNamen)
        {
            var vorlage = _vorlagen.FirstOrDefault(v => v.Name == alterName);
            if (vorlage is null || IstVorhanden(neu)) continue;

            var soll = _seedStand[neu];
            vorlage.Name = neu.Name;
            vorlage.Variante = neu.Variante;
            if (vorlage.Beschreibung == ALTE_BESCHREIBUNG_WM24) vorlage.Beschreibung = soll.Beschreibung;
            AlteDatenNachziehen(vorlage, soll);
            umbenannt++;
        }
        return umbenannt;
    }

    // Start- und Enddatum ziehen nur mit, solange sie noch einem bekannten alten Seed-Stand entsprechen.
    private static void AlteDatenNachziehen(TurnierInstanz vorlage, TurnierInstanz soll)
    {
        if (vorlage.Enddatum == _altesEnddatumWm24 || vorlage.Enddatum == _altesEnddatumWm16) vorlage.Enddatum = soll.Enddatum;
        if (vorlage.Startdatum == _altesStartdatumEm8) vorlage.Startdatum = soll.Startdatum;
    }

    // Issue #375/#465: Vorlagen, die schon Name + Variante tragen und nur eine neue Variante bekommen.
    private int VariantenUmstellen()
    {
        int umbenannt = 0;
        foreach (var (alt, neueVariante) in TurnierVorlagenSeedDaten.VariantenUmbenennungen)
        {
            var neu = (alt.Name, neueVariante);
            var vorlage = _vorlagen.FirstOrDefault(v => Schluessel(v) == alt);
            if (vorlage is null || IstVorhanden(neu)) continue;

            vorlage.Variante = neueVariante;
            AlteDatenNachziehen(vorlage, _seedStand[neu]);
            umbenannt++;
        }
        return umbenannt;
    }

    /// <summary>Legt Seed-Vorlagen, die im Bestand fehlen, als vollständigen Objektgraph an (Runden,
    /// Übergangsregeln, feste Paarungen).</summary>
    private int FehlendeErgaenzen()
    {
        var fehlende = _seedVorlagenZumAnlegen.Where(v => !IstVorhanden(Schluessel(v))).ToList();
        _context.TurnierInstanzen.AddRange(fehlende);
        _vorlagen.AddRange(fehlende);
        return fehlende.Count;
    }

    /// <summary>Issue #189-Vorarbeit: VerbandId/LandId/MannschaftsartFilter kamen nachträglich zu TurnierInstanz
    /// hinzu. Trägt sie nur nach, wo alle drei noch auf dem technischen Default stehen.</summary>
    private int FilterErgaenzen()
    {
        int ergaenzt = 0;
        foreach (var (vorlage, soll) in MitSeedStand())
        {
            // Issue #452: Vorlagen ohne Filter im Seed (Mini-Turniere, Freies Turnier) haben nichts nachzutragen.
            if (HatFilter(vorlage) || !HatFilter(soll)) continue;

            vorlage.VerbandId = soll.VerbandId;
            vorlage.LandId = soll.LandId;
            vorlage.MannschaftsartFilter = soll.MannschaftsartFilter;
            ergaenzt++;
        }
        return ergaenzt;
    }

    private static bool HatFilter(TurnierInstanz vorlage) =>
        vorlage.VerbandId is not null || vorlage.LandId is not null
        || vorlage.MannschaftsartFilter != MannschaftsartFilter.Alle;

    // ── Rundenart und Regelwerk ────────────────────────────────────────────────

    /// <summary>Issue #343: der #328-Fix (CL/EL/ECL-Ligaphase TopfLiga statt SchweizerSystem) änderte nur die
    /// Seed-Daten, bestehende Datenbanken behielten die falsche Rundenart. Korrigiert nur diesen bekannten
    /// Altwert.</summary>
    private int SchweizerLigaphaseAufTopfLigaKorrigieren()
    {
        int korrigiert = 0;
        foreach (var (vorlage, soll) in MitSeedStand())
        {
            foreach (var runde in vorlage.Runden.Where(r => r.Rundenart == Rundenart.SchweizerSystem))
            {
                if (SeedVorlagenAbgleich.Gegenstueck(soll, runde)?.Rundenart != Rundenart.TopfLiga) continue;

                runde.Rundenart = Rundenart.TopfLiga;
                runde.KonfigurationJson = null; // AnzahlSpieltage ist nur für SchweizerSystem relevant
                korrigiert++;
            }
        }
        return korrigiert;
    }

    /// <summary>Issue #353: CL/EL/ECL ab 2024/25 haben ein eigenes Regelwerk mit der Rangfolge der Ligaphase,
    /// bestehende Vorlagen behielten "UEFA Clubwettbewerb Standard" (direkter Vergleich zuerst).</summary>
    private int LigaphaseRegelwerkUmstellen() =>
        RegelwerkUmstellen(TurnierVorlagenSeedDaten.UEFA_CLUB_STANDARD, TurnierVorlagenSeedDaten.UEFA_LIGAPHASE);

    /// <summary>Issue #374: die Copa América spielt Viertelfinale, Halbfinale und Spiel um Platz 3 ohne
    /// Verlängerung, bestehende Vorlagen behielten dort das FIFA-Regelwerk (Verlängerung + Elfmeterschießen).</summary>
    private int CopaRegelwerkUmstellen() =>
        RegelwerkUmstellen(
            TurnierVorlagenSeedDaten.FIFA_STANDARD_MODERN, TurnierVorlagenSeedDaten.COPA_DIREKTES_ELFMETERSCHIESSEN);

    /// <summary>Issue #375: die EM-Endrunden 1984–1992 liefen mit zwei Punkten je Sieg, Verlängerung und
    /// Elfmeterschießen — die frühere Sammelvorlage stand auf Wiederholungsspiel + Los.</summary>
    private int Em8RegelwerkUmstellen() =>
        RegelwerkUmstellen(
            TurnierVorlagenSeedDaten.HISTORISCH_WIEDERHOLUNGSSPIEL_LOS, TurnierVorlagenSeedDaten.FIFA_HISTORISCH_2_PUNKTE);

    /// <summary>Issue #465: die WM 1934/1938 ging bei Unentschieden erst in die Verlängerung, dann ins
    /// Wiederholungsspiel — die frühere Sammelvorlage „1934–1978“ stand auf Wiederholungsspiel + Los.</summary>
    private int Wm16RegelwerkUmstellen() =>
        RegelwerkUmstellen(
            TurnierVorlagenSeedDaten.HISTORISCH_WIEDERHOLUNGSSPIEL_LOS,
            TurnierVorlagenSeedDaten.HISTORISCH_VERLAENGERUNG_WIEDERHOLUNGSSPIEL_LOS);

    /// <summary>Stellt nur Runden um, die noch auf genau dem alten Seed-Regelwerk stehen und deren Gegenstück im
    /// Seed das neue nutzt.</summary>
    private int RegelwerkUmstellen(string altesRegelwerk, string neuesRegelwerk)
    {
        if (!_regelwerkIds.TryGetValue(altesRegelwerk, out int altId)
            || !_regelwerkIds.TryGetValue(neuesRegelwerk, out int neuId)) return 0;

        int umgestellt = 0;
        foreach (var (vorlage, soll) in MitSeedStand())
        {
            foreach (var runde in vorlage.Runden.Where(r => r.RegelwerkId == altId))
            {
                if (SeedVorlagenAbgleich.Gegenstueck(soll, runde)?.RegelwerkId != neuId) continue;

                runde.RegelwerkId = neuId;
                umgestellt++;
            }
        }
        return umgestellt;
    }

    /// <summary>Issue #375: die frühere Sammelvorlage „8 Teams · 1960–1992“ hatte ein Spiel um Platz 3, das es
    /// im Format 1984–1992 nicht mehr gab. Entfernt die Runde samt ihrer festen Paarung und rückt das Finale
    /// nach — nur, solange die Runden noch dem alten Seed-Stand entsprechen und keine Übergangsregel an der
    /// Runde hängt.</summary>
    private async Task<int> Em8SpielUmPlatzDreiEntfernenAsync(CancellationToken ct)
    {
        var vorlage = _vorlagen.FirstOrDefault(
            v => Schluessel(v) == ("UEFA EM", TurnierVorlagenSeedDaten.EM_8_TEAMS_1984_BIS_1992));
        if (vorlage is null) return 0;

        var runden = vorlage.Runden.OrderBy(r => r.Reihenfolge).ToList();
        if (!runden.Select(r => (r.Name, r.Rundenart)).SequenceEqual(_alteRundenEm8)) return 0;

        var platzDrei = runden[2];
        var finale = runden[3];
        if (await _context.Uebergangsregeln.AnyAsync(u => u.QuellRundeId == platzDrei.Id || u.ZielRundeId == platzDrei.Id, ct))
            return 0;

        // Auslosung → Runde ist NoAction, daher zuerst die Auslosung; das Finale rückt erst nach dem Löschen
        // nach, weil (TurnierInstanzId, Reihenfolge) eindeutig ist.
        _context.Auslosungen.RemoveRange(vorlage.Auslosungen.Where(a => a.TurnierRundeId == platzDrei.Id));
        _context.TurnierRunden.Remove(platzDrei);
        await _context.SaveChangesAsync(ct);

        finale.Reihenfolge = platzDrei.Reihenfolge;
        return 1;
    }

    // ── Beschreibung und Struktur ──────────────────────────────────────────────

    /// <summary>Issue #374: zieht die Beschreibung auf den Seed-Stand nach — nur, solange sie noch wörtlich dem
    /// alten Seed-Text entspricht.</summary>
    private int BeschreibungenAktualisieren()
    {
        int aktualisiert = 0;
        foreach (var (vorlage, soll) in MitSeedStand())
        {
            if (!TurnierVorlagenSeedDaten.AlteBeschreibungen.TryGetValue(Schluessel(vorlage), out string? alt)
                || vorlage.Beschreibung != alt) continue;

            vorlage.Beschreibung = soll.Beschreibung;
            aktualisiert++;
        }
        return aktualisiert;
    }

    /// <summary>Issue #350: ältere Seed-Stände legten Gruppenphasen ohne <c>GruppenAnzahl</c> und Vorlagen mit
    /// besten Gruppendritten (EM 24) ohne Übergangsregeln an — solche Vorlagen scheiterten beim Speichern am
    /// TurnierValidator bzw. konnten die Dritten nicht weiterleiten. Die Reihenfolge zählt: die Übergangsregeln
    /// setzen die Gruppenanzahl voraus, der Eingang über Übergangsregeln die Regeln.</summary>
    private async Task<int> StrukturErgaenzenAsync(CancellationToken ct)
    {
        var uebergangsregeln = await UebergangsregelnDerVorlagenAsync(ct);

        int ergaenzt = 0;
        foreach (var (vorlage, soll) in MitSeedStand())
        {
            ergaenzt += SeedVorlagenAbgleich.GruppenAnzahlErgaenzen(vorlage, soll);
            ergaenzt += UebergangsregelnErgaenzen(vorlage, soll, uebergangsregeln);
            ergaenzt += SeedVorlagenAbgleich.EingangUeberUebergangsregelnSetzen(vorlage, soll, uebergangsregeln);
        }
        return ergaenzt;
    }

    private Task<List<Uebergangsregel>> UebergangsregelnDerVorlagenAsync(CancellationToken ct)
    {
        var rundenIds = _vorlagen.SelectMany(v => v.Runden).Select(r => r.Id).ToList();
        return _context.Uebergangsregeln.Where(u => rundenIds.Contains(u.QuellRundeId)).ToListAsync(ct);
    }

    // Zählt die Vorlage, nicht ihre Regeln; die neuen Regeln kommen zu den vorhandenen, damit der Eingang über
    // Übergangsregeln sie schon vor dem Speichern sieht.
    private int UebergangsregelnErgaenzen(TurnierInstanz vorlage, TurnierInstanz soll, List<Uebergangsregel> vorhandeneRegeln)
    {
        var neueRegeln = SeedVorlagenAbgleich.FehlendeUebergangsregeln(vorlage, soll, vorhandeneRegeln);
        _context.Uebergangsregeln.AddRange(neueRegeln);
        vorhandeneRegeln.AddRange(neueRegeln);
        return neueRegeln.Count > 0 ? 1 : 0;
    }

    /// <summary>Issue #372/#466: trägt feste Vorbelegungen nach (K.-o.-Baum samt Kombinationstabelle, fest besetzte Gruppen).</summary>
    private int FesteVorbelegungenErgaenzen()
    {
        int ergaenzt = 0;
        foreach (var (vorlage, soll) in MitSeedStand())
        {
            var vorbelegungen = SeedVorlagenAbgleich.FesteVorbelegungenUebertragen(vorlage, soll);
            if (vorbelegungen.Count == 0) continue;

            _context.Auslosungen.AddRange(vorbelegungen);
            ergaenzt++;
        }
        return ergaenzt;
    }

    /// <summary>Issue #393: Standard-Rangliste dort, wo der Seed eine vorgibt und die Vorlage noch keine hat.</summary>
    private int StandardRanglistenErgaenzen() =>
        MitSeedStand().Sum(paar => SeedVorlagenAbgleich.StandardRanglisteErgaenzen(paar.Vorlage, paar.Soll));

    /// <summary>Issue #393: DFB-Pokal-Runden von „Heimrecht nach Los“ auf „Heimrecht für Unterklassige“ — nur, wo
    /// die Vorgabe noch dem alten Seed-Stand entspricht.</summary>
    private int UnterklassigenHeimrechtUebernehmen() =>
        MitSeedStand().Sum(paar => SeedVorlagenAbgleich.UnterklassigenHeimrechtUebernehmen(paar.Vorlage, paar.Soll));

    /// <summary>Issue #469: die Conference League lost seit 2024/25 einen Gegner je Topf aus.</summary>
    private int GegnerJeTopfUebernehmen() =>
        MitSeedStand().Sum(paar => SeedVorlagenAbgleich.GegnerJeTopfUebernehmen(paar.Vorlage, paar.Soll));

    /// <summary>Issue #420: Tabellenbereiche der Liga-Vorlagen und der Nations League — nur in Runden, die noch
    /// keine Bereiche haben und dem Seed entsprechen.</summary>
    private int TabellenBereicheErgaenzen()
    {
        int ergaenzt = 0;
        foreach (var (vorlage, soll) in MitSeedStand())
        {
            var bereiche = SeedVorlagenAbgleich.FehlendeTabellenBereiche(vorlage, soll);
            if (bereiche.Count == 0) continue;

            _context.TabellenBereiche.AddRange(bereiche);
            ergaenzt++;
        }
        return ergaenzt;
    }
}

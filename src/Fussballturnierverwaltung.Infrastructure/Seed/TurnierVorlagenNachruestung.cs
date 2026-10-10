using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Fussballturnierverwaltung.Infrastructure.Seed.Daten;
using Microsoft.EntityFrameworkCore;

namespace Fussballturnierverwaltung.Infrastructure.Seed;

/// <summary>Zieht bestehende Turniervorlagen auf neue Seed-Stände nach (Issue #440: aus
/// <see cref="SeedDatenService"/> herausgelöst, weil dieser Teil mit jedem Vorlagen-Issue wächst).</summary>
internal sealed class TurnierVorlagenNachruestung
{
    private readonly TurnierverwaltungDbContext _context;
    private readonly Action<int, string> _protokollieren;

    public TurnierVorlagenNachruestung(TurnierverwaltungDbContext context, Action<int, string> protokollieren)
    {
        _context = context;
        _protokollieren = protokollieren;
    }

    public async Task AusfuehrenAsync(CancellationToken cancellationToken)
    {
        await TurnierVorlagenUmbenennen(cancellationToken);
        await TurnierVorlagenNachruesten(cancellationToken);
        await TurnierVorlagenFilterNachruesten(cancellationToken);
        await TurnierVorlagenRundenartNachruesten(cancellationToken);
        await TurnierVorlagenLigaphaseRegelwerkNachruesten(cancellationToken);
        await TurnierVorlagenCopaRegelwerkNachruesten(cancellationToken);
        // Vor dem Regelwerk: die Umstellung ordnet Runden über Reihenfolge + Name zu (Issue #375).
        await TurnierVorlageEm8SpielUmPlatzDreiEntfernen(cancellationToken);
        await TurnierVorlagenEm8RegelwerkNachruesten(cancellationToken);
        await TurnierVorlagenBeschreibungNachruesten(cancellationToken);
        await TurnierVorlagenStrukturNachruesten(cancellationToken);
        await TurnierVorlagenFesteKoBaeumeNachruesten(cancellationToken);
    }

    private void Protokollieren(int anzahl, string text) => _protokollieren(anzahl, text);

    // Issue #367: Vorlagen sind über Name + Variante identifiziert (mehrere "FIFA WM" mit verschiedener Variante).
    private static (string Name, string Variante) Schluessel(TurnierInstanz vorlage) => (vorlage.Name, vorlage.Variante);

    /// <summary>Issue #294: seit der Vorlage/Instanz-Vereinheitlichung sind Vorlagen ganz normale
    /// <see cref="Domain.Entitaeten.Turniere.TurnierInstanz"/>-Zeilen mit
    /// <see cref="Domain.Enums.TurnierStatus.Vorlage"/> — Name **und** Status bilden die Identität, damit
    /// ein echtes Turnier mit zufällig gleichem Namen nicht fälschlich als "bereits vorhanden" zählt.
    /// Ersetzt die vormals drei getrennten Methoden (Vorlagen/Runden/Runden-Regelwerke), da eine neue
    /// Vorlage jetzt als vollständiger Objektgraph (inkl. Runden + Übergangsregeln) in einem Zug eingefügt wird.</summary>
    private async Task TurnierVorlagenNachruesten(CancellationToken ct)
    {
        var vorhandeneSchluessel = (await _context.TurnierInstanzen
            .Where(i => i.Status == Domain.Enums.TurnierStatus.Vorlage)
            .Select(v => new { v.Name, v.Variante })
            .ToListAsync(ct))
            .Select(v => (v.Name, v.Variante))
            .ToHashSet();

        var regelwerkLookup = await _context.Regelwerke
            .ToDictionaryAsync(r => r.Name, r => r.Id, ct);
        var verbandLookup = await _context.Verbaende
            .ToDictionaryAsync(v => v.Kurzname, v => v.Id, ct);
        var landLookup = await _context.Laender
            .ToDictionaryAsync(l => l.FifaCode, l => l.Id, ct);

        var alleVorlagen = TurnierVorlagenSeedDaten.Erstellen(
            regelwerkLookup, verbandLookup, landLookup, await RanglistenQuellenLookupAsync(ct));
        int hinzugefuegt = 0;

        foreach (var vorlage in alleVorlagen)
        {
            if (vorhandeneSchluessel.Contains(Schluessel(vorlage))) continue;
            _context.TurnierInstanzen.Add(vorlage);
            hinzugefuegt++;
        }

        if (hinzugefuegt > 0)
        {
            await _context.SaveChangesAsync(ct);
            Protokollieren(hinzugefuegt, "neue Turniervorlagen nachgerüstet.");
        }
    }

    /// <summary>Issue #189-Vorarbeit: VerbandId/LandId/MannschaftsartFilter wurden nachträglich zu
    /// TurnierInstanz hinzugefügt und beim ursprünglichen Vorlagen-Seed nicht rückwirkend gepflegt. Trägt
    /// sie bei bereits vorhandenen Vorlagen nach — nur wo das Feld noch auf dem technischen Default steht,
    /// damit eine eventuell schon manuell in der UI vorgenommene Anpassung nicht überschrieben wird.</summary>
    private async Task TurnierVorlagenFilterNachruesten(CancellationToken ct)
    {
        var verbandLookup = await _context.Verbaende
            .ToDictionaryAsync(v => v.Kurzname, v => v.Id, ct);
        var landLookup = await _context.Laender
            .ToDictionaryAsync(l => l.FifaCode, l => l.Id, ct);
        var sollFilter = TurnierVorlagenSeedDaten.FilterNachVorlagenSchluessel(verbandLookup, landLookup);

        var vorhandeneVorlagen = await _context.TurnierInstanzen
            .Where(i => i.Status == Domain.Enums.TurnierStatus.Vorlage)
            .ToListAsync(ct);

        int aktualisiert = 0;
        foreach (var vorlage in vorhandeneVorlagen)
        {
            if (!sollFilter.TryGetValue(Schluessel(vorlage), out var soll)) continue;
            if (vorlage.VerbandId is not null || vorlage.LandId is not null
                || vorlage.MannschaftsartFilter != Domain.Enums.MannschaftsartFilter.Alle) continue;

            vorlage.VerbandId = soll.VerbandId;
            vorlage.LandId = soll.LandId;
            vorlage.MannschaftsartFilter = soll.MannschaftsartFilter;
            aktualisiert++;
        }

        if (aktualisiert > 0)
        {
            await _context.SaveChangesAsync(ct);
            Protokollieren(aktualisiert, "Turniervorlagen um Verband-/Land-/Mannschaftsartfilter ergänzt.");
        }
    }

    /// <summary>Issue #343: der #328-Fix (CL/EL/ECL-Ligaphase TopfLiga statt SchweizerSystem) änderte nur die
    /// Seed-Daten — <see cref="TurnierVorlagenNachruesten"/> ergänzt ausschließlich fehlende Vorlagen, bestehende
    /// Datenbanken behielten daher die falsche Rundenart. Korrigiert gezielt nur diesen bekannten Altwert (Runde
    /// derselben Reihenfolge und desselben Namens steht noch auf SchweizerSystem, Seed sagt TopfLiga), damit
    /// manuelle Anpassungen an Vorlagen nicht überschrieben werden.</summary>
    private async Task TurnierVorlagenRundenartNachruesten(CancellationToken ct)
    {
        var regelwerkLookup = await _context.Regelwerke
            .ToDictionaryAsync(r => r.Name, r => r.Id, ct);
        var verbandLookup = await _context.Verbaende
            .ToDictionaryAsync(v => v.Kurzname, v => v.Id, ct);
        var landLookup = await _context.Laender
            .ToDictionaryAsync(l => l.FifaCode, l => l.Id, ct);
        var sollVorlagen = TurnierVorlagenSeedDaten.Erstellen(regelwerkLookup, verbandLookup, landLookup)
            .ToDictionary(Schluessel);

        var vorhandeneVorlagen = await _context.TurnierInstanzen
            .Include(i => i.Runden)
            .Where(i => i.Status == Domain.Enums.TurnierStatus.Vorlage)
            .ToListAsync(ct);

        int aktualisiert = 0;
        foreach (var vorlage in vorhandeneVorlagen)
        {
            if (!sollVorlagen.TryGetValue(Schluessel(vorlage), out var soll)) continue;
            foreach (var runde in vorlage.Runden.Where(r => r.Rundenart == Domain.Enums.Rundenart.SchweizerSystem))
            {
                var sollRunde = soll.Runden.FirstOrDefault(r => r.Reihenfolge == runde.Reihenfolge && r.Name == runde.Name);
                if (sollRunde?.Rundenart != Domain.Enums.Rundenart.TopfLiga) continue;

                runde.Rundenart = Domain.Enums.Rundenart.TopfLiga;
                runde.KonfigurationJson = null; // AnzahlSpieltage ist nur für SchweizerSystem relevant
                aktualisiert++;
            }
        }

        if (aktualisiert > 0)
        {
            await _context.SaveChangesAsync(ct);
            Protokollieren(aktualisiert, "Vorlagen-Runden von SchweizerSystem auf TopfLiga korrigiert.");
        }
    }

    /// <summary>Issue #353: CL/EL/ECL ab 2024/25 haben ein eigenes Regelwerk mit der Rangfolge der Ligaphase,
    /// bestehende Vorlagen behielten "UEFA Clubwettbewerb Standard" (direkter Vergleich zuerst).</summary>
    private Task TurnierVorlagenLigaphaseRegelwerkNachruesten(CancellationToken ct) =>
        TurnierVorlagenRegelwerkUmstellen(
            TurnierVorlagenSeedDaten.UEFA_CLUB_STANDARD, TurnierVorlagenSeedDaten.UEFA_LIGAPHASE,
            "Vorlagen-Runden auf das Regelwerk der UEFA-Ligaphase umgestellt.", ct);

    /// <summary>Issue #374: die Copa América spielt Viertelfinale, Halbfinale und Spiel um Platz 3 ohne
    /// Verlängerung, bestehende Vorlagen behielten dort das FIFA-Regelwerk (Verlängerung + Elfmeterschießen).</summary>
    private Task TurnierVorlagenCopaRegelwerkNachruesten(CancellationToken ct) =>
        TurnierVorlagenRegelwerkUmstellen(
            TurnierVorlagenSeedDaten.FIFA_STANDARD_MODERN, TurnierVorlagenSeedDaten.COPA_DIREKTES_ELFMETERSCHIESSEN,
            "Vorlagen-Runden der Copa América auf direktes Elfmeterschießen umgestellt.", ct);

    /// <summary>Issue #375: die EM-Endrunden 1984–1992 liefen mit zwei Punkten je Sieg, Verlängerung und
    /// Elfmeterschießen — die frühere Sammelvorlage stand auf Wiederholungsspiel + Los.</summary>
    private Task TurnierVorlagenEm8RegelwerkNachruesten(CancellationToken ct) =>
        TurnierVorlagenRegelwerkUmstellen(
            TurnierVorlagenSeedDaten.HISTORISCH_WIEDERHOLUNGSSPIEL_LOS, TurnierVorlagenSeedDaten.FIFA_HISTORISCH_2_PUNKTE,
            "Vorlagen-Runden der EM mit 8 Teams auf das 2-Punkte-Regelwerk mit Verlängerung + Elfmeterschießen umgestellt.", ct);

    /// <summary>Issue #375: die frühere Sammelvorlage „8 Teams · 1960–1992“ hatte ein Spiel um Platz 3, das es
    /// im Format 1984–1992 nicht mehr gab. Entfernt die Runde samt ihrer festen Paarung und rückt das Finale
    /// nach — nur, solange die Runden noch dem alten Seed-Stand entsprechen und keine Übergangsregel an der
    /// Runde hängt; eine vom Nutzer umgebaute Vorlage bleibt unangetastet.</summary>
    private async Task TurnierVorlageEm8SpielUmPlatzDreiEntfernen(CancellationToken ct)
    {
        var vorlage = await _context.TurnierInstanzen
            .Include(i => i.Runden)
            .Include(i => i.Auslosungen)
            .FirstOrDefaultAsync(i => i.Status == Domain.Enums.TurnierStatus.Vorlage
                                   && i.Name == "UEFA EM"
                                   && i.Variante == TurnierVorlagenSeedDaten.EM_8_TEAMS_1984_BIS_1992, ct);
        if (vorlage is null) return;

        var runden = vorlage.Runden.OrderBy(r => r.Reihenfolge).ToList();
        if (!runden.Select(r => (r.Name, r.Rundenart)).SequenceEqual(_alteRundenEm8)) return;

        var platzDrei = runden[2];
        var finale = runden[3];
        if (await _context.Uebergangsregeln.AnyAsync(u => u.QuellRundeId == platzDrei.Id || u.ZielRundeId == platzDrei.Id, ct))
            return;

        // Auslosung → Runde ist NoAction, daher zuerst die Auslosung; das Finale rückt erst nach dem Löschen
        // nach, weil (TurnierInstanzId, Reihenfolge) eindeutig ist.
        _context.Auslosungen.RemoveRange(vorlage.Auslosungen.Where(a => a.TurnierRundeId == platzDrei.Id));
        _context.TurnierRunden.Remove(platzDrei);
        await _context.SaveChangesAsync(ct);

        finale.Reihenfolge = platzDrei.Reihenfolge;
        await _context.SaveChangesAsync(ct);
        Protokollieren(1, "Spiel um Platz 3 aus der Vorlage „UEFA EM · 8 Teams · 1984–1992“ entfernt.");
    }

    // Runden der EM-Sammelvorlage „8 Teams · 1960–1992“ vor #375.
    private static readonly (string Name, Domain.Enums.Rundenart Rundenart)[] _alteRundenEm8 =
    [
        ("Gruppenphase",     Domain.Enums.Rundenart.Gruppenphase),
        ("Halbfinale",       Domain.Enums.Rundenart.KoRunde),
        ("Spiel um Platz 3", Domain.Enums.Rundenart.SpielUmPlatzDrei),
        ("Finale",           Domain.Enums.Rundenart.Finale),
    ];

    /// <summary>Issue #374: zieht die Beschreibung bestehender Vorlagen auf den Seed-Stand nach — nur, solange
    /// sie noch wörtlich dem alten Seed-Text entspricht, eine vom Nutzer geänderte Beschreibung bleibt erhalten.</summary>
    private async Task TurnierVorlagenBeschreibungNachruesten(CancellationToken ct)
    {
        var sollVorlagen = TurnierVorlagenSeedDaten.Erstellen().ToDictionary(Schluessel);
        var vorhandeneVorlagen = await _context.TurnierInstanzen
            .Where(i => i.Status == Domain.Enums.TurnierStatus.Vorlage)
            .ToListAsync(ct);

        int aktualisiert = 0;
        foreach (var vorlage in vorhandeneVorlagen)
        {
            if (!TurnierVorlagenSeedDaten.AlteBeschreibungen.TryGetValue(Schluessel(vorlage), out string? alt)
                || vorlage.Beschreibung != alt) continue;

            vorlage.Beschreibung = sollVorlagen[Schluessel(vorlage)].Beschreibung;
            aktualisiert++;
        }

        if (aktualisiert > 0)
        {
            await _context.SaveChangesAsync(ct);
            Protokollieren(aktualisiert, "Beschreibungen von Turniervorlagen aktualisiert.");
        }
    }

    /// <summary><see cref="TurnierVorlagenNachruesten"/> ergänzt nur fehlende Vorlagen — wechselt der Seed das
    /// Regelwerk einer Runde, behalten bestehende Vorlagen das alte. Stellt nur Runden um, die noch auf genau
    /// diesem alten Seed-Wert stehen und deren Seed-Gegenstück (Reihenfolge + Name) das neue Regelwerk nutzt —
    /// ein vom Nutzer gewähltes anderes Regelwerk bleibt erhalten.</summary>
    private async Task TurnierVorlagenRegelwerkUmstellen(
        string altesRegelwerk, string neuesRegelwerk, string protokollText, CancellationToken ct)
    {
        var regelwerkLookup = await _context.Regelwerke
            .ToDictionaryAsync(r => r.Name, r => r.Id, ct);
        if (!regelwerkLookup.TryGetValue(altesRegelwerk, out int altId)
            || !regelwerkLookup.TryGetValue(neuesRegelwerk, out int neuId)) return;

        var sollVorlagen = TurnierVorlagenSeedDaten.Erstellen(regelwerkLookup).ToDictionary(Schluessel);

        var vorhandeneVorlagen = await _context.TurnierInstanzen
            .Include(i => i.Runden)
            .Where(i => i.Status == Domain.Enums.TurnierStatus.Vorlage)
            .ToListAsync(ct);

        int aktualisiert = 0;
        foreach (var vorlage in vorhandeneVorlagen)
        {
            if (!sollVorlagen.TryGetValue(Schluessel(vorlage), out var soll)) continue;
            foreach (var runde in vorlage.Runden.Where(r => r.RegelwerkId == altId))
            {
                var sollRunde = soll.Runden.FirstOrDefault(r => r.Reihenfolge == runde.Reihenfolge && r.Name == runde.Name);
                if (sollRunde?.RegelwerkId != neuId) continue;

                runde.RegelwerkId = neuId;
                aktualisiert++;
            }
        }

        if (aktualisiert > 0)
        {
            await _context.SaveChangesAsync(ct);
            Protokollieren(aktualisiert, protokollText);
        }
    }

    // Seed-Stand der WM-24-Vorlage vor #350 (damals als "1982–1994" geführt).
    private const string ALTE_BESCHREIBUNG_WM24 = "6 Dreiergruppen + Achtelfinale-Runde → K.-o.";

    // Seed-Stand der EM-Vorlage mit 8 Teams vor #375 (damals als "1960–1992" geführt).
    private static readonly DateOnly _altesStartdatumEm8 = new(1960, 1, 1);

    /// <summary>Issue #350/#367: benennt Bestands-Vorlagen um, deren Seed-Name sich geändert hat (seit #367 inkl.
    /// Verschiebung des Klammerzusatzes in <see cref="TurnierInstanz.Variante"/>) — muss vor
    /// <see cref="TurnierVorlagenNachruesten"/> laufen, sonst entstünde unter dem neuen Schlüssel ein Duplikat. Nur,
    /// solange unter dem neuen Namen + Variante noch keine Vorlage existiert. Beschreibung/Enddatum werden nur mitgezogen,
    /// wenn sie noch dem alten Seed-Stand entsprechen (keine manuelle Anpassung überschreiben).</summary>
    private async Task TurnierVorlagenUmbenennen(CancellationToken ct)
    {
        var vorlagen = await _context.TurnierInstanzen
            .Where(i => i.Status == Domain.Enums.TurnierStatus.Vorlage)
            .ToListAsync(ct);
        var sollNachSchluessel = TurnierVorlagenSeedDaten.Erstellen().ToDictionary(Schluessel);

        int umbenannt = 0;
        foreach (var (alterName, neu) in TurnierVorlagenSeedDaten.UmbenennungenNachAltemNamen)
        {
            var alt = vorlagen.FirstOrDefault(v => v.Name == alterName);
            if (alt is null || vorlagen.Any(v => Schluessel(v) == neu)) continue;

            var soll = sollNachSchluessel[neu];
            alt.Name = neu.Name;
            alt.Variante = neu.Variante;
            if (alt.Beschreibung == ALTE_BESCHREIBUNG_WM24) alt.Beschreibung = soll.Beschreibung;
            if (alt.Enddatum == new DateOnly(1994, 12, 31)) alt.Enddatum = soll.Enddatum;
            if (alt.Startdatum == _altesStartdatumEm8) alt.Startdatum = soll.Startdatum;
            umbenannt++;
        }

        // Issue #375: Vorlagen, die schon Name + Variante tragen und nur eine neue Variante bekommen.
        foreach (var (alt, neueVariante) in TurnierVorlagenSeedDaten.VariantenUmbenennungen)
        {
            var neu = (alt.Name, neueVariante);
            var vorlage = vorlagen.FirstOrDefault(v => Schluessel(v) == alt);
            if (vorlage is null || vorlagen.Any(v => Schluessel(v) == neu)) continue;

            vorlage.Variante = neueVariante;
            if (vorlage.Startdatum == _altesStartdatumEm8) vorlage.Startdatum = sollNachSchluessel[neu].Startdatum;
            umbenannt++;
        }

        if (umbenannt > 0)
        {
            await _context.SaveChangesAsync(ct);
            Protokollieren(umbenannt, "Turniervorlagen umbenannt.");
        }
    }

    /// <summary>Issue #350: ältere Seed-Stände legten Gruppenphasen ohne <c>GruppenAnzahl</c> und Vorlagen mit
    /// besten Gruppendritten (EM 24) ohne Übergangsregeln an — solche Vorlagen scheiterten beim Speichern am
    /// TurnierValidator bzw. konnten die Dritten nicht weiterleiten. Ergänzt ausschließlich fehlende Werte:
    /// <c>GruppenAnzahl</c> nur, wo sie leer ist; Übergangsregeln nur bei Vorlagen ganz ohne Regeln, deren
    /// beteiligte Runden noch dem Seed entsprechen; <c>EingangUeberUebergangsregeln</c> nur bei Runden mit
    /// tatsächlich eingehenden Regeln (fehlte auch beim Achtelfinale von CL/EL/ECL ab 2024/25).</summary>
    private async Task TurnierVorlagenStrukturNachruesten(CancellationToken ct)
    {
        var sollVorlagen = TurnierVorlagenSeedDaten.Erstellen().ToDictionary(Schluessel);

        var vorhandeneVorlagen = await _context.TurnierInstanzen
            .Include(i => i.Runden)
            .Where(i => i.Status == Domain.Enums.TurnierStatus.Vorlage)
            .ToListAsync(ct);
        var rundenIds = vorhandeneVorlagen.SelectMany(v => v.Runden).Select(r => r.Id).ToList();
        var vorhandeneRegeln = await _context.Uebergangsregeln
            .Where(u => rundenIds.Contains(u.QuellRundeId))
            .ToListAsync(ct);

        int aktualisiert = 0;
        foreach (var vorlage in vorhandeneVorlagen)
        {
            if (!sollVorlagen.TryGetValue(Schluessel(vorlage), out var soll)) continue;

            TurnierRunde? Zuordnen(TurnierRunde sollRunde) =>
                vorlage.Runden.FirstOrDefault(r => r.Reihenfolge == sollRunde.Reihenfolge && r.Name == sollRunde.Name);

            foreach (var sollRunde in soll.Runden.Where(r => r.GruppenAnzahl is not null))
            {
                var runde = Zuordnen(sollRunde);
                if (runde is null || runde.GruppenAnzahl is not null || runde.Rundenart != sollRunde.Rundenart) continue;
                runde.GruppenAnzahl = sollRunde.GruppenAnzahl;
                aktualisiert++;
            }

            var eigeneRundenIds = vorlage.Runden.Select(r => r.Id).ToHashSet();
            var sollRegeln = soll.Runden.SelectMany(r => r.Uebergangsregeln).ToList();
            if (sollRegeln.Count > 0 && !vorhandeneRegeln.Any(u => eigeneRundenIds.Contains(u.QuellRundeId)))
            {
                var zuordnung = sollRegeln
                    .SelectMany(u => new[] { u.QuellRunde, u.ZielRunde })
                    .Distinct()
                    .ToDictionary(r => r, Zuordnen);
                bool passt = zuordnung.All(z => z.Value is not null
                    && z.Value.Rundenart == z.Key.Rundenart
                    && z.Value.EingangsMannschaftsanzahl == z.Key.EingangsMannschaftsanzahl
                    && z.Value.AusgangsMannschaftsanzahl == z.Key.AusgangsMannschaftsanzahl
                    && z.Value.GruppenAnzahl == z.Key.GruppenAnzahl);
                if (passt)
                {
                    foreach (var sollRegel in sollRegeln)
                    {
                        var regel = new Uebergangsregel
                        {
                            QuellRundeId   = zuordnung[sollRegel.QuellRunde]!.Id,
                            ZielRundeId    = zuordnung[sollRegel.ZielRunde]!.Id,
                            GruppeIndex    = sollRegel.GruppeIndex,
                            PlatzierungVon = sollRegel.PlatzierungVon,
                            PlatzierungBis = sollRegel.PlatzierungBis,
                            AnzahlBeste    = sollRegel.AnzahlBeste,
                        };
                        _context.Uebergangsregeln.Add(regel);
                        vorhandeneRegeln.Add(regel);
                    }
                    aktualisiert++;
                }
            }

            foreach (var sollRunde in soll.Runden.Where(r => r.EingangUeberUebergangsregeln))
            {
                var runde = Zuordnen(sollRunde);
                if (runde is null || runde.EingangUeberUebergangsregeln) continue;
                if (!vorhandeneRegeln.Any(u => u.ZielRundeId == runde.Id)) continue;
                runde.EingangUeberUebergangsregeln = true;
                aktualisiert++;
            }
        }

        if (aktualisiert > 0)
        {
            await _context.SaveChangesAsync(ct);
            Protokollieren(aktualisiert, "Struktur-Ergänzungen an Turniervorlagen nachgerüstet.");
        }
    }

    /// <summary>Issue #372: trägt den realen festen K.-o.-Baum (Vorbelegung + ggf. Kombinationstabelle) bei
    /// bestehenden Vorlagen nach. Nur, wenn ALLE betroffenen Runden noch dem Seed entsprechen (Reihenfolge,
    /// Name, Rundenart, Eingangsanzahl) und keine davon schon eine Auslosung hat — eine manuell angelegte
    /// Vorbelegung wird nie überschrieben, ein halb nachgerüsteter Baum entsteht nie.</summary>
    private async Task TurnierVorlagenFesteKoBaeumeNachruesten(CancellationToken ct)
    {
        var sollVorlagen = TurnierVorlagenSeedDaten.Erstellen()
            .Where(v => v.Auslosungen.Count > 0)
            .ToDictionary(Schluessel);

        var vorhandeneVorlagen = await _context.TurnierInstanzen
            .Include(i => i.Runden)
            .Include(i => i.Auslosungen)
            .Where(i => i.Status == Domain.Enums.TurnierStatus.Vorlage)
            .ToListAsync(ct);

        int aktualisiert = 0;
        foreach (var vorlage in vorhandeneVorlagen)
        {
            if (!sollVorlagen.TryGetValue(Schluessel(vorlage), out var soll)) continue;

            var zuordnung = soll.Auslosungen.ToDictionary(
                a => a,
                a => vorlage.Runden.FirstOrDefault(r =>
                    r.Reihenfolge == a.TurnierRunde.Reihenfolge
                    && r.Name == a.TurnierRunde.Name
                    && r.Rundenart == a.TurnierRunde.Rundenart
                    && r.EingangsMannschaftsanzahl == a.TurnierRunde.EingangsMannschaftsanzahl));
            if (zuordnung.Values.Any(r => r is null)) continue;
            if (zuordnung.Values.Any(r => vorlage.Auslosungen.Any(a => a.TurnierRundeId == r!.Id))) continue;

            foreach (var (sollAuslosung, runde) in zuordnung)
            {
                runde!.PaarungFestVorgegeben = true;
                runde.KombinationstabelleJson ??= sollAuslosung.TurnierRunde.KombinationstabelleJson;
                var auslosung = new Domain.Entitaeten.Auslosungen.Auslosung
                {
                    TurnierInstanzId = vorlage.Id,
                    TurnierRundeId   = runde.Id,
                    Name             = sollAuslosung.Name,
                    Auslosungsart    = sollAuslosung.Auslosungsart,
                    Status           = sollAuslosung.Status,
                };
                foreach (var e in sollAuslosung.Ergebnisse)
                    auslosung.Ergebnisse.Add(new Domain.Entitaeten.Auslosungen.AuslosungsErgebnis
                    {
                        PaarungsNummer           = e.PaarungsNummer,
                        HeimPlatzhalterName      = e.HeimPlatzhalterName,
                        AuswaertsPlatzhalterName = e.AuswaertsPlatzhalterName,
                    });
                _context.Auslosungen.Add(auslosung);
            }
            aktualisiert++;
        }

        if (aktualisiert > 0)
        {
            await _context.SaveChangesAsync(ct);
            Protokollieren(aktualisiert, "Turniervorlagen um festen K.-o.-Baum ergänzt.");
        }
    }

    // Quellennamen sind nicht eindeutig erzwungen (frei anlegbar im Quellen-Dialog) — bei Dubletten gilt die älteste.
    public async Task<Dictionary<string, int>> RanglistenQuellenLookupAsync(CancellationToken ct) =>
        (await _context.RanglistenQuellen.Select(q => new { q.Name, q.Id }).ToListAsync(ct))
            .GroupBy(q => q.Name)
            .ToDictionary(g => g.Key, g => g.Min(q => q.Id));
}

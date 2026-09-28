using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Fussballturnierverwaltung.Infrastructure.Seed.Daten;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Fussballturnierverwaltung.Infrastructure.Seed;

public class SeedDatenService
{
    private readonly TurnierverwaltungDbContext _context;
    private readonly ILogger<SeedDatenService> _logger;

    public SeedDatenService(TurnierverwaltungDbContext context, ILogger<SeedDatenService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task InitialisierenAsync(CancellationToken cancellationToken = default)
    {
        if (!await _context.Verbaende.AnyAsync(cancellationToken))
        {
            await ErstSeedAsync(cancellationToken);
        }
        else
        {
            _logger.LogInformation("Seed-Daten bereits vorhanden – Basis-Seed übersprungen.");
        }

        await LaenderNachruesten(cancellationToken);
        await BildmaterialNachruesten(cancellationToken);
        await MannschaftenNachruesten(cancellationToken);
        await MannschaftsBildmaterialNachruesten(cancellationToken);
        await RegelwerkeNachruesten(cancellationToken);
        await TurnierVorlagenUmbenennen(cancellationToken);
        await TurnierVorlagenNachruesten(cancellationToken);
        await TurnierVorlagenFilterNachruesten(cancellationToken);
        await TurnierVorlagenRundenartNachruesten(cancellationToken);
        await TurnierVorlagenStrukturNachruesten(cancellationToken);
        await TurnierVorlagenFesteKoBaeumeNachruesten(cancellationToken);
        await RanglistenQuellenNachruesten(cancellationToken);
    }

    private async Task LaenderNachruesten(CancellationToken ct)
    {
        var verbaendeLookup = await _context.Verbaende
            .ToDictionaryAsync(v => v.Kurzname, v => v.Id, ct);

        var vorhandeneCodes = await _context.Laender
            .Select(l => l.FifaCode)
            .ToHashSetAsync(ct);

        var alleLaender = LaenderSeedDaten.Erstellen(verbaendeLookup);
        var neue = alleLaender.Where(l => !vorhandeneCodes.Contains(l.FifaCode)).ToList();

        if (neue.Count > 0)
        {
            _context.Laender.AddRange(neue);
            await _context.SaveChangesAsync(ct);
            _logger.LogInformation("{Anzahl} Länder nachgerüstet.", neue.Count);
        }
    }

    /// <summary>Issue #233: befüllt Flagge/Logo bei bereits vorhandenen Länder-/Verbands-Datensätzen ohne
    /// Bild nachträglich (Nachrüsten-Pattern, analog zu <see cref="LaenderNachruesten"/>) — betrifft sowohl
    /// frisch angelegte Datensätze aus <see cref="ErstSeedAsync"/> als auch ältere Datenbanken ohne Bildspalten.</summary>
    private async Task BildmaterialNachruesten(CancellationToken ct)
    {
        var verbaendeOhneLogo = await _context.Verbaende.Where(v => v.Logo == null).ToListAsync(ct);
        int aktualisiert = 0;
        foreach (var verband in verbaendeOhneLogo)
        {
            var logo = SeedBildLader.Laden("Verbandslogos", verband.Kurzname);
            if (logo is null) continue;
            verband.Logo = logo;
            aktualisiert++;
        }

        var laenderOhneFlagge = await _context.Laender.Where(l => l.Flagge == null).ToListAsync(ct);
        foreach (var land in laenderOhneFlagge)
        {
            var flagge = SeedBildLader.Laden("Flaggen", land.FifaCode);
            if (flagge is null) continue;
            land.Flagge = flagge;
            aktualisiert++;
        }

        if (aktualisiert > 0)
        {
            await _context.SaveChangesAsync(ct);
            _logger.LogInformation("{Anzahl} Verbands-/Länder-Bilder nachgerüstet.", aktualisiert);
        }
    }

    /// <summary>Issue #233: befüllt Wappen bei bereits vorhandenen Mannschaften ohne Bild nachträglich —
    /// getrennt von <see cref="BildmaterialNachruesten"/>, da Mannschaften selbst erst durch
    /// <see cref="MannschaftenNachruesten"/> angelegt werden (Reihenfolge in <see cref="InitialisierenAsync"/>).</summary>
    private async Task MannschaftsBildmaterialNachruesten(CancellationToken ct)
    {
        var mannschaftenOhneWappen = await _context.Mannschaften.Where(m => m.Wappen == null).ToListAsync(ct);
        int aktualisiert = 0;
        foreach (var mannschaft in mannschaftenOhneWappen)
        {
            var kategorie = mannschaft.Mannschaftsart == Domain.Enums.Mannschaftsart.Nationalmannschaft
                ? "Nationalmannschaften"
                : "Vereinswappen";
            var wappen = SeedBildLader.Laden(kategorie, mannschaft.Kurzname);
            if (wappen is null) continue;
            mannschaft.Wappen = wappen;
            aktualisiert++;
        }

        if (aktualisiert > 0)
        {
            await _context.SaveChangesAsync(ct);
            _logger.LogInformation("{Anzahl} Mannschafts-Wappen nachgerüstet.", aktualisiert);
        }
    }

    private async Task MannschaftenNachruesten(CancellationToken ct)
    {
        var laender = await _context.Laender.ToListAsync(ct);
        var laenderLookup = laender.ToDictionary(l => l.FifaCode, l => l.Id);

        var sollMannschaften = NationalmannschaftenSeedDaten.Erstellen(laender, laenderLookup)
            .Concat(VereinsmannschaftenSeedDaten.Erstellen(laenderLookup))
            .ToList();

        var vorhandeneSchluessel = (await _context.Mannschaften
                .Select(m => new { m.Kurzname, m.LandId })
                .ToListAsync(ct))
            .Select(m => (m.Kurzname, m.LandId))
            .ToHashSet();

        var neue = sollMannschaften
            .Where(m => !vorhandeneSchluessel.Contains((m.Kurzname, m.LandId)))
            .ToList();

        if (neue.Count > 0)
        {
            _context.Mannschaften.AddRange(neue);
            await _context.SaveChangesAsync(ct);
            _logger.LogInformation("{Anzahl} Mannschaften nachgerüstet.", neue.Count);
        }
    }

    private async Task RegelwerkeNachruesten(CancellationToken ct)
    {
        var vorhandeneNamen = await _context.Regelwerke
            .Select(r => r.Name)
            .ToHashSetAsync(ct);

        var setLookup = await _context.RankingRegelSets
            .ToDictionaryAsync(s => s.Name, s => s.Id, ct);

        var alleDefinitionen = RegelwerkeSeedDaten.RegelwerkeErstellen(setLookup);
        int hinzugefuegt = 0;

        foreach (var (regelwerk, sieg, unentschieden, niederlage, verfahren) in alleDefinitionen)
        {
            if (vorhandeneNamen.Contains(regelwerk.Name)) continue;

            regelwerk.PunkteRegeln.Add(new Domain.Entitaeten.Regelwerke.PunkteRegel
            {
                PunkteFuerSieg          = sieg,
                PunkteFuerUnentschieden = unentschieden,
                PunkteFuerNiederlage    = niederlage
            });
            for (int i = 0; i < verfahren.Length; i++)
            {
                regelwerk.Entscheidungsverfahren.Add(new Domain.Entitaeten.Regelwerke.EntscheidungsVerfahren
                {
                    Reihenfolge  = i + 1,
                    VerfahrenArt = verfahren[i]
                });
            }
            _context.Regelwerke.Add(regelwerk);
            hinzugefuegt++;
        }

        if (hinzugefuegt > 0)
        {
            await _context.SaveChangesAsync(ct);
            _logger.LogInformation("{Anzahl} Regelwerke nachgerüstet.", hinzugefuegt);
        }
    }

    /// <summary>Issue #294: seit der Vorlage/Instanz-Vereinheitlichung sind Vorlagen ganz normale
    /// <see cref="Domain.Entitaeten.Turniere.TurnierInstanz"/>-Zeilen mit
    /// <see cref="Domain.Enums.TurnierStatus.Vorlage"/> — Name **und** Status bilden die Identität, damit
    /// ein echtes Turnier mit zufällig gleichem Namen nicht fälschlich als "bereits vorhanden" zählt.
    /// Ersetzt die vormals drei getrennten Methoden (Vorlagen/Runden/Runden-Regelwerke), da eine neue
    /// Vorlage jetzt als vollständiger Objektgraph (inkl. Runden + Übergangsregeln) in einem Zug eingefügt wird.</summary>
    private async Task TurnierVorlagenNachruesten(CancellationToken ct)
    {
        var vorhandeneNamen = await _context.TurnierInstanzen
            .Where(i => i.Status == Domain.Enums.TurnierStatus.Vorlage)
            .Select(v => v.Name)
            .ToHashSetAsync(ct);

        var regelwerkLookup = await _context.Regelwerke
            .ToDictionaryAsync(r => r.Name, r => r.Id, ct);
        var verbandLookup = await _context.Verbaende
            .ToDictionaryAsync(v => v.Kurzname, v => v.Id, ct);
        var landLookup = await _context.Laender
            .ToDictionaryAsync(l => l.FifaCode, l => l.Id, ct);

        var alleVorlagen = TurnierVorlagenSeedDaten.Erstellen(regelwerkLookup, verbandLookup, landLookup);
        int hinzugefuegt = 0;

        foreach (var vorlage in alleVorlagen)
        {
            if (vorhandeneNamen.Contains(vorlage.Name)) continue;
            _context.TurnierInstanzen.Add(vorlage);
            hinzugefuegt++;
        }

        if (hinzugefuegt > 0)
        {
            await _context.SaveChangesAsync(ct);
            _logger.LogInformation("{Anzahl} neue Turniervorlagen nachgerüstet.", hinzugefuegt);
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
        var sollFilter = TurnierVorlagenSeedDaten.FilterNachVorlagenName(verbandLookup, landLookup);

        var vorhandeneVorlagen = await _context.TurnierInstanzen
            .Where(i => i.Status == Domain.Enums.TurnierStatus.Vorlage)
            .ToListAsync(ct);

        int aktualisiert = 0;
        foreach (var vorlage in vorhandeneVorlagen)
        {
            if (!sollFilter.TryGetValue(vorlage.Name, out var soll)) continue;
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
            _logger.LogInformation("{Anzahl} Turniervorlagen um Verband-/Land-/Mannschaftsartfilter ergänzt.", aktualisiert);
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
            .ToDictionary(v => v.Name);

        var vorhandeneVorlagen = await _context.TurnierInstanzen
            .Include(i => i.Runden)
            .Where(i => i.Status == Domain.Enums.TurnierStatus.Vorlage)
            .ToListAsync(ct);

        int aktualisiert = 0;
        foreach (var vorlage in vorhandeneVorlagen)
        {
            if (!sollVorlagen.TryGetValue(vorlage.Name, out var soll)) continue;
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
            _logger.LogInformation("{Anzahl} Vorlagen-Runden von SchweizerSystem auf TopfLiga korrigiert.", aktualisiert);
        }
    }

    // Seed-Stand der WM-24-Vorlage vor #350 (damals als "1982–1994" geführt).
    private const string ALTE_BESCHREIBUNG_WM24 = "6 Dreiergruppen + Achtelfinale-Runde → K.-o.";

    /// <summary>Issue #350: benennt Bestands-Vorlagen um, deren Seed-Name sich geändert hat — muss vor
    /// <see cref="TurnierVorlagenNachruesten"/> laufen, sonst entstünde unter dem neuen Namen ein Duplikat. Nur,
    /// solange unter dem neuen Namen noch keine Vorlage existiert. Beschreibung/Enddatum werden nur mitgezogen,
    /// wenn sie noch dem alten Seed-Stand entsprechen (keine manuelle Anpassung überschreiben).</summary>
    private async Task TurnierVorlagenUmbenennen(CancellationToken ct)
    {
        var vorlagen = await _context.TurnierInstanzen
            .Where(i => i.Status == Domain.Enums.TurnierStatus.Vorlage)
            .ToListAsync(ct);
        var sollNachName = TurnierVorlagenSeedDaten.Erstellen().ToDictionary(v => v.Name);

        int umbenannt = 0;
        foreach (var (alterName, neuerName) in TurnierVorlagenSeedDaten.UmbenennungenNachAltemNamen)
        {
            var alt = vorlagen.FirstOrDefault(v => v.Name == alterName);
            if (alt is null || vorlagen.Any(v => v.Name == neuerName)) continue;

            var soll = sollNachName[neuerName];
            alt.Name = neuerName;
            if (alt.Beschreibung == ALTE_BESCHREIBUNG_WM24) alt.Beschreibung = soll.Beschreibung;
            if (alt.Enddatum == new DateOnly(1994, 12, 31)) alt.Enddatum = soll.Enddatum;
            umbenannt++;
        }

        if (umbenannt > 0)
        {
            await _context.SaveChangesAsync(ct);
            _logger.LogInformation("{Anzahl} Turniervorlagen umbenannt.", umbenannt);
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
        var sollVorlagen = TurnierVorlagenSeedDaten.Erstellen().ToDictionary(v => v.Name);

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
            if (!sollVorlagen.TryGetValue(vorlage.Name, out var soll)) continue;

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
            _logger.LogInformation("{Anzahl} Struktur-Ergänzungen an Turniervorlagen nachgerüstet.", aktualisiert);
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
            .ToDictionary(v => v.Name);

        var vorhandeneVorlagen = await _context.TurnierInstanzen
            .Include(i => i.Runden)
            .Include(i => i.Auslosungen)
            .Where(i => i.Status == Domain.Enums.TurnierStatus.Vorlage)
            .ToListAsync(ct);

        int aktualisiert = 0;
        foreach (var vorlage in vorhandeneVorlagen)
        {
            if (!sollVorlagen.TryGetValue(vorlage.Name, out var soll)) continue;

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
            _logger.LogInformation("{Anzahl} Turniervorlagen um festen K.-o.-Baum ergänzt.", aktualisiert);
        }
    }

    private async Task RanglistenQuellenNachruesten(CancellationToken ct)
    {
        var vorhandeneNamen = await _context.RanglistenQuellen
            .Select(q => q.Name)
            .ToHashSetAsync(ct);

        var neue = RanglistenQuellenSeedDaten.Erstellen()
            .Where(q => !vorhandeneNamen.Contains(q.Name))
            .ToList();

        if (neue.Count > 0)
        {
            _context.RanglistenQuellen.AddRange(neue);
            await _context.SaveChangesAsync(ct);
            _logger.LogInformation("{Anzahl} Ranglisten-Quellen nachgerüstet.", neue.Count);
        }
    }

    private async Task ErstSeedAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Seed-Daten werden angelegt...");

        // 1. Verbände
        var verbaende = VerbaendeSeedDaten.Erstellen();
        _context.Verbaende.AddRange(verbaende);
        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("{Anzahl} Verbände angelegt.", verbaende.Count);

        // 2. Länder (mit VerbandId-Lookup nach SaveChanges, da IDs erst dann gesetzt sind)
        var verbaendeLookup = verbaende.ToDictionary(v => v.Kurzname, v => v.Id);
        var laender = LaenderSeedDaten.Erstellen(verbaendeLookup);
        _context.Laender.AddRange(laender);
        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("{Anzahl} Länder angelegt.", laender.Count);

        // 3. Mannschaften
        var laenderLookup = laender.ToDictionary(l => l.FifaCode, l => l.Id);
        var nationalmannschaften = NationalmannschaftenSeedDaten.Erstellen(laender, laenderLookup);
        var vereinsmannschaften  = VereinsmannschaftenSeedDaten.Erstellen(laenderLookup);

        _context.Mannschaften.AddRange(nationalmannschaften);
        _context.Mannschaften.AddRange(vereinsmannschaften);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "{Nat} Nationalmannschaften und {Ver} Vereinsmannschaften angelegt.",
            nationalmannschaften.Count, vereinsmannschaften.Count);

        // 4. RankingRegeln
        var rankingRegeln = RegelwerkeSeedDaten.RankingRegelnErstellen();
        _context.RankingRegeln.AddRange(rankingRegeln);
        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("{Anzahl} RankingRegeln angelegt.", rankingRegeln.Count);

        // 5. RankingRegelSets + Eintraege
        var (sets, eintraegeDefinitionen) = RegelwerkeSeedDaten.RankingRegelSetsErstellen();
        _context.RankingRegelSets.AddRange(sets);
        await _context.SaveChangesAsync(cancellationToken);

        var setLookup   = sets.ToDictionary(s => s.Name, s => s.Id);
        var regelLookup = rankingRegeln.ToDictionary(r => r.Regelart, r => r.Id);

        foreach (var (setName, reihenfolge, regelart) in eintraegeDefinitionen)
        {
            _context.RankingRegelSetEintraege.Add(new Domain.Entitaeten.Regelwerke.RankingRegelSetEintrag
            {
                RankingRegelSetId = setLookup[setName],
                RankingRegelId    = regelLookup[regelart],
                Reihenfolge       = reihenfolge
            });
        }
        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("{Anzahl} RankingRegelSets mit Einträgen angelegt.", sets.Count);

        // 6. Regelwerke (mit PunkteRegeln + EntscheidungsVerfahren)
        var regelwerkeDefinitionen = RegelwerkeSeedDaten.RegelwerkeErstellen(setLookup);
        foreach (var (regelwerk, sieg, unentschieden, niederlage, verfahren) in regelwerkeDefinitionen)
        {
            regelwerk.PunkteRegeln.Add(new Domain.Entitaeten.Regelwerke.PunkteRegel
            {
                PunkteFuerSieg          = sieg,
                PunkteFuerUnentschieden = unentschieden,
                PunkteFuerNiederlage    = niederlage
            });
            for (int i = 0; i < verfahren.Length; i++)
            {
                regelwerk.Entscheidungsverfahren.Add(new Domain.Entitaeten.Regelwerke.EntscheidungsVerfahren
                {
                    Reihenfolge  = i + 1,
                    VerfahrenArt = verfahren[i]
                });
            }
            _context.Regelwerke.Add(regelwerk);
        }
        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("{Anzahl} Regelwerke angelegt.", regelwerkeDefinitionen.Count);

        // 7. Turniervorlagen (TurnierInstanz mit Status=Vorlage, mit Regelwerk-Lookup für ihre Runden)
        var regelwerkLookup = regelwerkeDefinitionen.ToDictionary(
            t => t.Regelwerk.Name, t => t.Regelwerk.Id);
        var vorlagen = TurnierVorlagenSeedDaten.Erstellen(regelwerkLookup, verbaendeLookup, laenderLookup);
        _context.TurnierInstanzen.AddRange(vorlagen);
        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("{Anzahl} Turniervorlagen angelegt.", vorlagen.Count);

        _logger.LogInformation("Seed-Daten erfolgreich abgeschlossen.");
    }
}

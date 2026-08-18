using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Application.Validierung;
using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Fussballturnierverwaltung.Application.Auslosungen;

public record AuslosungErstellenEingabe(
    int TurnierInstanzId,
    int TurnierRundeId,
    string Name,
    Auslosungsart Auslosungsart,
    bool IstAutomatisch,
    DateOnly? Datum = null);

public class AuslosungService(
    IAuslosungRepository repository,
    IVerbandsQuoteRepository verbandsQuoteRepository,
    ITurnierRundeRepository rundeRepository,
    AuslosungsValidator validator,
    ILogger<AuslosungService> logger)
{
    public Task<IReadOnlyList<Auslosung>> AlleNachInstanzAbrufenAsync(int turnierInstanzId, CancellationToken ct = default)
        => repository.AlleNachInstanzAsync(turnierInstanzId, ct);

    public Task<Auslosung?> FindenNachIdAsync(int id, CancellationToken ct = default)
        => repository.FindenNachIdMitDetailsAsync(id, ct);

    public async Task<Auslosung> ErstellenAsync(AuslosungErstellenEingabe eingabe, CancellationToken ct = default)
    {
        var auslosung = new Auslosung
        {
            TurnierInstanzId = eingabe.TurnierInstanzId,
            TurnierRundeId   = eingabe.TurnierRundeId,
            Name             = eingabe.Name.Trim(),
            Auslosungsart    = eingabe.Auslosungsart,
            IstAutomatisch   = eingabe.IstAutomatisch,
            Datum            = eingabe.Datum,
            Status           = AuslosungsStatus.Vorbereitung
        };
        await repository.HinzufuegenAsync(auslosung, ct);
        logger.LogInformation("Auslosung '{Name}' (ID {Id}) für Runde {RundeId} angelegt.",
            auslosung.Name, auslosung.Id, auslosung.TurnierRundeId);
        return auslosung;
    }

    public async Task LostopfHinzufuegenAsync(int auslosungId, string name, CancellationToken ct = default)
    {
        var auslosung = await LadenOderFehler(auslosungId, ct);
        PruefeNichtAbgeschlossen(auslosung);

        var naechsteReihenfolge = auslosung.Lostoepfe.Any()
            ? auslosung.Lostoepfe.Max(l => l.Reihenfolge) + 1
            : 1;

        auslosung.Lostoepfe.Add(new Lostopf
        {
            AuslosungId = auslosungId,
            Name        = name.Trim(),
            Reihenfolge = naechsteReihenfolge
        });
        await repository.AktualisierenAsync(auslosung, ct);
    }

    public async Task MannschaftZuLostopfHinzufuegenAsync(
        int auslosungId, int lostopfId, int mannschaftId, int? setzPosition = null,
        CancellationToken ct = default)
    {
        var auslosung = await LadenOderFehler(auslosungId, ct);
        PruefeNichtAbgeschlossen(auslosung);

        var lostopf = auslosung.Lostoepfe.FirstOrDefault(l => l.Id == lostopfId)
            ?? throw new InvalidOperationException($"Lostopf {lostopfId} nicht gefunden.");

        if (auslosung.Lostoepfe.SelectMany(l => l.Eintraege).Any(e => e.MannschaftId == mannschaftId))
            throw new InvalidOperationException("Diese Mannschaft ist bereits in einem Lostopf vorhanden.");

        lostopf.Eintraege.Add(new LostopfEintrag
        {
            LostopfId    = lostopfId,
            MannschaftId = mannschaftId,
            SetzPosition = setzPosition
        });
        await repository.AktualisierenAsync(auslosung, ct);
    }

    public async Task PlatzhalterZuLostopfHinzufuegenAsync(
        int auslosungId, int lostopfId, string platzhalterName, CancellationToken ct = default)
    {
        var auslosung = await LadenOderFehler(auslosungId, ct);
        PruefeNichtAbgeschlossen(auslosung);

        var lostopf = auslosung.Lostoepfe.FirstOrDefault(l => l.Id == lostopfId)
            ?? throw new InvalidOperationException($"Lostopf {lostopfId} nicht gefunden.");

        var name = platzhalterName.Trim();

        if (auslosung.Lostoepfe.SelectMany(l => l.Eintraege).Any(e => e.PlatzhalterName == name))
            throw new InvalidOperationException($"Platzhalter '{name}' ist bereits in einem Lostopf vorhanden.");

        lostopf.Eintraege.Add(new LostopfEintrag
        {
            LostopfId       = lostopfId,
            PlatzhalterName = name
        });
        await repository.AktualisierenAsync(auslosung, ct);
    }

    public async Task MannschaftAusLostopfEntfernenAsync(
        int auslosungId, int lostopfId, int mannschaftId, CancellationToken ct = default)
    {
        var auslosung = await LadenOderFehler(auslosungId, ct);
        PruefeNichtAbgeschlossen(auslosung);

        var lostopf = auslosung.Lostoepfe.FirstOrDefault(l => l.Id == lostopfId)
            ?? throw new InvalidOperationException($"Lostopf {lostopfId} nicht gefunden.");

        var eintrag = lostopf.Eintraege.FirstOrDefault(e => e.MannschaftId == mannschaftId);
        if (eintrag is not null)
        {
            lostopf.Eintraege.Remove(eintrag);
            await repository.AktualisierenAsync(auslosung, ct);
        }
    }

    public async Task PlatzhalterAusLostopfEntfernenAsync(
        int auslosungId, int lostopfId, string platzhalterName, CancellationToken ct = default)
    {
        var auslosung = await LadenOderFehler(auslosungId, ct);
        PruefeNichtAbgeschlossen(auslosung);

        var lostopf = auslosung.Lostoepfe.FirstOrDefault(l => l.Id == lostopfId)
            ?? throw new InvalidOperationException($"Lostopf {lostopfId} nicht gefunden.");

        var eintrag = lostopf.Eintraege.FirstOrDefault(e => e.PlatzhalterName == platzhalterName.Trim());
        if (eintrag is not null)
        {
            lostopf.Eintraege.Remove(eintrag);
            await repository.AktualisierenAsync(auslosung, ct);
        }
    }

    public async Task EintragAusLostopfEntfernenAsync(
        int auslosungId, int lostopfId, int eintragId, CancellationToken ct = default)
    {
        var auslosung = await LadenOderFehler(auslosungId, ct);
        PruefeNichtAbgeschlossen(auslosung);

        var lostopf = auslosung.Lostoepfe.FirstOrDefault(l => l.Id == lostopfId)
            ?? throw new InvalidOperationException($"Lostopf {lostopfId} nicht gefunden.");

        var eintrag = lostopf.Eintraege.FirstOrDefault(e => e.Id == eintragId);
        if (eintrag is not null)
        {
            lostopf.Eintraege.Remove(eintrag);
            await repository.AktualisierenAsync(auslosung, ct);
        }
    }

    public async Task LostopfLeerenAsync(int auslosungId, int lostopfId, CancellationToken ct = default)
    {
        var auslosung = await LadenOderFehler(auslosungId, ct);
        PruefeNichtAbgeschlossen(auslosung);

        var lostopf = auslosung.Lostoepfe.FirstOrDefault(l => l.Id == lostopfId)
            ?? throw new InvalidOperationException($"Lostopf {lostopfId} nicht gefunden.");

        lostopf.Eintraege.Clear();
        await repository.AktualisierenAsync(auslosung, ct);
    }

    public async Task AlleToepfeZuruecksetzenAsync(int auslosungId, CancellationToken ct = default)
    {
        var auslosung = await LadenOderFehler(auslosungId, ct);
        PruefeNichtAbgeschlossen(auslosung);

        foreach (var lostopf in auslosung.Lostoepfe)
            lostopf.Eintraege.Clear();
        await repository.AktualisierenAsync(auslosung, ct);
    }

    public async Task RegelHinzufuegenOderAktivierenAsync(
        int auslosungId, AuslosungsRegelArt regelart, string beschreibung, CancellationToken ct = default)
    {
        var auslosung = await LadenOderFehler(auslosungId, ct);
        PruefeNichtAbgeschlossen(auslosung);

        var vorhanden = auslosung.Regeln.FirstOrDefault(r => r.Regelart == regelart);
        if (vorhanden is not null)
            vorhanden.IstAktiv = true;
        else
            auslosung.Regeln.Add(new AuslosungsRegel
            {
                AuslosungId  = auslosungId,
                Regelart     = regelart,
                Beschreibung = beschreibung,
                IstAktiv     = true
            });

        await repository.AktualisierenAsync(auslosung, ct);
    }

    public async Task RegelDeaktivierenAsync(int auslosungId, AuslosungsRegelArt regelart, CancellationToken ct = default)
    {
        var auslosung = await LadenOderFehler(auslosungId, ct);
        var regel = auslosung.Regeln.FirstOrDefault(r => r.Regelart == regelart);
        if (regel is not null)
        {
            regel.IstAktiv = false;
            await repository.AktualisierenAsync(auslosung, ct);
        }
    }

    public async Task<IReadOnlyList<AuslosungsErgebnis>> AusfuehrenAsync(int auslosungId, CancellationToken ct = default)
    {
        var auslosung = await LadenOderFehler(auslosungId, ct);

        if (auslosung.Auslosungsart == Auslosungsart.Manuell)
            throw new InvalidOperationException("Für manuelle Auslosungen bitte den Befehl 'ManuelleErgebnisseAbschliessenAsync' verwenden.");

        if (auslosung.Status == AuslosungsStatus.Abgeschlossen)
            throw new InvalidOperationException("Auslosung wurde bereits durchgeführt. Bitte zuerst zurücksetzen.");

        var alleEintraege = auslosung.Lostoepfe
            .SelectMany(l => l.Eintraege)
            .ToList();

        // Verbandsquoten gelten laut bestätigter Domänen-Annahme nur für die initiale Turnierrunde
        // (Zulassung/erste Auslosung), nicht kumulativ für Folgerunden mit eigenem Losvorgang (z.B. eine
        // separate K.o.-Auslosung nach der Gruppenphase) — siehe [[project_verbandsquote_scope]].
        IReadOnlyList<VerbandsQuote>? verbandsQuoten = null;
        var alleRunden = await rundeRepository.AlleNachInstanzAsync(auslosung.TurnierInstanzId, ct);
        var ersteRunde = alleRunden.OrderBy(r => r.Reihenfolge).FirstOrDefault();
        if (ersteRunde is not null && ersteRunde.Id == auslosung.TurnierRundeId)
            verbandsQuoten = await verbandsQuoteRepository.AlleNachTurnierInstanzIdAsync(auslosung.TurnierInstanzId, ct);

        ValidationException.WerfenWennUngueltig(
            validator.Validieren(auslosung, alleEintraege, verbandsQuoten));

        var ergebnisse = Ausloesen(auslosung);
        await repository.ErgebnisseSetzenUndAbschliessenAsync(auslosungId, ergebnisse, ct);

        logger.LogInformation("Auslosung '{Name}' durchgeführt: {Anzahl} Paarungen.", auslosung.Name, ergebnisse.Count);
        return ergebnisse;
    }

    public async Task LostoepfeSynchronisierenAsync(int auslosungId, int anzahl, CancellationToken ct = default)
    {
        if (anzahl < 1)
            throw new InvalidOperationException("Mindestens 1 Lostopf ist erforderlich.");

        var auslosung = await LadenOderFehler(auslosungId, ct);
        PruefeNichtAbgeschlossen(auslosung);

        var sortiert = auslosung.Lostoepfe.OrderBy(l => l.Reihenfolge).ToList();

        // fehlende Töpfe anlegen
        for (int i = sortiert.Count + 1; i <= anzahl; i++)
        {
            auslosung.Lostoepfe.Add(new Lostopf
            {
                AuslosungId = auslosungId,
                Name        = $"Topf {i}",
                Reihenfolge = i
            });
        }

        // überzählige leere Töpfe entfernen (von hinten)
        foreach (var topf in sortiert.Skip(anzahl))
        {
            if (topf.Eintraege.Any())
                throw new InvalidOperationException(
                    $"Lostopf '{topf.Name}' enthält noch Teams und kann nicht entfernt werden. " +
                    "Bitte zuerst alle Teams aus diesem Topf entfernen.");
            auslosung.Lostoepfe.Remove(topf);
        }

        await repository.AktualisierenAsync(auslosung, ct);
        logger.LogInformation("Auslosung {Id}: Lostöpfe auf {Anzahl} synchronisiert.", auslosungId, anzahl);
    }

    public async Task BearbeitenAsync(int auslosungId, string name, Auslosungsart auslosungsart, CancellationToken ct = default)
    {
        var auslosung = await repository.FindenNachIdAsync(auslosungId, ct)
            ?? throw new InvalidOperationException($"Auslosung {auslosungId} nicht gefunden.");
        if (auslosung.Status == AuslosungsStatus.Abgeschlossen)
            throw new InvalidOperationException("Eine abgeschlossene Auslosung kann nicht bearbeitet werden. Bitte zuerst zurücksetzen.");
        auslosung.Name         = name.Trim();
        auslosung.Auslosungsart = auslosungsart;
        await repository.AktualisierenAsync(auslosung, ct);
        logger.LogInformation("Auslosung {Id}: Name='{Name}', Art={Art} aktualisiert.", auslosungId, auslosung.Name, auslosungsart);
    }

    public async Task<Auslosung?> FindenAbgeschlosseneNachRundeAsync(
        int turnierInstanzId, int turnierRundeId, CancellationToken ct = default)
    {
        var alle    = await repository.AlleNachInstanzAsync(turnierInstanzId, ct);
        var treffer = alle
            .Where(a => a.TurnierRundeId == turnierRundeId && a.Status == AuslosungsStatus.Abgeschlossen)
            .MaxBy(a => a.Id);

        if (treffer is null) return null;
        return await repository.FindenNachIdMitDetailsAsync(treffer.Id, ct);
    }

    public async Task ZuruecksetzenAsync(int auslosungId, CancellationToken ct = default)
    {
        await repository.ErgebnisseLoeschenUndZuruecksetzenAsync(auslosungId, ct);
        logger.LogInformation("Auslosung {Id} zurückgesetzt.", auslosungId);
    }

    public async Task LoeschenAsync(int auslosungId, CancellationToken ct = default)
    {
        var auslosung = await repository.FindenNachIdAsync(auslosungId, ct)
            ?? throw new InvalidOperationException($"Auslosung {auslosungId} nicht gefunden.");
        await repository.LoeschenAsync(auslosung, ct);
    }

    private async Task<Auslosung> LadenOderFehler(int auslosungId, CancellationToken ct)
        => await repository.FindenNachIdMitDetailsAsync(auslosungId, ct)
           ?? throw new InvalidOperationException($"Auslosung {auslosungId} nicht gefunden.");

    private static void PruefeNichtAbgeschlossen(Auslosung auslosung)
    {
        if (auslosung.Status == AuslosungsStatus.Abgeschlossen)
            throw new InvalidOperationException("Eine abgeschlossene Auslosung kann nicht geändert werden. Bitte zuerst zurücksetzen.");
    }

    // ── Internal slot abstraction ──────────────────────────────────────────

    private record TeilnehmerSlot(int? MannschaftId, int? LandId, string? PlatzhalterName)
    {
        public static TeilnehmerSlot AusEintrag(LostopfEintrag e) =>
            e.MannschaftId.HasValue
                ? new(e.MannschaftId, e.Mannschaft?.LandId, null)
                : new(null, null, e.PlatzhalterName);
    }

    private static bool GleichesLand(TeilnehmerSlot a, TeilnehmerSlot b) =>
        a.LandId.HasValue && b.LandId.HasValue && a.LandId == b.LandId;

    private static AuslosungsErgebnis Paarung(int nr, TeilnehmerSlot heim, TeilnehmerSlot auswaerts) =>
        new()
        {
            PaarungsNummer           = nr,
            HeimMannschaftId         = heim.MannschaftId,
            AuswaertsMannschaftId    = auswaerts.MannschaftId,
            HeimPlatzhalterName      = heim.PlatzhalterName,
            AuswaertsPlatzhalterName = auswaerts.PlatzhalterName
        };

    // ── Draw algorithms ────────────────────────────────────────────────────

    private static List<AuslosungsErgebnis> Ausloesen(Auslosung auslosung)
    {
        var rundenart = auslosung.TurnierRunde?.Rundenart;

        // Gruppenphase und Liga: Zuweisung zu Gruppen (Liga = eine Gruppe)
        if (rundenart == Rundenart.Gruppenphase || rundenart == Rundenart.Liga)
        {
            return auslosung.Auslosungsart switch
            {
                Auslosungsart.NachToepfen   => GruppenphaseNachToepfenAusloesen(auslosung),
                Auslosungsart.NachSetzliste => GruppenphaseNachSetzlisteAusloesen(auslosung),
                _                           => GruppenphaseZufaelligAusloesen(auslosung)
            };
        }

        // All other round types produce match pairings
        return auslosung.Auslosungsart switch
        {
            Auslosungsart.NachToepfen or
            Auslosungsart.GruppensiegerGegenGruppenzweite => NachToepfenAusloesen(auslosung),
            Auslosungsart.NachSetzliste                   => NachSetzlisteAusloesen(auslosung),
            Auslosungsart.TopfZuordnung                   => TopfZuordnungAusloesen(auslosung),
            _                                             => ZufaelligAusloesen(auslosung)
        };
    }

    // ── Gruppenphase algorithms (output: team → group assignments) ─────────

    private static int GruppenAnzahlOderFehler(Auslosung auslosung)
    {
        if (auslosung.TurnierRunde?.Rundenart == Rundenart.Liga) return 1;
        return auslosung.TurnierRunde?.GruppenAnzahl
            ?? throw new InvalidOperationException(
                "Gruppenphase-Auslosung: Die Runde hat keine Gruppenanzahl definiert.");
    }

    private static List<AuslosungsErgebnis> GruppenphaseZufaelligAusloesen(Auslosung auslosung)
    {
        var gruppenAnzahl    = GruppenAnzahlOderFehler(auslosung);
        var keinGleichesLand = HatAktiveRegel(auslosung, AuslosungsRegelArt.KeinGleichesLand);

        var alleSlots = auslosung.Lostoepfe
            .OrderBy(l => l.Reihenfolge)
            .SelectMany(l => l.Eintraege)
            .Select(TeilnehmerSlot.AusEintrag)
            .ToList();

        return GruppenZuErgebnisse(GruppenBefuellen(alleSlots, gruppenAnzahl, keinGleichesLand));
    }

    // Each pot has exactly gruppenAnzahl teams; one team per pot goes into each group
    private static List<AuslosungsErgebnis> GruppenphaseNachToepfenAusloesen(Auslosung auslosung)
    {
        var gruppenAnzahl    = GruppenAnzahlOderFehler(auslosung);
        var keinGleichesLand = HatAktiveRegel(auslosung, AuslosungsRegelArt.KeinGleichesLand);
        var lostoepfe        = auslosung.Lostoepfe.OrderBy(l => l.Reihenfolge).ToList();

        var gruppen = Enumerable.Range(0, gruppenAnzahl)
            .Select(_ => new List<TeilnehmerSlot>())
            .ToList();

        if (gruppenAnzahl == 1)
        {
            // Liga: nur eine Gruppe — alle Teams aller Töpfe landen darin, keine Verteilung nötig.
            // Die generische Verteilung unten setzt genau ein Team pro Topf und Gruppe voraus
            // und würde hier alle bis auf ein Team je Topf verwerfen.
            foreach (var topf in lostoepfe)
                gruppen[0].AddRange(topf.Eintraege.Select(TeilnehmerSlot.AusEintrag));

            return GruppenZuErgebnisse(gruppen);
        }

        foreach (var topf in lostoepfe)
        {
            var slots     = topf.Eintraege.Select(TeilnehmerSlot.AusEintrag).ToList();
            var zuweisung = GruppenTopfZuordnen(slots, gruppen, gruppenAnzahl, keinGleichesLand);
            for (int g = 0; g < gruppenAnzahl && g < zuweisung.Count; g++)
                gruppen[g].Add(zuweisung[g]);
        }

        return GruppenZuErgebnisse(gruppen);
    }

    // Seeded teams (SetzPosition set) go one per group; rest distributed topf-wise
    private static List<AuslosungsErgebnis> GruppenphaseNachSetzlisteAusloesen(Auslosung auslosung)
    {
        var gruppenAnzahl    = GruppenAnzahlOderFehler(auslosung);
        var keinGleichesLand = HatAktiveRegel(auslosung, AuslosungsRegelArt.KeinGleichesLand);
        var lostoepfe        = auslosung.Lostoepfe.OrderBy(l => l.Reihenfolge).ToList();

        if (gruppenAnzahl == 1)
        {
            // Liga: nur eine Gruppe — die Setzliste hat keinen Verteilungseffekt,
            // alle Teams (gesetzt wie ungesetzt) landen in der einen Gruppe. Die
            // Take(gruppenAnzahl) unten würde sonst alle bis auf ein gesetztes Team verwerfen.
            var alle = lostoepfe.SelectMany(l => l.Eintraege).Select(TeilnehmerSlot.AusEintrag).ToList();
            return GruppenZuErgebnisse([alle]);
        }

        var alleEintraege = lostoepfe.SelectMany(l => l.Eintraege).ToList();

        var gesetzte = alleEintraege
            .Where(e => e.SetzPosition.HasValue)
            .OrderBy(e => e.SetzPosition)
            .Select(TeilnehmerSlot.AusEintrag)
            .Take(gruppenAnzahl)
            .ToList();

        var gruppen = Enumerable.Range(0, gruppenAnzahl)
            .Select(i => i < gesetzte.Count
                ? new List<TeilnehmerSlot> { gesetzte[i] }
                : new List<TeilnehmerSlot>())
            .ToList();

        // Ungesetzte Teams werden topfweise verteilt (ein Topf wird komplett auf die
        // Gruppen aufgeteilt, bevor der nächste Topf drankommt) — analog zu
        // GruppenphaseNachToepfenAusloesen, statt alle ungesetzten Töpfe topf-unabhängig
        // zusammenzuwerfen (siehe Issue #5-Diskussion).
        foreach (var topf in lostoepfe)
        {
            var ungesetzteImTopf = topf.Eintraege
                .Where(e => !e.SetzPosition.HasValue)
                .Select(TeilnehmerSlot.AusEintrag)
                .ToList();

            if (ungesetzteImTopf.Count == 0) continue;

            gruppen = GruppenBefuellen(ungesetzteImTopf, gruppenAnzahl, keinGleichesLand, gruppen);
        }

        return GruppenZuErgebnisse(gruppen);
    }

    // Assigns slots to groups round-robin, respecting KeinGleichesLand if active
    private static List<List<TeilnehmerSlot>> GruppenBefuellen(
        List<TeilnehmerSlot> slots, int gruppenAnzahl, bool keinGleichesLand,
        List<List<TeilnehmerSlot>>? startGruppen = null)
    {
        for (int versuch = 0; versuch < 500; versuch++)
        {
            var shuffled = slots.OrderBy(_ => Random.Shared.Next()).ToList();
            var gruppen = startGruppen?.Select(g => g.ToList()).ToList()
                ?? Enumerable.Range(0, gruppenAnzahl).Select(_ => new List<TeilnehmerSlot>()).ToList();
            bool valid = true;
            int gIdx = 0;

            foreach (var slot in shuffled)
            {
                bool placed = false;
                for (int i = 0; i < gruppenAnzahl; i++)
                {
                    int g = (gIdx + i) % gruppenAnzahl;
                    if (keinGleichesLand && gruppen[g].Any(t => GleichesLand(t, slot)))
                        continue;
                    gruppen[g].Add(slot);
                    gIdx = (g + 1) % gruppenAnzahl;
                    placed = true;
                    break;
                }
                if (!placed) { valid = false; break; }
            }

            if (valid) return gruppen;
        }

        throw new InvalidOperationException(
            "Keine gültige Gruppenauslosung gefunden. Die Einschränkungen " +
            "(z. B. 'Kein gleiches Land') können mit den gewählten Mannschaften nicht erfüllt werden.");
    }

    // Returns a shuffled order of topfSlots such that shuffled[g] can go into gruppen[g]
    private static List<TeilnehmerSlot> GruppenTopfZuordnen(
        List<TeilnehmerSlot> topfSlots, List<List<TeilnehmerSlot>> gruppen,
        int gruppenAnzahl, bool keinGleichesLand)
    {
        for (int versuch = 0; versuch < 500; versuch++)
        {
            var shuffled = topfSlots.OrderBy(_ => Random.Shared.Next()).ToList();
            bool valid = true;

            for (int g = 0; g < gruppenAnzahl && g < shuffled.Count; g++)
            {
                if (keinGleichesLand && gruppen[g].Any(t => GleichesLand(t, shuffled[g])))
                { valid = false; break; }
            }

            if (valid) return shuffled;
        }

        throw new InvalidOperationException(
            "Keine gültige Topf-Zuweisung für die Gruppenphase gefunden. " +
            "Die Einschränkungen können nicht erfüllt werden.");
    }

    private static List<AuslosungsErgebnis> GruppenZuErgebnisse(List<List<TeilnehmerSlot>> gruppen)
    {
        var ergebnisse = new List<AuslosungsErgebnis>();
        for (int g = 0; g < gruppen.Count; g++)
        {
            int gruppeNr = g + 1;
            foreach (var slot in gruppen[g])
                ergebnisse.Add(new AuslosungsErgebnis
                {
                    PaarungsNummer           = gruppeNr,
                    HeimMannschaftId         = slot.MannschaftId,
                    HeimPlatzhalterName      = slot.PlatzhalterName,
                    AuswaertsMannschaftId    = null,
                    AuswaertsPlatzhalterName = null
                });
        }
        return ergebnisse;
    }

    private static List<AuslosungsErgebnis> ZufaelligAusloesen(Auslosung auslosung)
    {
        var alleSlots = auslosung.Lostoepfe
            .OrderBy(l => l.Reihenfolge)
            .SelectMany(l => l.Eintraege)
            .Select(TeilnehmerSlot.AusEintrag)
            .ToList();

        var keinGleichesLand = HatAktiveRegel(auslosung, AuslosungsRegelArt.KeinGleichesLand);
        var heimNachLos      = HatAktiveRegel(auslosung, AuslosungsRegelArt.HeimrechtNachLos);
        var freilosMoeglich  = HatAktiveRegel(auslosung, AuslosungsRegelArt.FreilosMoeglich);

        TeilnehmerSlot? freilosSlot = null;
        var slots = alleSlots.ToList();

        if (freilosMoeglich && slots.Count % 2 == 1)
        {
            int freilosIdx = Random.Shared.Next(slots.Count);
            freilosSlot = slots[freilosIdx];
            slots.RemoveAt(freilosIdx);
        }

        var paare = PaarenMitKonstraints(slots, keinGleichesLand);
        return ErgebnisseAusListe(paare, freilosSlot, heimNachLos);
    }

    private static List<AuslosungsErgebnis> NachToepfenAusloesen(Auslosung auslosung)
    {
        var lostoepfe         = auslosung.Lostoepfe.OrderBy(l => l.Reihenfolge).ToList();
        var keinGleichesLand  = HatAktiveRegel(auslosung, AuslosungsRegelArt.KeinGleichesLand);
        var heimNachLos       = HatAktiveRegel(auslosung, AuslosungsRegelArt.HeimrechtNachLos);

        var alleErgebnisse = new List<AuslosungsErgebnis>();
        int paarungsNr = 1;

        for (int i = 0; i + 1 < lostoepfe.Count; i += 2)
        {
            var topf1 = lostoepfe[i].Eintraege.Select(TeilnehmerSlot.AusEintrag).ToList();
            var topf2 = lostoepfe[i + 1].Eintraege.Select(TeilnehmerSlot.AusEintrag).ToList();
            var paare = ZweiTopfPaaren(topf1, topf2, keinGleichesLand);

            foreach (var (slotA, slotB) in paare)
            {
                TeilnehmerSlot heim, auswaerts;
                if (heimNachLos && Random.Shared.Next(2) == 0)
                {
                    heim = slotB; auswaerts = slotA;
                }
                else
                {
                    heim = slotA; auswaerts = slotB;
                }
                alleErgebnisse.Add(Paarung(paarungsNr++, heim, auswaerts));
            }
        }

        return alleErgebnisse;
    }

    private static List<AuslosungsErgebnis> NachSetzlisteAusloesen(Auslosung auslosung)
    {
        var alleEintraege = auslosung.Lostoepfe
            .OrderBy(l => l.Reihenfolge)
            .SelectMany(l => l.Eintraege)
            .ToList();

        var gesetzte = alleEintraege
            .Where(e => e.SetzPosition.HasValue)
            .OrderBy(e => e.SetzPosition)
            .Select(TeilnehmerSlot.AusEintrag)
            .ToList();

        var ungesetzte = alleEintraege
            .Where(e => !e.SetzPosition.HasValue)
            .OrderBy(_ => Random.Shared.Next())
            .Select(TeilnehmerSlot.AusEintrag)
            .ToList();

        var ergebnisse = new List<AuslosungsErgebnis>();
        int paarungsNr = 1;

        for (int i = 0; i < Math.Min(gesetzte.Count, ungesetzte.Count); i++)
            ergebnisse.Add(Paarung(paarungsNr++, gesetzte[i], ungesetzte[i]));

        return ergebnisse;
    }

    // ── TopfZuordnung (UCL-Ligaphase) ──────────────────────────────────────

    private static List<AuslosungsErgebnis> TopfZuordnungAusloesen(Auslosung auslosung)
    {
        var keinGleichesLand = HatAktiveRegel(auslosung, AuslosungsRegelArt.KeinGleichesLand);
        var lostoepfe        = auslosung.Lostoepfe.OrderBy(l => l.Reihenfolge).ToList();

        var alleErgebnisse = new List<AuslosungsErgebnis>();
        int paarungsNr     = 1;

        for (int i = 0; i < lostoepfe.Count; i++)
        {
            var teamsA = lostoepfe[i].Eintraege.Select(TeilnehmerSlot.AusEintrag).ToList();

            foreach (var (heim, auswaerts) in InnerPotPaaren(teamsA, keinGleichesLand))
                alleErgebnisse.Add(Paarung(paarungsNr++, heim, auswaerts));

            for (int j = i + 1; j < lostoepfe.Count; j++)
            {
                var teamsB = lostoepfe[j].Eintraege.Select(TeilnehmerSlot.AusEintrag).ToList();
                foreach (var (heim, auswaerts) in CrossPotPaaren(teamsA, teamsB, keinGleichesLand))
                    alleErgebnisse.Add(Paarung(paarungsNr++, heim, auswaerts));
            }
        }

        return alleErgebnisse;
    }

    private static List<(TeilnehmerSlot Heim, TeilnehmerSlot Auswaerts)> InnerPotPaaren(
        List<TeilnehmerSlot> slots, bool keinGleichesLand)
    {
        int n = slots.Count;
        for (int versuch = 0; versuch < 300; versuch++)
        {
            var shuffled = slots.OrderBy(_ => Random.Shared.Next()).ToList();
            bool valid   = true;

            for (int i = 0; i < n; i++)
            {
                if (keinGleichesLand && GleichesLand(shuffled[i], shuffled[(i + 1) % n]))
                {
                    valid = false;
                    break;
                }
            }

            if (!valid) continue;

            var result = new List<(TeilnehmerSlot, TeilnehmerSlot)>(n);
            for (int i = 0; i < n; i++)
                result.Add((shuffled[i], shuffled[(i + 1) % n]));
            return result;
        }

        throw new InvalidOperationException(
            "Keine gültige Innerhalb-Topf-Auslosung gefunden. " +
            "Zu viele Mannschaften aus demselben Land im Topf?");
    }

    private static List<(TeilnehmerSlot Heim, TeilnehmerSlot Auswaerts)> CrossPotPaaren(
        List<TeilnehmerSlot> teamsA, List<TeilnehmerSlot> teamsB, bool keinGleichesLand)
    {
        int m = teamsA.Count;
        for (int versuch = 0; versuch < 500; versuch++)
        {
            var perm1  = teamsB.OrderBy(_ => Random.Shared.Next()).ToList();
            var perm2  = teamsB.OrderBy(_ => Random.Shared.Next()).ToList();
            bool valid = true;

            for (int i = 0; i < m; i++)
            {
                if (perm1[i] == perm2[i]) { valid = false; break; }
                if (keinGleichesLand &&
                    (GleichesLand(teamsA[i], perm1[i]) || GleichesLand(teamsA[i], perm2[i])))
                { valid = false; break; }
            }

            if (!valid) continue;

            var result = new List<(TeilnehmerSlot, TeilnehmerSlot)>(m * 2);
            for (int i = 0; i < m; i++)
            {
                result.Add((teamsA[i], perm1[i]));
                result.Add((perm2[i], teamsA[i]));
            }
            return result;
        }

        throw new InvalidOperationException(
            "Keine gültige Topf-Überkreuz-Auslosung gefunden. " +
            "Die Einschränkungen (z. B. 'Kein gleiches Land') können nicht erfüllt werden.");
    }

    // ─────────────────────────────────────────────────────────────────────

    private static List<(TeilnehmerSlot, TeilnehmerSlot)> PaarenMitKonstraints(
        List<TeilnehmerSlot> slots, bool keinGleichesLand)
    {
        for (int attempt = 0; attempt < 200; attempt++)
        {
            var shuffled = slots.OrderBy(_ => Random.Shared.Next()).ToList();
            bool valid = true;

            for (int i = 0; i + 1 < shuffled.Count; i += 2)
            {
                if (keinGleichesLand && GleichesLand(shuffled[i], shuffled[i + 1]))
                {
                    valid = false;
                    break;
                }
            }

            if (!valid) continue;

            var result = new List<(TeilnehmerSlot, TeilnehmerSlot)>();
            for (int i = 0; i + 1 < shuffled.Count; i += 2)
                result.Add((shuffled[i], shuffled[i + 1]));
            return result;
        }

        throw new InvalidOperationException(
            "Keine gültige Auslosung gefunden. Die Einschränkungen (z. B. 'Kein gleiches Land') " +
            "können mit den gewählten Mannschaften nicht erfüllt werden.");
    }

    private static List<(TeilnehmerSlot, TeilnehmerSlot)> ZweiTopfPaaren(
        List<TeilnehmerSlot> topf1, List<TeilnehmerSlot> topf2, bool keinGleichesLand)
    {
        for (int attempt = 0; attempt < 200; attempt++)
        {
            var shuffled2 = topf2.OrderBy(_ => Random.Shared.Next()).ToList();
            bool valid = !keinGleichesLand ||
                         !topf1.Where((t, i) => i < shuffled2.Count && GleichesLand(t, shuffled2[i])).Any();

            if (valid)
                return topf1.Zip(shuffled2).ToList();
        }

        throw new InvalidOperationException(
            "Keine gültige Auslosung gefunden. Die Einschränkungen (z. B. 'Kein gleiches Land') " +
            "können mit den gewählten Mannschaften nicht erfüllt werden.");
    }

    private static List<AuslosungsErgebnis> ErgebnisseAusListe(
        List<(TeilnehmerSlot SlotA, TeilnehmerSlot SlotB)> paare,
        TeilnehmerSlot? freilosSlot,
        bool heimNachLos)
    {
        var ergebnisse = new List<AuslosungsErgebnis>();
        int paarungsNr = 1;

        foreach (var (slotA, slotB) in paare)
        {
            TeilnehmerSlot heim, auswaerts;
            if (heimNachLos && Random.Shared.Next(2) == 0)
            {
                heim = slotB; auswaerts = slotA;
            }
            else
            {
                heim = slotA; auswaerts = slotB;
            }
            ergebnisse.Add(Paarung(paarungsNr++, heim, auswaerts));
        }

        if (freilosSlot is not null)
        {
            ergebnisse.Add(new AuslosungsErgebnis
            {
                PaarungsNummer      = paarungsNr,
                HeimMannschaftId    = freilosSlot.MannschaftId,
                HeimPlatzhalterName = freilosSlot.PlatzhalterName,
                IstFreilos          = true
            });
        }

        return ergebnisse;
    }

    // ── Manuelle Auslosung ─────────────────────────────────────────────────────

    public async Task ManuelleErgebnisseAbschliessenAsync(
        int auslosungId,
        IReadOnlyList<ManuellePaarungEingabe> paarungen,
        CancellationToken ct = default)
    {
        var auslosung = await LadenOderFehler(auslosungId, ct);

        if (auslosung.Auslosungsart != Auslosungsart.Manuell)
            throw new InvalidOperationException("Diese Methode ist nur für manuelle Auslosungen.");
        if (auslosung.Status == AuslosungsStatus.Abgeschlossen)
            throw new InvalidOperationException("Auslosung wurde bereits abgeschlossen. Bitte zuerst zurücksetzen.");
        if (!paarungen.Any())
            throw new InvalidOperationException("Keine Paarungen angegeben.");

        // Gruppenphase-Einträge haben nie ein Auswärtsteam (1 Team pro Zeile ist hier die Norm,
        // siehe GruppenZuErgebnisse) — das ist kein Freilos. Freilos gilt nur im Paarungsmodus
        // (KO/Liga/etc.), wenn bei ungerader Teamanzahl bewusst ein Slot ohne Gegner bleibt.
        bool istGruppenmodus = auslosung.TurnierRunde?.Rundenart == Rundenart.Gruppenphase;

        var ergebnisse = paarungen.Select((p, i) => new AuslosungsErgebnis
        {
            PaarungsNummer           = p.PaarungsNummer ?? i + 1,
            HeimMannschaftId         = p.HeimMannschaftId,
            HeimPlatzhalterName      = p.HeimPlatzhalterName,
            AuswaertsMannschaftId    = p.AuswaertsMannschaftId,
            AuswaertsPlatzhalterName = p.AuswaertsPlatzhalterName,
            IstFreilos               = !istGruppenmodus &&
                                        p.AuswaertsMannschaftId is null && p.AuswaertsPlatzhalterName is null
        }).ToList();

        await repository.ErgebnisseSetzenUndAbschliessenAsync(auslosungId, ergebnisse, ct);
        logger.LogInformation("Manuelle Auslosung '{Name}' abgeschlossen: {Anzahl} Paarungen.",
            auslosung.Name, ergebnisse.Count);
    }

    private static bool HatAktiveRegel(Auslosung auslosung, AuslosungsRegelArt art)
        => auslosung.Regeln.Any(r => r.IstAktiv && r.Regelart == art);
}

public record ManuellePaarungEingabe(
    int? HeimMannschaftId,
    string? HeimPlatzhalterName,
    int? AuswaertsMannschaftId,
    string? AuswaertsPlatzhalterName,
    int? PaarungsNummer = null);

using Fussballturnierverwaltung.Application.Validierung;
using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
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

        if (auslosung.Status == AuslosungsStatus.Abgeschlossen)
            throw new InvalidOperationException("Auslosung wurde bereits durchgeführt. Bitte zuerst zurücksetzen.");

        var alleMannschaften = auslosung.Lostoepfe
            .SelectMany(l => l.Eintraege)
            .Select(e => e.Mannschaft)
            .Where(m => m is not null)
            .ToList()!;

        ValidationException.WerfenWennUngueltig(
            validator.Validieren(auslosung, alleMannschaften));

        var ergebnisse = Ausloesen(auslosung);
        await repository.ErgebnisseSetzenUndAbschliessenAsync(auslosungId, ergebnisse, ct);

        logger.LogInformation("Auslosung '{Name}' durchgeführt: {Anzahl} Paarungen.", auslosung.Name, ergebnisse.Count);
        return ergebnisse;
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

    // ── Draw algorithms ────────────────────────────────────────────────────

    private static List<AuslosungsErgebnis> Ausloesen(Auslosung auslosung) =>
        auslosung.Auslosungsart switch
        {
            Auslosungsart.NachToepfen or
            Auslosungsart.GruppensiegerGegenGruppenzweite => NachToepfenAusloesen(auslosung),
            Auslosungsart.NachSetzliste                  => NachSetzlisteAusloesen(auslosung),
            _                                            => ZufaelligAusloesen(auslosung)
        };

    private static List<AuslosungsErgebnis> ZufaelligAusloesen(Auslosung auslosung)
    {
        var alleTeams = auslosung.Lostoepfe
            .OrderBy(l => l.Reihenfolge)
            .SelectMany(l => l.Eintraege)
            .Select(e => e.Mannschaft)
            .ToList();

        var keinGleichesLand = HatAktiveRegel(auslosung, AuslosungsRegelArt.KeinGleichesLand);
        var heimNachLos      = HatAktiveRegel(auslosung, AuslosungsRegelArt.HeimrechtNachLos);
        var freilosMoeglich  = HatAktiveRegel(auslosung, AuslosungsRegelArt.FreilosMoeglich);

        Mannschaft? freilosTeam = null;
        var teams = alleTeams.ToList();

        if (freilosMoeglich && teams.Count % 2 == 1)
        {
            int freilosIdx = Random.Shared.Next(teams.Count);
            freilosTeam = teams[freilosIdx];
            teams.RemoveAt(freilosIdx);
        }

        var paare = PaarenMitKonstraints(teams, keinGleichesLand);
        return ErgebnisseAusListe(paare, freilosTeam, heimNachLos, heimVonPot1: true);
    }

    private static List<AuslosungsErgebnis> NachToepfenAusloesen(Auslosung auslosung)
    {
        var lostoepfe        = auslosung.Lostoepfe.OrderBy(l => l.Reihenfolge).ToList();
        var keinGleichesLand = HatAktiveRegel(auslosung, AuslosungsRegelArt.KeinGleichesLand);
        var heimNachSetzliste = HatAktiveRegel(auslosung, AuslosungsRegelArt.HeimrechtNachSetzliste);
        var heimNachLos      = HatAktiveRegel(auslosung, AuslosungsRegelArt.HeimrechtNachLos);

        var alleErgebnisse = new List<AuslosungsErgebnis>();
        int paarungsNr = 1;

        for (int i = 0; i + 1 < lostoepfe.Count; i += 2)
        {
            var topf1 = lostoepfe[i].Eintraege.Select(e => e.Mannschaft).ToList();
            var topf2 = lostoepfe[i + 1].Eintraege.Select(e => e.Mannschaft).ToList();
            var paare = ZweiTopfPaaren(topf1, topf2, keinGleichesLand);

            foreach (var (teamA, teamB) in paare)
            {
                int heimId, auswaertsId;
                if (heimNachLos && Random.Shared.Next(2) == 0)
                {
                    heimId = teamB.Id; auswaertsId = teamA.Id;
                }
                else
                {
                    // Default oder HeimrechtNachSetzliste: Pot 1 = Heim
                    heimId = teamA.Id; auswaertsId = teamB.Id;
                }

                alleErgebnisse.Add(new AuslosungsErgebnis
                {
                    PaarungsNummer        = paarungsNr++,
                    HeimMannschaftId      = heimId,
                    AuswaertsMannschaftId = auswaertsId
                });
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
            .Select(e => e.Mannschaft)
            .ToList();

        var ungesetzte = alleEintraege
            .Where(e => !e.SetzPosition.HasValue)
            .OrderBy(_ => Random.Shared.Next())
            .Select(e => e.Mannschaft)
            .ToList();

        var ergebnisse = new List<AuslosungsErgebnis>();
        int paarungsNr = 1;

        for (int i = 0; i < Math.Min(gesetzte.Count, ungesetzte.Count); i++)
        {
            ergebnisse.Add(new AuslosungsErgebnis
            {
                PaarungsNummer        = paarungsNr++,
                HeimMannschaftId      = gesetzte[i].Id,
                AuswaertsMannschaftId = ungesetzte[i].Id
            });
        }

        return ergebnisse;
    }

    private static List<(Mannschaft, Mannschaft)> PaarenMitKonstraints(List<Mannschaft> teams, bool keinGleichesLand)
    {
        for (int attempt = 0; attempt < 200; attempt++)
        {
            var shuffled = teams.OrderBy(_ => Random.Shared.Next()).ToList();
            bool valid = true;

            for (int i = 0; i + 1 < shuffled.Count; i += 2)
            {
                if (keinGleichesLand && shuffled[i].LandId == shuffled[i + 1].LandId)
                {
                    valid = false;
                    break;
                }
            }

            if (!valid) continue;

            var result = new List<(Mannschaft, Mannschaft)>();
            for (int i = 0; i + 1 < shuffled.Count; i += 2)
                result.Add((shuffled[i], shuffled[i + 1]));
            return result;
        }

        throw new InvalidOperationException(
            "Keine gültige Auslosung gefunden. Die Einschränkungen (z. B. 'Kein gleiches Land') " +
            "können mit den gewählten Mannschaften nicht erfüllt werden.");
    }

    private static List<(Mannschaft, Mannschaft)> ZweiTopfPaaren(
        List<Mannschaft> topf1, List<Mannschaft> topf2, bool keinGleichesLand)
    {
        for (int attempt = 0; attempt < 200; attempt++)
        {
            var shuffled2 = topf2.OrderBy(_ => Random.Shared.Next()).ToList();
            bool valid = !keinGleichesLand ||
                         !topf1.Where((t, i) => i < shuffled2.Count && t.LandId == shuffled2[i].LandId).Any();

            if (valid)
                return topf1.Zip(shuffled2).ToList();
        }

        throw new InvalidOperationException(
            "Keine gültige Auslosung gefunden. Die Einschränkungen (z. B. 'Kein gleiches Land') " +
            "können mit den gewählten Mannschaften nicht erfüllt werden.");
    }

    private static List<AuslosungsErgebnis> ErgebnisseAusListe(
        List<(Mannschaft TeamA, Mannschaft TeamB)> paare,
        Mannschaft? freilosTeam,
        bool heimNachLos,
        bool heimVonPot1)
    {
        var ergebnisse = new List<AuslosungsErgebnis>();
        int paarungsNr = 1;

        foreach (var (teamA, teamB) in paare)
        {
            int heimId, auswaertsId;
            if (heimNachLos && Random.Shared.Next(2) == 0)
            {
                heimId = teamB.Id; auswaertsId = teamA.Id;
            }
            else
            {
                heimId = teamA.Id; auswaertsId = teamB.Id;
            }

            ergebnisse.Add(new AuslosungsErgebnis
            {
                PaarungsNummer        = paarungsNr++,
                HeimMannschaftId      = heimId,
                AuswaertsMannschaftId = auswaertsId
            });
        }

        if (freilosTeam is not null)
        {
            ergebnisse.Add(new AuslosungsErgebnis
            {
                PaarungsNummer   = paarungsNr,
                HeimMannschaftId = freilosTeam.Id,
                IstFreilos       = true
            });
        }

        return ergebnisse;
    }

    private static bool HatAktiveRegel(Auslosung auslosung, AuslosungsRegelArt art)
        => auslosung.Regeln.Any(r => r.IstAktiv && r.Regelart == art);
}

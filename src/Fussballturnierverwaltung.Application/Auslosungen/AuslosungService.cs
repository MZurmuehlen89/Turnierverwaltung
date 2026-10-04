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
    ILandesQuoteRepository landesQuoteRepository,
    ITurnierRundeRepository rundeRepository,
    AuslosungsValidator validator,
    IPlatzhalterKaskadeService platzhalterKaskadeService,
    ILogger<AuslosungService> logger)
{
    public Task<IReadOnlyList<Auslosung>> AlleNachInstanzAbrufenAsync(int turnierInstanzId, CancellationToken ct = default)
        => repository.AlleNachInstanzAsync(turnierInstanzId, ct);

    public Task<Auslosung?> FindenNachIdAsync(int id, CancellationToken ct = default)
        => repository.FindenNachIdMitDetailsAsync(id, ct);

    public async Task<Auslosung> ErstellenAsync(AuslosungErstellenEingabe eingabe, CancellationToken ct = default)
    {
        // Issue #320: eine Runde darf nie zwei Auslosung-Zeilen gleichzeitig haben — sonst kollidiert
        // z.B. der Vorbelegung-Tab beim ToDictionary(a => a.TurnierRundeId)-Aufbau mit einer
        // ArgumentException. Betrifft insbesondere den Fall, dass eine Runde zuvor per "Paarung fest
        // vorgegeben" eine Manuell-Auslosung erhalten hat und das Flag danach wieder deaktiviert wurde.
        var bestehendeDerInstanz = await repository.AlleNachInstanzAsync(eingabe.TurnierInstanzId, ct);
        if (bestehendeDerInstanz.Any(a => a.TurnierRundeId == eingabe.TurnierRundeId))
            throw new InvalidOperationException(
                "Für diese Runde existiert bereits eine Auslosung. Bitte zuerst die bestehende Auslosung " +
                "löschen (oder im Vorbelegung-Tab zurücksetzen), bevor eine neue angelegt wird.");

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

    /// <summary>Führt die Auslosung durch und ersetzt den Spielplan der Runde durch den daraus generierten —
    /// vorhandene Spiele samt Ergebnissen gehen dabei verloren, eine Rückfrage dazu ist Sache des Aufrufers.
    /// Scheitert die Generierung, bleibt die Auslosung abgeschlossen und die Ausnahme geht an den Aufrufer.</summary>
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
        IReadOnlyList<LandesQuote>? landesQuoten = null;
        var alleRunden = await rundeRepository.AlleNachInstanzAsync(auslosung.TurnierInstanzId, ct);
        var ersteRunde = alleRunden.OrderBy(r => r.Reihenfolge).FirstOrDefault();
        if (ersteRunde is not null && ersteRunde.Id == auslosung.TurnierRundeId)
        {
            verbandsQuoten = await verbandsQuoteRepository.AlleNachTurnierInstanzIdAsync(auslosung.TurnierInstanzId, ct);
            landesQuoten = await landesQuoteRepository.AlleNachTurnierInstanzIdAsync(auslosung.TurnierInstanzId, ct);
        }

        ValidationException.WerfenWennUngueltig(
            validator.Validieren(auslosung, alleEintraege, verbandsQuoten, landesQuoten));

        var (regeln, gruppeJeMannschaft) = await RegelnErmittelnAsync(auslosung, alleRunden, ct);
        var ergebnisse = Ausloesen(auslosung, regeln, gruppeJeMannschaft);
        await repository.ErgebnisseSetzenUndAbschliessenAsync(auslosungId, ergebnisse, ct);

        logger.LogInformation("Auslosung '{Name}' durchgeführt: {Anzahl} Paarungen.", auslosung.Name, ergebnisse.Count);

        // Wer auslost, will den Spielplan dazu (#19) — in einem Zug mit der Kaskade für die Folgerunden, die aus
        // einer früheren Durchführung aufgelöste Platzhalter enthalten können (Redo-Fall, #306). Getrennt
        // entstand der Spielplan einer Platzhalter-Runde zweimal: einmal durch die Kaskade hier, einmal durch
        // den Aufrufer (#414, wie #411 bei der manuellen Auslosung).
        await platzhalterKaskadeService.RundenSpielplanAusAuslosungErsetzenAsync(auslosung.TurnierRundeId, ergebnisse, ct);

        return ergebnisse;
    }

    // Die wirksamen Regeln (#331) und — nur wenn „Keine gleiche Gruppe“ wirkt und echte Mannschaften in den Töpfen
    // liegen — deren Gruppe aus der letzten vorangegangenen Gruppenphase. Platzhalter („1. Gruppe A“) tragen ihre
    // Gruppe selbst im Namen.
    private async Task<(RegelKontext Regeln, IReadOnlyDictionary<int, string> GruppeJeMannschaft)> RegelnErmittelnAsync(
        Auslosung auslosung, IReadOnlyList<TurnierRunde> alleRunden, CancellationToken ct)
    {
        var art       = auslosung.Auslosungsart;
        var rundenart = auslosung.TurnierRunde?.Rundenart;

        // folgtAufGruppenphase: true — ohne vorangegangene Gruppenphase fehlt ohnehin jede Gruppen-Herkunft,
        // „Keine gleiche Gruppe“ läuft dann ins Leere statt die Auslosung zu stören.
        bool Wirkt(AuslosungsRegelArt regel) => AuslosungsRegelKompatibilitaet.Wirkt(
            regel, HatAktiveRegel(auslosung, regel), art, rundenart, folgtAufGruppenphase: true);

        var regeln = new RegelKontext(
            Wirkt(AuslosungsRegelArt.KeinGleichesLand),
            Wirkt(AuslosungsRegelArt.KeineGleicheGruppe),
            Wirkt(AuslosungsRegelArt.HeimrechtNachLos),
            Wirkt(AuslosungsRegelArt.HeimrechtNachSetzliste),
            Wirkt(AuslosungsRegelArt.FreilosMoeglich),
            auslosung.TurnierRunde?.MitRueckspiel ?? false);

        var gruppeJeMannschaft = new Dictionary<int, string>();
        if (!regeln.KeineGleicheGruppe ||
            !auslosung.Lostoepfe.SelectMany(l => l.Eintraege).Any(e => e.MannschaftId.HasValue))
            return (regeln, gruppeJeMannschaft);

        var aktuelleRunde = alleRunden.FirstOrDefault(r => r.Id == auslosung.TurnierRundeId);
        var gruppenphase  = aktuelleRunde is null ? null : alleRunden
            .Where(r => r.Rundenart == Rundenart.Gruppenphase && r.Reihenfolge < aktuelleRunde.Reihenfolge)
            .MaxBy(r => r.Reihenfolge);
        if (gruppenphase is null) return (regeln, gruppeJeMannschaft);

        var gruppenAuslosung = await repository.FindenAbgeschlosseneNachRundeAsync(
            auslosung.TurnierInstanzId, gruppenphase.Id, ct);
        foreach (var e in gruppenAuslosung?.Ergebnisse ?? [])
            if (e.HeimMannschaftId is { } id)
                gruppeJeMannschaft[id] = GruppenHerkunft.Buchstabe(e.PaarungsNummer);

        return (regeln, gruppeJeMannschaft);
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

    public Task<Auslosung?> FindenAbgeschlosseneNachRundeAsync(
        int turnierInstanzId, int turnierRundeId, CancellationToken ct = default)
        => repository.FindenAbgeschlosseneNachRundeAsync(turnierInstanzId, turnierRundeId, ct);

    public async Task ZuruecksetzenAsync(int auslosungId, CancellationToken ct = default)
    {
        var auslosung = await repository.FindenNachIdAsync(auslosungId, ct)
            ?? throw new InvalidOperationException($"Auslosung {auslosungId} nicht gefunden.");

        await repository.ErgebnisseLoeschenUndZuruecksetzenAsync(auslosungId, ct);

        // Issue #320: ein aus dieser Auslosung bereits generierter Spielplan wird beim Zurücksetzen
        // konsistent mitgelöscht — sonst bleibt er beim erneuten Abschließen/Ausführen als Karteileiche
        // stehen und summiert sich mit dem neu generierten Spielplan zu doppelten Partien.
        await platzhalterKaskadeService.RundenSpielplanOhneKaskadeLoeschenAsync(auslosung.TurnierRundeId, ct);
        logger.LogInformation("Auslosung {Id} zurückgesetzt.", auslosungId);

        // Folgerunden können bereits aufgelöste Platzhalter enthalten, die auf dieser jetzt
        // zurückgesetzten Auslosung basieren — ohne die Kaskade blieben sie fälschlich stehen (#306).
        await platzhalterKaskadeService.KaskadeAusloesenAsync(auslosung.TurnierInstanzId, ct);
    }

    public async Task LoeschenAsync(int auslosungId, CancellationToken ct = default)
    {
        var auslosung = await repository.FindenNachIdAsync(auslosungId, ct)
            ?? throw new InvalidOperationException($"Auslosung {auslosungId} nicht gefunden.");
        await repository.LoeschenAsync(auslosung, ct);

        // Issue #320: wie in ZuruecksetzenAsync — ein aus dieser Auslosung bereits generierter
        // Spielplan wird mitgelöscht, sonst bleibt er als Karteileiche stehen und summiert sich bei
        // einer neu erstellten Auslosung derselben Runde zu doppelten Partien.
        await platzhalterKaskadeService.RundenSpielplanOhneKaskadeLoeschenAsync(auslosung.TurnierRundeId, ct);
        logger.LogInformation("Auslosung {Id} gelöscht.", auslosungId);

        // Folgerunden können bereits aufgelöste Platzhalter enthalten, die auf dieser jetzt
        // gelöschten Auslosung basieren — ohne die Kaskade blieben sie fälschlich stehen (#306).
        await platzhalterKaskadeService.KaskadeAusloesenAsync(auslosung.TurnierInstanzId, ct);
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

    // TopfRang: 0 = erster Topf. Gruppe/Gruppenplatz: Herkunft aus einer vorangegangenen Gruppenphase — aus dem
    // Platzhalternamen („1. Gruppe A“) oder bei echten Mannschaften aus deren Gruppen-Auslosung (#331).
    private record TeilnehmerSlot(
        int? MannschaftId, int? LandId, string? PlatzhalterName,
        int TopfRang = 0, int? SetzPosition = null, string? Gruppe = null, int? Gruppenplatz = null)
    {
        public static TeilnehmerSlot AusEintrag(
            LostopfEintrag e, int topfRang, IReadOnlyDictionary<int, string> gruppeJeMannschaft)
        {
            if (e.MannschaftId is { } id)
                return new(id, e.Mannschaft?.LandId, null, topfRang, e.SetzPosition,
                    gruppeJeMannschaft.GetValueOrDefault(id));

            var herkunft = GruppenHerkunft.AusPlatzhalter(e.PlatzhalterName);
            return new(null, null, e.PlatzhalterName, topfRang, e.SetzPosition, herkunft?.Gruppe, herkunft?.Platz);
        }
    }

    /// <summary>Die bei dieser Auslosung tatsächlich wirksamen Regeln (siehe <see cref="AuslosungsRegelKompatibilitaet"/>).</summary>
    private sealed record RegelKontext(
        bool KeinGleichesLand, bool KeineGleicheGruppe, bool HeimrechtNachLos, bool HeimrechtNachSetzliste,
        bool FreilosMoeglich, bool MitRueckspiel)
    {
        public bool Verbietet(TeilnehmerSlot a, TeilnehmerSlot b) =>
            (KeinGleichesLand && a.LandId.HasValue && a.LandId == b.LandId) ||
            (KeineGleicheGruppe && a.Gruppe is not null && a.Gruppe == b.Gruppe);

        public string EinschraenkungenText =>
            string.Join(", ", new[]
            {
                KeinGleichesLand ? "'Kein gleiches Land'" : null,
                KeineGleicheGruppe ? "'Keine gleiche Gruppe'" : null
            }.OfType<string>()) is { Length: > 0 } text ? text : "keine";
    }

    private static AuslosungsErgebnis Paarung(int nr, TeilnehmerSlot heim, TeilnehmerSlot auswaerts) =>
        new()
        {
            PaarungsNummer           = nr,
            HeimMannschaftId         = heim.MannschaftId,
            AuswaertsMannschaftId    = auswaerts.MannschaftId,
            HeimPlatzhalterName      = heim.PlatzhalterName,
            AuswaertsPlatzhalterName = auswaerts.PlatzhalterName
        };

    // Heimrecht nach Setzliste (#331): das besser eingestufte Team hat Heimrecht — bei Hin- und Rückspiel im
    // Rückspiel (UEFA-Praxis, z. B. Gruppensieger im Nations-League-Viertelfinale). Das Rückspiel entsteht
    // gespiegelt aus dem Hinspiel, der Bessere steht hier also auswärts.
    private static (TeilnehmerSlot Heim, TeilnehmerSlot Auswaerts) HeimrechtFestlegen(
        TeilnehmerSlot a, TeilnehmerSlot b, RegelKontext regeln)
    {
        if (regeln.HeimrechtNachSetzliste)
        {
            int vergleich = Rangfolge(a, b);
            if (vergleich != 0)
            {
                var (besser, schlechter) = vergleich < 0 ? (a, b) : (b, a);
                return regeln.MitRueckspiel ? (schlechter, besser) : (besser, schlechter);
            }
        }

        if (regeln.HeimrechtNachLos && Random.Shared.Next(2) == 0)
            return (b, a);
        return (a, b);
    }

    // Kleiner = besser: höherer Topf, dann bessere Setzposition, dann besserer Gruppenplatz.
    private static int Rangfolge(TeilnehmerSlot a, TeilnehmerSlot b)
    {
        int vergleich = a.TopfRang.CompareTo(b.TopfRang);
        if (vergleich != 0) return vergleich;
        vergleich = (a.SetzPosition ?? int.MaxValue).CompareTo(b.SetzPosition ?? int.MaxValue);
        if (vergleich != 0) return vergleich;
        return (a.Gruppenplatz ?? int.MaxValue).CompareTo(b.Gruppenplatz ?? int.MaxValue);
    }

    // ── Draw algorithms ────────────────────────────────────────────────────

    private static List<AuslosungsErgebnis> Ausloesen(
        Auslosung auslosung, RegelKontext regeln, IReadOnlyDictionary<int, string> gruppeJeMannschaft)
    {
        var rundenart = auslosung.TurnierRunde?.Rundenart;
        var toepfe = auslosung.Lostoepfe
            .OrderBy(l => l.Reihenfolge)
            .Select((l, rang) => l.Eintraege.Select(e => TeilnehmerSlot.AusEintrag(e, rang, gruppeJeMannschaft)).ToList())
            .ToList();

        // Gruppenphase und Liga: Zuweisung zu Gruppen (Liga = eine Gruppe)
        if (rundenart == Rundenart.Gruppenphase || rundenart == Rundenart.Liga)
        {
            int gruppenAnzahl = GruppenAnzahlOderFehler(auslosung);
            return auslosung.Auslosungsart switch
            {
                Auslosungsart.NachToepfen   => GruppenphaseNachToepfenAusloesen(toepfe, gruppenAnzahl, regeln),
                Auslosungsart.NachSetzliste => GruppenphaseNachSetzlisteAusloesen(toepfe, gruppenAnzahl, regeln),
                _                           => GruppenZuErgebnisse(GruppenBefuellen(toepfe.SelectMany(t => t).ToList(), gruppenAnzahl, regeln))
            };
        }

        // All other round types produce match pairings
        return auslosung.Auslosungsart switch
        {
            Auslosungsart.NachToepfen or
            Auslosungsart.GruppensiegerGegenGruppenzweite => NachToepfenAusloesen(toepfe, regeln),
            Auslosungsart.NachSetzliste                   => NachSetzlisteAusloesen(toepfe, regeln),
            Auslosungsart.TopfZuordnung                   => TopfZuordnungAusloesen(toepfe, regeln),
            _                                             => ZufaelligAusloesen(toepfe, regeln)
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

    // Each pot has exactly gruppenAnzahl teams; one team per pot goes into each group
    private static List<AuslosungsErgebnis> GruppenphaseNachToepfenAusloesen(
        List<List<TeilnehmerSlot>> toepfe, int gruppenAnzahl, RegelKontext regeln)
    {
        var gruppen = Enumerable.Range(0, gruppenAnzahl)
            .Select(_ => new List<TeilnehmerSlot>())
            .ToList();

        if (gruppenAnzahl == 1)
        {
            // Liga: nur eine Gruppe — alle Teams aller Töpfe landen darin, keine Verteilung nötig.
            // Die generische Verteilung unten setzt genau ein Team pro Topf und Gruppe voraus
            // und würde hier alle bis auf ein Team je Topf verwerfen.
            foreach (var topf in toepfe)
                gruppen[0].AddRange(topf);

            return GruppenZuErgebnisse(gruppen);
        }

        foreach (var topf in toepfe)
        {
            var zuweisung = GruppenTopfZuordnen(topf, gruppen, gruppenAnzahl, regeln);
            for (int g = 0; g < gruppenAnzahl && g < zuweisung.Count; g++)
                gruppen[g].Add(zuweisung[g]);
        }

        return GruppenZuErgebnisse(gruppen);
    }

    // Seeded teams (SetzPosition set) go one per group; rest distributed topf-wise
    private static List<AuslosungsErgebnis> GruppenphaseNachSetzlisteAusloesen(
        List<List<TeilnehmerSlot>> toepfe, int gruppenAnzahl, RegelKontext regeln)
    {
        if (gruppenAnzahl == 1)
        {
            // Liga: nur eine Gruppe — die Setzliste hat keinen Verteilungseffekt,
            // alle Teams (gesetzt wie ungesetzt) landen in der einen Gruppe. Die
            // Take(gruppenAnzahl) unten würde sonst alle bis auf ein gesetztes Team verwerfen.
            return GruppenZuErgebnisse([toepfe.SelectMany(t => t).ToList()]);
        }

        var gesetzte = toepfe
            .SelectMany(t => t)
            .Where(s => s.SetzPosition.HasValue)
            .OrderBy(s => s.SetzPosition)
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
        foreach (var topf in toepfe)
        {
            var ungesetzteImTopf = topf.Where(s => !s.SetzPosition.HasValue).ToList();
            if (ungesetzteImTopf.Count == 0) continue;

            gruppen = GruppenBefuellen(ungesetzteImTopf, gruppenAnzahl, regeln, gruppen);
        }

        return GruppenZuErgebnisse(gruppen);
    }

    // Assigns slots to groups round-robin, respecting the active constraints
    private static List<List<TeilnehmerSlot>> GruppenBefuellen(
        List<TeilnehmerSlot> slots, int gruppenAnzahl, RegelKontext regeln,
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
                    if (gruppen[g].Any(t => regeln.Verbietet(t, slot)))
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
            $"Keine gültige Gruppenauslosung gefunden. Die Einschränkungen ({regeln.EinschraenkungenText}) " +
            "können mit den gewählten Mannschaften nicht erfüllt werden.");
    }

    // Returns a shuffled order of topfSlots such that shuffled[g] can go into gruppen[g]
    private static List<TeilnehmerSlot> GruppenTopfZuordnen(
        List<TeilnehmerSlot> topfSlots, List<List<TeilnehmerSlot>> gruppen,
        int gruppenAnzahl, RegelKontext regeln)
    {
        for (int versuch = 0; versuch < 500; versuch++)
        {
            var shuffled = topfSlots.OrderBy(_ => Random.Shared.Next()).ToList();
            bool valid = true;

            for (int g = 0; g < gruppenAnzahl && g < shuffled.Count; g++)
            {
                if (gruppen[g].Any(t => regeln.Verbietet(t, shuffled[g])))
                { valid = false; break; }
            }

            if (valid) return shuffled;
        }

        throw new InvalidOperationException(
            "Keine gültige Topf-Zuweisung für die Gruppenphase gefunden. " +
            $"Die Einschränkungen ({regeln.EinschraenkungenText}) können nicht erfüllt werden.");
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

    private static List<AuslosungsErgebnis> ZufaelligAusloesen(List<List<TeilnehmerSlot>> toepfe, RegelKontext regeln)
    {
        var slots = toepfe.SelectMany(t => t).ToList();
        TeilnehmerSlot? freilosSlot = null;

        if (regeln.FreilosMoeglich && slots.Count % 2 == 1)
        {
            int freilosIdx = Random.Shared.Next(slots.Count);
            freilosSlot = slots[freilosIdx];
            slots.RemoveAt(freilosIdx);
        }

        var paare = PaarenMitKonstraints(slots, regeln);
        return ErgebnisseAusListe(paare, freilosSlot, regeln);
    }

    private static List<AuslosungsErgebnis> NachToepfenAusloesen(List<List<TeilnehmerSlot>> toepfe, RegelKontext regeln)
    {
        var alleErgebnisse = new List<AuslosungsErgebnis>();
        int paarungsNr = 1;

        for (int i = 0; i + 1 < toepfe.Count; i += 2)
        {
            foreach (var (slotA, slotB) in ZweiTopfPaaren(toepfe[i], toepfe[i + 1], regeln))
            {
                var (heim, auswaerts) = HeimrechtFestlegen(slotA, slotB, regeln);
                alleErgebnisse.Add(Paarung(paarungsNr++, heim, auswaerts));
            }
        }

        return alleErgebnisse;
    }

    // Gesetzte (nach Setzposition) gegen Ungesetzte — dieselbe Zwei-Topf-Paarung wie „Nach Töpfen“, damit auch
    // hier alle Einschränkungen und Heimrecht-Regeln greifen (bis #331 ignorierte „Nach Setzliste“ sie alle).
    private static List<AuslosungsErgebnis> NachSetzlisteAusloesen(List<List<TeilnehmerSlot>> toepfe, RegelKontext regeln)
    {
        var alle = toepfe.SelectMany(t => t).ToList();
        var gesetzte   = alle.Where(s => s.SetzPosition.HasValue).OrderBy(s => s.SetzPosition).ToList();
        var ungesetzte = alle.Where(s => !s.SetzPosition.HasValue).ToList();

        return ZweiTopfPaaren(gesetzte, ungesetzte, regeln)
            .Select((p, i) =>
            {
                var (heim, auswaerts) = HeimrechtFestlegen(p.Item1, p.Item2, regeln);
                return Paarung(i + 1, heim, auswaerts);
            })
            .ToList();
    }

    // ── TopfZuordnung (UCL-Ligaphase) ──────────────────────────────────────

    // Die Länder-Regeln (kein gleiches Land, höchstens 2 Gegner aus demselben Land) sind fest an die Regel
    // "Kein gleiches Land" gekoppelt (Nutzerentscheidung Issue #347, keine eigene AuslosungsRegelArt).
    private static List<AuslosungsErgebnis> TopfZuordnungAusloesen(List<List<TeilnehmerSlot>> lostoepfe, RegelKontext regeln)
    {
        var keinGleichesLand = regeln.KeinGleichesLand;
        if (keinGleichesLand) LandverteilungPruefen(lostoepfe);

        var teilnehmer = lostoepfe.SelectMany((topf, i) => topf.Select(slot => (Slot: slot, Topf: i))).ToList();
        var paarungen  = new LigaphasenAuslosung(
                teilnehmer.Select(t => t.Topf).ToList(),
                teilnehmer.Select(t => t.Slot.LandId).ToList(),
                keinGleichesLand,
                Random.Shared)
            .Ausloesen()
            ?? throw new InvalidOperationException(
                "Keine gültige Topf-Zuordnung gefunden: Die Einschränkungen (kein gleiches Land, höchstens " +
                $"{LigaphasenAuslosung.MAX_GEGNER_JE_LAND} Gegner aus demselben Land, konfliktfreie Spieltage) " +
                "sind mit dieser Topf-Besetzung nicht erfüllbar.");

        return paarungen
            .Select((p, i) => Paarung(i + 1, teilnehmer[p.Heim].Slot, teilnehmer[p.Gast].Slot))
            .ToList();
    }

    // Innerhalb eines Topfs braucht jedes Team einen Gastgeber und einen Gast aus einem anderen Land — mehr als
    // die Hälfte eines Topfs aus einem Land ist daher nie auslosbar. Vorab prüfen, statt erst nach erfolgloser
    // Suche mit einer unspezifischen Meldung zu scheitern.
    private static void LandverteilungPruefen(List<List<TeilnehmerSlot>> lostoepfe)
    {
        for (int i = 0; i < lostoepfe.Count; i++)
        {
            var topf = lostoepfe[i];
            int groessteGruppe = topf.Where(t => t.LandId.HasValue).GroupBy(t => t.LandId)
                .Select(g => g.Count()).DefaultIfEmpty(0).Max();
            if (groessteGruppe > topf.Count / 2)
                throw new InvalidOperationException(
                    $"Topf {i + 1} enthält {groessteGruppe} von {topf.Count} Mannschaften aus demselben Land. " +
                    "Mit der Regel 'Kein gleiches Land' darf höchstens die Hälfte eines Topfs aus einem Land kommen.");
        }
    }

    // ── Paarungssuche mit Einschränkungen ─────────────────────────────────────

    // Obergrenze für die Backtracking-Schritte — eine unerfüllbare Kombination bricht so zuverlässig ab, statt
    // bei großen Töpfen exponentiell lange zu suchen.
    private const int MAX_SUCHSCHRITTE = 200_000;

    // Backtracking statt blindem Neu-Mischen (#331): findet eine Paarung garantiert, sofern eine existiert — mit
    // „Kein gleiches Land“ und „Keine gleiche Gruppe“ gleichzeitig wurde die Trefferquote zufälliger Versuche sonst
    // zu klein. Die Kandidaten werden vorab gemischt, die Auslosung bleibt zufällig.
    private static List<(TeilnehmerSlot, TeilnehmerSlot)> PaarenMitKonstraints(
        List<TeilnehmerSlot> slots, RegelKontext regeln)
    {
        var gemischt = slots.OrderBy(_ => Random.Shared.Next()).ToList();
        var vergeben = new bool[gemischt.Count];
        var paare    = new List<(TeilnehmerSlot, TeilnehmerSlot)>();
        int schritte = 0;

        bool Suchen()
        {
            // Bei ungerader Anzahl ohne Freilos bleibt ein Team übrig — wie bisher ohne Paarung.
            if (vergeben.Count(v => !v) < 2) return true;
            int erster = Array.IndexOf(vergeben, false);
            if (++schritte > MAX_SUCHSCHRITTE) return false;

            vergeben[erster] = true;
            for (int j = erster + 1; j < gemischt.Count; j++)
            {
                if (vergeben[j] || regeln.Verbietet(gemischt[erster], gemischt[j])) continue;
                vergeben[j] = true;
                paare.Add((gemischt[erster], gemischt[j]));
                if (Suchen()) return true;
                paare.RemoveAt(paare.Count - 1);
                vergeben[j] = false;
            }
            vergeben[erster] = false;
            return false;
        }

        if (Suchen()) return paare;
        throw KeineGueltigeAuslosung(regeln);
    }

    // Paart topf1[i] mit je einem anderen Team aus topf2 (bei ungleicher Größe nur die ersten Min(n1, n2)).
    private static List<(TeilnehmerSlot, TeilnehmerSlot)> ZweiTopfPaaren(
        List<TeilnehmerSlot> topf1, List<TeilnehmerSlot> topf2, RegelKontext regeln)
    {
        var kandidaten = topf2.OrderBy(_ => Random.Shared.Next()).ToList();
        int anzahl     = Math.Min(topf1.Count, kandidaten.Count);
        var zuordnung  = new int[anzahl];
        var vergeben   = new bool[kandidaten.Count];
        int schritte   = 0;

        bool Suchen(int i)
        {
            if (i == anzahl) return true;
            if (++schritte > MAX_SUCHSCHRITTE) return false;

            for (int j = 0; j < kandidaten.Count; j++)
            {
                if (vergeben[j] || regeln.Verbietet(topf1[i], kandidaten[j])) continue;
                vergeben[j] = true;
                zuordnung[i] = j;
                if (Suchen(i + 1)) return true;
                vergeben[j] = false;
            }
            return false;
        }

        if (Suchen(0))
            return Enumerable.Range(0, anzahl).Select(i => (topf1[i], kandidaten[zuordnung[i]])).ToList();
        throw KeineGueltigeAuslosung(regeln);
    }

    private static InvalidOperationException KeineGueltigeAuslosung(RegelKontext regeln) =>
        new($"Keine gültige Auslosung gefunden. Die Einschränkungen ({regeln.EinschraenkungenText}) " +
            "können mit den gewählten Mannschaften nicht erfüllt werden.");

    private static List<AuslosungsErgebnis> ErgebnisseAusListe(
        List<(TeilnehmerSlot SlotA, TeilnehmerSlot SlotB)> paare,
        TeilnehmerSlot? freilosSlot,
        RegelKontext regeln)
    {
        var ergebnisse = new List<AuslosungsErgebnis>();
        int paarungsNr = 1;

        foreach (var (slotA, slotB) in paare)
        {
            var (heim, auswaerts) = HeimrechtFestlegen(slotA, slotB, regeln);
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

    /// <summary>Schließt eine manuelle Auslosung mit den übergebenen Paarungen ab und ersetzt den Spielplan der
    /// Runde durch den daraus generierten — vorhandene Spiele samt Ergebnissen gehen dabei verloren, eine
    /// Rückfrage dazu ist Sache des Aufrufers.</summary>
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
        // siehe GruppenZuErgebnisse) — das ist kein Freilos. Freilos gilt nur im echten Paarungsmodus
        // (KO/Sonderrunde/etc.), wenn bei ungerader Teamanzahl bewusst ein Slot ohne Gegner bleibt.
        // Liga und SchweizerSystem teilen sich dieselbe "flache Teilnehmerliste ohne Paarung"-Semantik
        // wie Gruppenphase (siehe Ausloesen()-Dispatch oben sowie RundenEditorViewModel.IstLiga,
        // Issue #251-Folgefund) — ein einzelnes Team pro Zeile ist dort ebenfalls die Norm.
        bool istGruppenmodus = auslosung.TurnierRunde?.Rundenart
            is Rundenart.Gruppenphase or Rundenart.Liga or Rundenart.SchweizerSystem;

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

        // Wer die Paarungen abschließt, will den Spielplan dazu (#19) — in einem Zug mit der Kaskade für die
        // Folgerunden (#306). Getrennt entstand der Spielplan einer Platzhalter-Runde zweimal (#411).
        await platzhalterKaskadeService.RundenSpielplanAusAuslosungErsetzenAsync(auslosung.TurnierRundeId, ergebnisse, ct);
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

using Fussballturnierverwaltung.Application.Ranglisten;
using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Application.Validierung;
using Fussballturnierverwaltung.Application.Zufall;
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
    ITurnierRundeRepository rundeRepository,
    AuslosungsVorbereitung vorbereitung,
    IPlatzhalterKaskadeService platzhalterKaskadeService,
    Zufallsquelle zufallsquelle,
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

        // Von der Vorlage vorgegeben (Gruppenposition #475, Regeln #356) und von Beginn an aktiv, bleibt änderbar.
        var runde = await rundeRepository.FindenNachIdAsync(eingabe.TurnierRundeId, ct);
        var auslosung = new Auslosung
        {
            TurnierInstanzId = eingabe.TurnierInstanzId,
            TurnierRundeId   = eingabe.TurnierRundeId,
            Name             = eingabe.Name.Trim(),
            Auslosungsart    = eingabe.Auslosungsart,
            Gruppenposition  = runde?.VorgegebeneGruppenposition ?? Gruppenposition.NachTopf,
            IstAutomatisch   = eingabe.IstAutomatisch,
            Datum            = eingabe.Datum,
            Status           = AuslosungsStatus.Vorbereitung
        };

        // Ob eine vorgegebene Regel bei der gewählten Auslosungsart tatsächlich wirkt, entscheidet weiterhin
        // AuslosungsRegelKompatibilitaet.
        foreach (var regelart in runde?.VorgegebeneAuslosungsregeln.Distinct() ?? [])
            auslosung.Regeln.Add(new AuslosungsRegel
            {
                Regelart     = regelart,
                Beschreibung = AuslosungsRegelKompatibilitaet.AnzeigeName(regelart),
                IstAktiv     = true
            });

        await repository.HinzufuegenAsync(auslosung, ct);
        logger.LogInformation("Auslosung '{Name}' (ID {Id}) für Runde {RundeId} angelegt.",
            auslosung.Name, auslosung.Id, auslosung.TurnierRundeId);
        return auslosung;
    }

    public async Task MannschaftZuLostopfHinzufuegenAsync(
        int auslosungId, int lostopfId, int mannschaftId, int? setzPosition = null,
        CancellationToken ct = default)
    {
        var (auslosung, lostopf) = await BearbeitbareMitLostopfLadenAsync(auslosungId, lostopfId, ct);

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
        var (auslosung, lostopf) = await BearbeitbareMitLostopfLadenAsync(auslosungId, lostopfId, ct);

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

    public async Task EintragAusLostopfEntfernenAsync(
        int auslosungId, int lostopfId, int eintragId, CancellationToken ct = default)
    {
        var (auslosung, lostopf) = await BearbeitbareMitLostopfLadenAsync(auslosungId, lostopfId, ct);

        var eintrag = lostopf.Eintraege.FirstOrDefault(e => e.Id == eintragId);
        if (eintrag is not null)
        {
            lostopf.Eintraege.Remove(eintrag);
            await repository.AktualisierenAsync(auslosung, ct);
        }
    }

    public async Task LostopfLeerenAsync(int auslosungId, int lostopfId, CancellationToken ct = default)
    {
        var (auslosung, lostopf) = await BearbeitbareMitLostopfLadenAsync(auslosungId, lostopfId, ct);

        lostopf.Eintraege.Clear();
        await repository.AktualisierenAsync(auslosung, ct);
    }

    public async Task AlleToepfeZuruecksetzenAsync(int auslosungId, CancellationToken ct = default)
    {
        var auslosung = await BearbeitbareLadenAsync(auslosungId, ct);

        foreach (var lostopf in auslosung.Lostoepfe)
            lostopf.Eintraege.Clear();
        await vorbereitung.GruppenkoepfeEinlegenAsync(auslosung, ct);
        await repository.AktualisierenAsync(auslosung, ct);
    }

    public async Task RegelHinzufuegenOderAktivierenAsync(
        int auslosungId, AuslosungsRegelArt regelart, string beschreibung, CancellationToken ct = default)
    {
        var auslosung = await BearbeitbareLadenAsync(auslosungId, ct);

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
    /// Scheitert die Generierung, bleibt die Auslosung abgeschlossen und die Ausnahme geht an den Aufrufer.
    /// <paramref name="qualifikanten"/>: die Mannschaften hinter den Platzhaltern der Lostöpfe (#483) — mit ihnen
    /// wirken Land- und Verbandsregeln auch auf Platzhalter. Der Aufrufer hat sie für die Anzeige schon aufgelöst.</summary>
    public async Task<IReadOnlyList<AuslosungsErgebnis>> AusfuehrenAsync(
        int auslosungId, Qualifikanten? qualifikanten = null, CancellationToken ct = default)
    {
        var auslosung = await LadenOderFehler(auslosungId, ct);

        if (auslosung.Auslosungsart == Auslosungsart.Manuell)
            throw new InvalidOperationException("Für manuelle Auslosungen bitte die Ziehung von Hand verwenden (ZiehungStartenAsync).");

        var losVorbereitung = await vorbereitung.VorbereitenAsync(auslosung, ct);
        var ergebnisse      = Losverfahren.Ausloesen(auslosung, losVorbereitung, zufallsquelle.Erzeugen(), qualifikanten);
        await repository.ErgebnisseSetzenUndAbschliessenAsync(auslosungId, ergebnisse, ct);

        logger.LogInformation("Auslosung '{Name}' durchgeführt: {Anzahl} Paarungen.", auslosung.Name, ergebnisse.Count);

        // Wer auslost, will den Spielplan dazu (#19) — in einem Zug mit der Kaskade für die Folgerunden, die aus
        // einer früheren Durchführung aufgelöste Platzhalter enthalten können (Redo-Fall, #306). Getrennt
        // entstand der Spielplan einer Platzhalter-Runde zweimal: einmal durch die Kaskade hier, einmal durch
        // den Aufrufer (#414, wie #411 bei der manuellen Auslosung).
        await platzhalterKaskadeService.RundenSpielplanAusAuslosungErsetzenAsync(auslosung.TurnierRundeId, ergebnisse, ct);

        return ergebnisse;
    }

    /// <summary>Beginnt die Ziehung von Hand einer manuellen Auslosung (#482) — mit denselben Vorbereitungen und
    /// Prüfungen wie <see cref="AusfuehrenAsync"/>. Gespeichert wird erst das Ergebnis über
    /// <see cref="ManuelleErgebnisseAbschliessenAsync"/>.</summary>
    public async Task<ManuelleZiehung> ZiehungStartenAsync(
        int auslosungId, Qualifikanten? qualifikanten = null, CancellationToken ct = default)
    {
        var auslosung = await LadenOderFehler(auslosungId, ct);
        if (auslosung.Auslosungsart != Auslosungsart.Manuell)
            throw new InvalidOperationException("Die Ziehung von Hand gibt es nur bei der Auslosungsart „Manuell“.");

        var losVorbereitung = await vorbereitung.VorbereitenAsync(auslosung, ct);
        return ManuelleZiehung.Starten(auslosung, losVorbereitung, qualifikanten ?? Qualifikanten.Keine);
    }

    public async Task LostoepfeSynchronisierenAsync(int auslosungId, int anzahl, CancellationToken ct = default)
    {
        if (anzahl < 1)
            throw new InvalidOperationException("Mindestens 1 Lostopf ist erforderlich.");

        var auslosung = await BearbeitbareLadenAsync(auslosungId, ct);

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

        await vorbereitung.GruppenkoepfeEinlegenAsync(auslosung, ct);
        await repository.AktualisierenAsync(auslosung, ct);
        logger.LogInformation("Auslosung {Id}: Lostöpfe auf {Anzahl} synchronisiert.", auslosungId, anzahl);
    }

    public async Task BearbeitenAsync(
        int auslosungId, string name, Auslosungsart auslosungsart, Gruppenposition gruppenposition,
        CancellationToken ct = default)
    {
        var auslosung = await OhneDetailsLadenOderFehler(auslosungId, ct);
        if (auslosung.Status == AuslosungsStatus.Abgeschlossen)
            throw new InvalidOperationException("Eine abgeschlossene Auslosung kann nicht bearbeitet werden. Bitte zuerst zurücksetzen.");
        auslosung.Name            = name.Trim();
        auslosung.Auslosungsart   = auslosungsart;
        auslosung.Gruppenposition = gruppenposition;
        await repository.AktualisierenAsync(auslosung, ct);
        logger.LogInformation("Auslosung {Id}: Name='{Name}', Art={Art}, Gruppenposition={Position} aktualisiert.",
            auslosungId, auslosung.Name, auslosungsart, gruppenposition);
    }

    public Task<Auslosung?> FindenAbgeschlosseneNachRundeAsync(
        int turnierInstanzId, int turnierRundeId, CancellationToken ct = default)
        => repository.FindenAbgeschlosseneNachRundeAsync(turnierInstanzId, turnierRundeId, ct);

    public async Task ZuruecksetzenAsync(int auslosungId, CancellationToken ct = default)
    {
        var auslosung = await OhneDetailsLadenOderFehler(auslosungId, ct);

        await repository.ErgebnisseLoeschenUndZuruecksetzenAsync(auslosungId, ct);
        logger.LogInformation("Auslosung {Id} zurückgesetzt.", auslosungId);

        await SpielplanUndFolgerundenBereinigenAsync(auslosung, ct);
    }

    public async Task LoeschenAsync(int auslosungId, CancellationToken ct = default)
    {
        var auslosung = await OhneDetailsLadenOderFehler(auslosungId, ct);
        await repository.LoeschenAsync(auslosung, ct);
        logger.LogInformation("Auslosung {Id} gelöscht.", auslosungId);

        await SpielplanUndFolgerundenBereinigenAsync(auslosung, ct);
    }

    // Nach dem Zurücksetzen oder Löschen einer Auslosung: Ein aus ihr bereits generierter Spielplan wird
    // mitgelöscht — sonst bleibt er als Karteileiche stehen und summiert sich mit dem nächsten generierten
    // Spielplan der Runde zu doppelten Partien (#320). Folgerunden können bereits aufgelöste Platzhalter
    // enthalten, die auf dieser Auslosung basieren — ohne die Kaskade blieben sie fälschlich stehen (#306).
    private async Task SpielplanUndFolgerundenBereinigenAsync(Auslosung auslosung, CancellationToken ct)
    {
        await platzhalterKaskadeService.RundenSpielplanOhneKaskadeLoeschenAsync(auslosung.TurnierRundeId, ct);
        await platzhalterKaskadeService.KaskadeAusloesenAsync(auslosung.TurnierInstanzId, ct);
    }

    // ── Laden ─────────────────────────────────────────────────────────────────

    private async Task<Auslosung> LadenOderFehler(int auslosungId, CancellationToken ct)
        => await repository.FindenNachIdMitDetailsAsync(auslosungId, ct)
           ?? throw new InvalidOperationException($"Auslosung {auslosungId} nicht gefunden.");

    private async Task<Auslosung> OhneDetailsLadenOderFehler(int auslosungId, CancellationToken ct)
        => await repository.FindenNachIdAsync(auslosungId, ct)
           ?? throw new InvalidOperationException($"Auslosung {auslosungId} nicht gefunden.");

    private async Task<Auslosung> BearbeitbareLadenAsync(int auslosungId, CancellationToken ct)
    {
        var auslosung = await LadenOderFehler(auslosungId, ct);
        if (auslosung.Status == AuslosungsStatus.Abgeschlossen)
            throw new InvalidOperationException("Eine abgeschlossene Auslosung kann nicht geändert werden. Bitte zuerst zurücksetzen.");
        return auslosung;
    }

    private async Task<(Auslosung Auslosung, Lostopf Lostopf)> BearbeitbareMitLostopfLadenAsync(
        int auslosungId, int lostopfId, CancellationToken ct)
    {
        var auslosung = await BearbeitbareLadenAsync(auslosungId, ct);
        var lostopf   = auslosung.Lostoepfe.FirstOrDefault(l => l.Id == lostopfId)
            ?? throw new InvalidOperationException($"Lostopf {lostopfId} nicht gefunden.");
        return (auslosung, lostopf);
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
        // siehe Losverfahren.GruppenZuErgebnisse) — das ist kein Freilos. Freilos gilt nur im echten Paarungsmodus
        // (KO/Sonderrunde/etc.), wenn bei ungerader Teamanzahl bewusst ein Slot ohne Gegner bleibt.
        // Liga und SchweizerSystem teilen sich dieselbe "flache Teilnehmerliste ohne Paarung"-Semantik
        // wie Gruppenphase (siehe Losverfahren.Ausloesen sowie RundenEditorViewModel.IstLiga,
        // Issue #251-Folgefund) — ein einzelnes Team pro Zeile ist dort ebenfalls die Norm.
        bool istGruppenmodus = auslosung.TurnierRunde is { } runde && VorbelegungsRaster.HatGruppen(runde.Rundenart);

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
}

public record ManuellePaarungEingabe(
    int? HeimMannschaftId,
    string? HeimPlatzhalterName,
    int? AuswaertsMannschaftId,
    string? AuswaertsPlatzhalterName,
    int? PaarungsNummer = null)
{
    // Ein Teilnehmer ohne Mannschaft wird über seinen Namen als Platzhalter eingetragen.

    /// <summary>Ein besetzter Gruppenplatz: der Teilnehmer steht auf der Heimseite, die Paarungsnummer ist die
    /// Nummer seiner Gruppe.</summary>
    public static ManuellePaarungEingabe FuerGruppenplatz(int gruppenNr, int? mannschaftId, string name) =>
        new(mannschaftId, mannschaftId is null ? name : null, null, null, gruppenNr);

    /// <summary>Eine K.-o.-Paarung; ohne <paramref name="auswaerts"/> bleibt der Auswärtsplatz frei (Freilos).</summary>
    public static ManuellePaarungEingabe FuerPaarung(
        int paarungsNr, (int? MannschaftId, string Name) heim, (int? MannschaftId, string Name)? auswaerts) =>
        new(heim.MannschaftId, heim.MannschaftId is null ? heim.Name : null,
            auswaerts?.MannschaftId, auswaerts is { } gast && gast.MannschaftId is null ? gast.Name : null,
            paarungsNr);
}

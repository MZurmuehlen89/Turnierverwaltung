using Fussballturnierverwaltung.Application.Ranglisten;
using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Auslosungen;

/// <summary>Was vor jedem Losen geschieht (<see cref="AuslosungService"/>): Gruppenköpfe einlegen, Auslosung
/// prüfen, wirksame Regeln ermitteln.</summary>
public class AuslosungsVorbereitung(
    IAuslosungRepository repository,
    ITurnierRundeRepository rundeRepository,
    AuslosungsPruefung pruefung,
    IRanglisteRepository ranglisteRepository)
{
    public async Task<LosVorbereitung> VorbereitenAsync(Auslosung auslosung, CancellationToken ct)
    {
        if (auslosung.Status == AuslosungsStatus.Abgeschlossen)
            throw new InvalidOperationException("Auslosung wurde bereits durchgeführt. Bitte zuerst zurücksetzen.");

        var gruppenkoepfe = await GruppenkoepfeSicherstellenAsync(auslosung, ct);
        var alleRunden    = await rundeRepository.AlleNachInstanzAsync(auslosung.TurnierInstanzId, ct);
        await pruefung.PruefenAsync(auslosung, alleRunden, ct);

        var (regeln, gruppeJeMannschaft) = await RegelnErmittelnAsync(auslosung, alleRunden, ct);
        return new LosVorbereitung(regeln, gruppeJeMannschaft, gruppenkoepfe);
    }

    // Ein Gastgeber, der erst nach dem Anlegen der Töpfe eingetragen wurde, kommt spätestens hier hinein (#487).
    private async Task<IReadOnlyDictionary<int, int>> GruppenkoepfeSicherstellenAsync(Auslosung auslosung, CancellationToken ct)
    {
        var gruppenkoepfe = await GruppenkoepfeEinlegenAsync(auslosung, ct);
        if (gruppenkoepfe.OhnePlatz.Count > 0)
            throw new InvalidOperationException(OhnePlatzMeldung(gruppenkoepfe.OhnePlatz));
        if (gruppenkoepfe.Geaendert) await repository.AktualisierenAsync(auslosung, ct);
        return gruppenkoepfe.Koepfe;
    }

    // Die wirksamen Regeln (#331) und die Gruppe je Mannschaft aus der vorangegangenen Gruppenphase.
    private async Task<(RegelKontext Regeln, IReadOnlyDictionary<int, string> GruppeJeMannschaft)> RegelnErmittelnAsync(
        Auslosung auslosung, IReadOnlyList<TurnierRunde> alleRunden, CancellationToken ct)
    {
        bool Wirkt(AuslosungsRegelArt regel) =>
            AuslosungsRegelKompatibilitaet.Wirkt(regel, auslosung, auslosung.TurnierRunde?.Rundenart);

        var regeln = new RegelKontext(
            Wirkt(AuslosungsRegelArt.KeinGleichesLand),
            Wirkt(AuslosungsRegelArt.KeineGleicheGruppe),
            Wirkt(AuslosungsRegelArt.HeimrechtNachLos),
            Wirkt(AuslosungsRegelArt.HeimrechtNachSetzliste),
            Wirkt(AuslosungsRegelArt.FreilosMoeglich),
            Wirkt(AuslosungsRegelArt.Konfoederationsverteilung),
            auslosung.TurnierRunde?.MitRueckspiel ?? false,
            Wirkt(AuslosungsRegelArt.HeimrechtFuerUnterklassige)
                ? await SpielklassenLadenAsync(auslosung.TurnierRunde?.StandardRanglisteId, ct)
                : null,
            Wirkt(AuslosungsRegelArt.HeimrechtFuerUnterenTopf));

        var gruppeJeMannschaft = regeln.KeineGleicheGruppe
            ? await GruppeJeMannschaftLadenAsync(auslosung, alleRunden, ct)
            : new Dictionary<int, string>();
        return (regeln, gruppeJeMannschaft);
    }

    // Gruppe je Mannschaft aus der letzten vorangegangenen Gruppenphase — nur nötig, wenn echte Mannschaften oder
    // Platzhalter ohne Gruppe im Namen in den Töpfen liegen. Platzhalter wie „1. Gruppe A“ tragen ihre Gruppe selbst
    // im Namen, die besten Dritten („1. bester 3. Platz“) bekommen sie über ihren feststehenden Qualifikanten (#489).
    // Quelle ist die Gruppenauslosung: Eine Gruppenphase, die selbst aus Platzhaltern gelost wurde, kennt dort keine
    // Mannschaften, ihre Qualifikanten bleiben dann ohne Gruppe.
    private async Task<Dictionary<int, string>> GruppeJeMannschaftLadenAsync(
        Auslosung auslosung, IReadOnlyList<TurnierRunde> alleRunden, CancellationToken ct)
    {
        var gruppeJeMannschaft = new Dictionary<int, string>();
        if (!auslosung.Lostoepfe.SelectMany(l => l.Eintraege).Any(BrauchtGruppeAusVorrunde))
            return gruppeJeMannschaft;

        var aktuelleRunde = alleRunden.FirstOrDefault(r => r.Id == auslosung.TurnierRundeId);
        var gruppenphase  = aktuelleRunde is null ? null : alleRunden
            .Where(r => r.Rundenart == Rundenart.Gruppenphase && r.Reihenfolge < aktuelleRunde.Reihenfolge)
            .MaxBy(r => r.Reihenfolge);
        if (gruppenphase is null) return gruppeJeMannschaft;

        var gruppenAuslosung = await repository.FindenAbgeschlosseneNachRundeAsync(
            auslosung.TurnierInstanzId, gruppenphase.Id, ct);
        foreach (var e in gruppenAuslosung?.Ergebnisse ?? [])
            if (e.HeimMannschaftId is { } id)
                gruppeJeMannschaft[id] = GruppenHerkunft.Buchstabe(e.PaarungsNummer);

        return gruppeJeMannschaft;
    }

    private static bool BrauchtGruppeAusVorrunde(LostopfEintrag eintrag) =>
        eintrag.MannschaftId.HasValue || GruppenHerkunft.AusPlatzhalter(eintrag.PlatzhalterName) is null;

    // Spielklassen aus der Standard-Rangliste der Runde (#393) — dieselbe Rangliste wie für die Lostöpfe (#349).
    // Ohne Standard-Rangliste kennt kein Team seine Klasse, dann entscheidet überall das Los.
    private async Task<Spielklassen> SpielklassenLadenAsync(int? ranglisteId, CancellationToken ct) =>
        ranglisteId is int id && await ranglisteRepository.MitAbschnittenAsync(id, ct) is { } rangliste
            ? Spielklassen.Aus(rangliste)
            : Spielklassen.Keine;

    /// <summary>Gastgeber und Titelverteidiger mit fester Gruppe (#487) gehören in jede Gruppenauslosung: das Programm
    /// legt die fehlenden selbst in Topf 1, statt sich darauf zu verlassen, dass jemand sie von Hand hineinlegt. Sind
    /// die Töpfe schon voll, nennt es die übrigen in <c>OhnePlatz</c>. Speichert nicht — das übernimmt der Aufrufer.</summary>
    public async Task<(IReadOnlyDictionary<int, int> Koepfe, bool Geaendert, IReadOnlyList<string> OhnePlatz)>
        GruppenkoepfeEinlegenAsync(Auslosung auslosung, CancellationToken ct)
    {
        if (auslosung.TurnierRunde is not { Rundenart: Rundenart.Gruppenphase } runde ||
            runde.GastgeberGruppen.Count == 0 && runde.TitelverteidigerGruppe is null)
            return (new Dictionary<int, int>(), false, []);

        var kandidaten = await repository.GruppenkopfKandidatenAsync(auslosung.TurnierInstanzId, ct);
        var koepfe     = Gruppenkoepfe.Bestimmen(runde, kandidaten.GastgeberIds, kandidaten.TitelverteidigerId);
        var (geaendert, ohnePlatz) = Gruppenkoepfe.Einlegen(koepfe, auslosung.Lostoepfe, runde.EingangsMannschaftsanzahl);
        return (koepfe, geaendert, [.. ohnePlatz.Select(id => kandidaten.Namen.GetValueOrDefault(id, $"Mannschaft {id}"))]);
    }

    private static string OhnePlatzMeldung(IReadOnlyList<string> ohnePlatz) =>
        (ohnePlatz.Count == 1
            ? $"{ohnePlatz[0]} führt als Gastgeber bzw. Titelverteidiger eine Gruppe fest an und gehört"
            : $"{string.Join(", ", ohnePlatz)} führen als Gastgeber bzw. Titelverteidiger je eine Gruppe fest an und gehören") +
        " in die Lostöpfe, die sind aber schon voll. Bitte ein Team herausnehmen – der Gruppenkopf kommt dann beim " +
        "Auslosen von selbst in Topf 1.";
}

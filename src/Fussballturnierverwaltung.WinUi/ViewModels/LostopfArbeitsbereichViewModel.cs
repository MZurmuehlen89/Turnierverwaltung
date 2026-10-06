using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Application.Ranglisten;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Ranglisten;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.WinUi.Dialoge;
using Fussballturnierverwaltung.WinUi.Helpers;
using System.Collections.ObjectModel;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Was der Lostopf-Arbeitsbereich von seinem Eltern-ViewModel braucht: den geteilten Schutz des
/// DbContext, die gerade offene Auslosung und den Weg zurück (Detail neu laden, Fehler anzeigen).</summary>
public interface ILostopfArbeitsbereichKontext
{
    DbContextReentrancySchutz DbContextSchutz { get; }

    /// <summary>Die offene Auslosung — <c>null</c>, sobald Turnier oder Reiter gewechselt wurden (#413).</summary>
    Auslosung? AktuellesDetail { get; }

    Task DetailNeuLadenAsync(int auslosungId);

    void FehlerMelden(string? meldung);
}

/// <summary>Arbeitsbereich einer offenen Auslosung: Liste der verfügbaren Teams mit ihren Filtern (#344, #421),
/// die Lostöpfe, Teams hinzufügen/entfernen/verschieben und die automatische Befüllung nach Rangliste (#119).
/// Kind-ViewModel von <see cref="AuslosungViewModel"/>. Den Schutz des geteilten DbContext betritt es nur an
/// seinen Einstiegspunkten (Befehle, Aufrufe aus dem Code-Behind) — er ist nicht wiedereintrittsfähig.</summary>
public partial class LostopfArbeitsbereichViewModel : ObservableObject
{
    private readonly ILostopfArbeitsbereichKontext _kontext;
    private readonly AuslosungService _auslosungService;
    private readonly RanglisteService _ranglisteService;
    private readonly TurnierInstanzService _instanzService;
    private readonly IDialogService _dialogService;
    private bool _bearbeitbar;

    public LostopfArbeitsbereichViewModel(
        ILostopfArbeitsbereichKontext kontext,
        AuslosungService auslosungService,
        RanglisteService ranglisteService,
        TurnierInstanzService instanzService,
        IDialogService dialogService)
    {
        _kontext = kontext;
        _auslosungService = auslosungService;
        _ranglisteService = ranglisteService;
        _instanzService = instanzService;
        _dialogService = dialogService;
        Filter = new TeilnehmerFilterViewModel(VerfuegbareTeilnehmer);
        Filter.Geaendert += GefilterteTeilnehmerAbgleichen;
    }

    // Beide Listen bleiben stabile Instanzen und werden nur abgeglichen (SammlungsAbgleich): eine neue
    // ItemsSource ließ die Verfügbar-Liste nach jeder Zuweisung nach oben springen (#388).
    public ObservableCollection<TeilnehmerAnzeige> VerfuegbareTeilnehmer { get; } = [];
    public ObservableCollection<TeilnehmerAnzeige> VerfuegbareTeilnehmerGefiltert { get; } = [];

    /// <summary>Text-, Verbands- und Länderfilter über der Verfügbar-Liste.</summary>
    public TeilnehmerFilterViewModel Filter { get; }

    private static (int?, string) TeilnehmerSchluessel(TeilnehmerAnzeige t) =>
        (t.MannschaftId, t.MannschaftId.HasValue ? "" : t.Name);

    private void GefilterteTeilnehmerAbgleichen() =>
        SammlungsAbgleich.Abgleichen(
            VerfuegbareTeilnehmerGefiltert,
            VerfuegbareTeilnehmer.Where(Filter.Passt).ToList(),
            TeilnehmerSchluessel);

    // Stabile Instanz, abgeglichen statt neu angelegt (#388) — sonst verlieren die Topf-Listen ihre Scrollposition.
    public ObservableCollection<LostopfViewModel> Lostoepfe { get; } = [];

    // ── Automatische Lostopf-Zuordnung via Rangliste (Issue #119) ────────────────
    private static readonly RanglisteOption _keineRangliste = new("— Keine —", null);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RanglisteOptionen))]
    private ObservableCollection<Rangliste> _alleRanglisten = [];

    [ObservableProperty] private RanglisteOption _ausgewaehlteRanglisteOption = _keineRangliste;

    // Gecacht statt bei jedem Zugriff neu projiziert: die WinUI3-ComboBox gleicht SelectedItem per Referenz
    // ab, eine programmatische Vorauswahl (Standard-Rangliste der Runde, #349) braucht daher dieselben
    // Option-Instanzen, die auch als ItemsSource gebunden sind.
    private IReadOnlyList<RanglisteOption> _ranglisteOptionen = [_keineRangliste];
    // Auslosung, für die die Standard-Rangliste zuletzt vorausgewählt wurde — nur beim Wechsel der Auslosung
    // neu vorauswählen, sonst überschriebe jedes Detail-Neuladen (z.B. nach dem Befüllen) eine Handauswahl.
    private int? _ranglisteVorausgewaehltFuerAuslosungId;
    public IReadOnlyList<RanglisteOption> RanglisteOptionen => _ranglisteOptionen;

    partial void OnAlleRanglistenChanged(ObservableCollection<Rangliste> value) =>
        _ranglisteOptionen = [_keineRangliste, .. value.Select(r => new RanglisteOption(r.Name, r))];

    // Die TwoWay-Bindung nullt die Auswahl transient, wenn die ItemsSource ausgetauscht wird.
    partial void OnAusgewaehlteRanglisteOptionChanged(RanglisteOption value)
    {
        if (value is null) AusgewaehlteRanglisteOption = _keineRangliste;
    }

    /// <summary>Wählt die neueste Rangliste der Standard-Quelle von <paramref name="runde"/> vor (#349).
    /// Ohne Vorgabe bleibt die bisherige Auswahl stehen. Liefert die gewählte Rangliste oder <c>null</c>.</summary>
    private Rangliste? StandardRanglisteVorauswaehlen(TurnierRunde? runde)
    {
        if (runde?.StandardRanglistenQuelleId is not int quelleId) return null;
        var rangliste = LostopfVorbelegung.NeuesteRanglisteZurQuelle(AlleRanglisten, quelleId);
        AusgewaehlteRanglisteOption = _ranglisteOptionen.FirstOrDefault(o => o.Wert == rangliste) ?? _keineRangliste;
        return rangliste;
    }

    // ── Füllstand ─────────────────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TeamsStatusText))]
    [NotifyPropertyChangedFor(nameof(TeamsVollstaendig))]
    private int _teamsZugewiesen;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TeamsStatusText))]
    [NotifyPropertyChangedFor(nameof(TeamsVollstaendig))]
    private int _teamsBenoetigt;

    public bool TeamsVollstaendig => TeamsBenoetigt > 0 && TeamsZugewiesen == TeamsBenoetigt;

    public string TeamsStatusText => TeamsBenoetigt == 0
        ? $"{TeamsZugewiesen} Teams zugewiesen"
        : TeamsZugewiesen == TeamsBenoetigt
            ? $"✓  Alle {TeamsBenoetigt} Teams zugewiesen"
            : TeamsZugewiesen > TeamsBenoetigt
                ? $"⚠  {TeamsZugewiesen} Teams zugewiesen — {TeamsZugewiesen - TeamsBenoetigt} zu viel (Runde erwartet {TeamsBenoetigt})"
                : $"{TeamsZugewiesen} / {TeamsBenoetigt} Teams zugewiesen — noch {TeamsBenoetigt - TeamsZugewiesen} fehlen";

    // ── Vom Eltern-ViewModel aufgerufen ────────────────────────────────────────

    /// <summary>Einmalig beim Aktivieren der Seite und vor der Befüllung nach Standard-Rangliste (#349).</summary>
    public async Task RanglistenLadenAsync()
    {
        var ranglisten = await _ranglisteService.AlleAbrufenAsync();
        AlleRanglisten = new ObservableCollection<Rangliste>(ranglisten);
    }

    /// <summary>Ob die Töpfe der offenen Auslosung verändert werden dürfen (Detail offen und nicht abgeschlossen).</summary>
    public void BearbeitbarSetzen(bool bearbeitbar)
    {
        _bearbeitbar = bearbeitbar;
        LostoepfeAutomatischBefuellenCommand.NotifyCanExecuteChanged();
        AlleToepfeZuruecksetzenCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Turnierwechsel: Verfügbar-Liste und Filter zurücksetzen.</summary>
    public void Leeren()
    {
        VerfuegbareTeilnehmer.Clear();
        Filter.Zuruecksetzen();
        GefilterteTeilnehmerAbgleichen();
    }

    /// <summary>Gleicht Verfügbar-Liste und Lostöpfe mit dem geladenen Stand von <paramref name="detail"/> ab;
    /// <paramref name="alleTeilnehmer"/> sind alle für die Runde in Frage kommenden Teams,
    /// <paramref name="mannschaftsart"/> ist die Vorgabe aus dem Turnierkopf (bestimmt die angebotenen Filter).</summary>
    public void Zeigen(Auslosung detail, IReadOnlyList<TeilnehmerAnzeige> alleTeilnehmer, MannschaftsartFilter mannschaftsart)
    {
        VerfuegbareAbgleichen(detail, alleTeilnehmer, mannschaftsart);
        LostoepfeAbgleichen(detail);

        TeamsZugewiesen = detail.Lostoepfe.SelectMany(l => l.Eintraege).Count();
        TeamsBenoetigt  = detail.TurnierRunde?.EingangsMannschaftsanzahl ?? 0;

        if (_ranglisteVorausgewaehltFuerAuslosungId != detail.Id)
        {
            _ranglisteVorausgewaehltFuerAuslosungId = detail.Id;
            StandardRanglisteVorauswaehlen(detail.TurnierRunde);
        }
    }

    /// <summary>Verfügbar ist, wer noch in keinem Lostopf liegt. Abgleich statt Neuanlage, damit Scrollposition
    /// und Auswahl erhalten bleiben (#388).</summary>
    private void VerfuegbareAbgleichen(Auslosung detail, IReadOnlyList<TeilnehmerAnzeige> alleTeilnehmer,
        MannschaftsartFilter mannschaftsart)
    {
        var eintraege = detail.Lostoepfe.SelectMany(l => l.Eintraege).ToList();
        var zugeordneteMannschaftIds = eintraege.Where(e => e.MannschaftId.HasValue).Select(e => e.MannschaftId!.Value).ToHashSet();
        var zugeordnetePlatzhalter   = eintraege.Where(e => e.PlatzhalterName != null).Select(e => e.PlatzhalterName!).ToHashSet();

        SammlungsAbgleich.Abgleichen(
            VerfuegbareTeilnehmer,
            alleTeilnehmer
                .Where(t => t.MannschaftId.HasValue
                    ? !zugeordneteMannschaftIds.Contains(t.MannschaftId.Value)
                    : !zugeordnetePlatzhalter.Contains(t.Name))
                .OrderBy(t => t.Name)
                .ToList(),
            TeilnehmerSchluessel);

        // Die Filteroptionen werden VOR der gefilterten Liste nachgezogen: fällt die gewählte Option weg, steht
        // der Filter schon wieder auf „Alle“, wenn die Liste abgeglichen wird.
        Filter.OptionenAbgleichen(mannschaftsart);
        GefilterteTeilnehmerAbgleichen();
    }

    private void LostoepfeAbgleichen(Auslosung detail)
    {
        SammlungsAbgleich.Abgleichen(
            Lostoepfe,
            detail.Lostoepfe.Select(l => new LostopfViewModel
            {
                Id          = l.Id,
                Name        = l.Name,
                Reihenfolge = l.Reihenfolge
            }).ToList(),
            l => l.Id,
            (ist, soll) => ist.Name == soll.Name && ist.Reihenfolge == soll.Reihenfolge);
        foreach (var topf in Lostoepfe)
            SammlungsAbgleich.Abgleichen(
                topf.Teilnehmer,
                detail.Lostoepfe.First(l => l.Id == topf.Id).Eintraege
                    .OrderBy(e => e.Id)
                    .Select(TeilnehmerAnzeige.AusEintrag)
                    .ToList(),
                t => t.EintragId);
        AlleToepfeZuruecksetzenCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Befüllt die Töpfe nach der Standard-Rangliste der Runde (#349). Läuft unter dem Schutz des
    /// Aufrufers. Liefert <c>false</c>, wenn für die Standard-Quelle noch keine Rangliste importiert ist.</summary>
    public async Task<bool> NachStandardRanglisteBefuellenAsync()
    {
        if (StandardRanglisteVorauswaehlen(_kontext.AktuellesDetail?.TurnierRunde) is not { } rangliste) return false;
        await LostoepfeNachRanglisteBefuellenAsync(rangliste.Id);
        return true;
    }

    // ── Töpfe von Hand befüllen und leeren ────────────────────────────────────

    /// <summary>Aufgerufen aus dem Drop-Handler des Codebehind (Drag&amp;Drop aus "Verfügbar" auf einen
    /// Lostopf, Precedent: <see cref="Views.VorbelegungPage"/>).</summary>
    public Task TeilnehmerZuTopfHinzufuegenAsync(int topfId, TeilnehmerAnzeige teilnehmer)
        => TeilnehmerZuTopfHinzufuegenAsync(topfId, [teilnehmer]);

    /// <summary>Mehrfachselektions-Variante (Issue #28-Muster aus dem WPF-Original: eine Liste statt eines
    /// einzelnen Teilnehmers deckt Einzel- UND Mehrfachauswahl uniform ab) — aufgerufen aus dem
    /// Rechtsklick-Kontextmenü sowie dem Mehrfach-Drag&amp;Drop im Codebehind. Ein gemeinsamer Reload am Ende
    /// statt pro Teilnehmer, damit die UI nicht mehrfach kurz aufflackert.</summary>
    public Task TeilnehmerZuTopfHinzufuegenAsync(int topfId, IReadOnlyList<TeilnehmerAnzeige> teilnehmer) =>
        teilnehmer.Count == 0
            ? Task.CompletedTask
            : ToepfeAendernAsync(async detail =>
            {
                foreach (var t in teilnehmer)
                    await InTopfLegenAsync(detail.Id, topfId, t);
            });

    /// <summary>Rechtsklick-Kontextmenü in einem Lostopf: Eintrag/Einträge zurück in "Verfügbar".</summary>
    public Task TeilnehmerAusTopfEntfernenAsync(int topfId, int eintragId)
        => TeilnehmerAusTopfEntfernenAsync(topfId, [eintragId]);

    /// <summary>Mehrfachselektions-Variante, siehe <see cref="TeilnehmerZuTopfHinzufuegenAsync(int, IReadOnlyList{TeilnehmerAnzeige})"/>.</summary>
    public Task TeilnehmerAusTopfEntfernenAsync(int topfId, IReadOnlyList<int> eintragIds) =>
        eintragIds.Count == 0
            ? Task.CompletedTask
            : ToepfeAendernAsync(async detail =>
            {
                foreach (var eintragId in eintragIds)
                    await _auslosungService.EintragAusLostopfEntfernenAsync(detail.Id, topfId, eintragId);
            });

    /// <summary>Drag&amp;Drop aus einem Lostopf in einen anderen (Codebehind) —
    /// <see cref="TeilnehmerZuTopfHinzufuegenAsync(int, IReadOnlyList{TeilnehmerAnzeige})"/> allein reicht nicht:
    /// <c>MannschaftZuLostopfHinzufuegenAsync</c> lehnt eine bereits einem Lostopf zugeordnete Mannschaft ab, der
    /// Quell-Lostopf muss also zuerst geleert werden.</summary>
    public Task TeilnehmerZuTopfVerschiebenAsync(IReadOnlyList<TeilnehmerAnzeige> teilnehmer, int? vonTopfId, int nachTopfId) =>
        teilnehmer.Count == 0
            ? Task.CompletedTask
            : ToepfeAendernAsync(async detail =>
            {
                foreach (var t in teilnehmer)
                {
                    if (vonTopfId.HasValue)
                        await _auslosungService.EintragAusLostopfEntfernenAsync(detail.Id, vonTopfId.Value, t.EintragId);
                    await InTopfLegenAsync(detail.Id, nachTopfId, t);
                }
            });

    [RelayCommand]
    private Task TopfLeeren(LostopfViewModel topf) =>
        ToepfeAendernAsync(detail => _auslosungService.LostopfLeerenAsync(detail.Id, topf.Id));

    [RelayCommand(CanExecute = nameof(KannAlleToepfeZuruecksetzen))]
    private async Task AlleToepfeZuruecksetzen()
    {
        if (_kontext.AktuellesDetail is null) return;
        var bestaetigt = await _dialogService.BestaetigenAsync(
            "Töpfe zurücksetzen", "Alle Töpfe leeren und alle Teams zurück in die Verfügbar-Liste?");
        if (!bestaetigt) return;

        await ToepfeAendernAsync(detail => _auslosungService.AlleToepfeZuruecksetzenAsync(detail.Id));
    }

    /// <summary>Gemeinsamer Rahmen aller Topf-Änderungen von Hand: Schutz des geteilten DbContext betreten, auf der
    /// DANN offenen Auslosung arbeiten (hinter einem Turnier- oder Reiterwechsel gibt es keine mehr, #413), das
    /// Detail neu laden und Fehler in die Fehlermeldung des Reiters schreiben.</summary>
    private async Task ToepfeAendernAsync(Func<Auslosung, Task> aenderung)
    {
        using var schutz = await _kontext.DbContextSchutz.BetretenAsync();
        if (_kontext.AktuellesDetail is not { } detail) return;
        _kontext.FehlerMelden(null);
        try
        {
            await aenderung(detail);
            await _kontext.DetailNeuLadenAsync(detail.Id);
        }
        catch (Exception ex) { _kontext.FehlerMelden(ex.Message); }
    }

    private Task InTopfLegenAsync(int auslosungId, int topfId, TeilnehmerAnzeige teilnehmer) =>
        teilnehmer.MannschaftId.HasValue
            ? _auslosungService.MannschaftZuLostopfHinzufuegenAsync(auslosungId, topfId, teilnehmer.MannschaftId.Value)
            : _auslosungService.PlatzhalterZuLostopfHinzufuegenAsync(auslosungId, topfId, teilnehmer.Name);

    // ── Töpfe nach Rangliste befüllen (#119) ──────────────────────────────────

    /// <summary>
    /// Verteilt die aktuell noch unzugeordneten Mannschaften (VerfuegbareTeilnehmer, Platzhalter
    /// ausgenommen) anhand der ausgewählten Rangliste auf die Lostöpfe: die bestplatzierten Teams
    /// zuerst in Topf 1, die nächsten in Topf 2 usw. Bereits manuell zugeordnete Teams bleiben
    /// unangetastet — der Button ergänzt nur, was noch in "Verfügbar" steht. Teams ohne
    /// Ranglisten-Eintrag werden ans Ende einsortiert (landen tendenziell im letzten Topf).
    /// </summary>
    [RelayCommand(CanExecute = nameof(KannLostoepfeAutomatischBefuellen))]
    private async Task LostoepfeAutomatischBefuellen()
    {
        using var schutz = await _kontext.DbContextSchutz.BetretenAsync();
        if (_kontext.AktuellesDetail is null) return;
        _kontext.FehlerMelden(null);

        var ausgewaehlteRangliste = AusgewaehlteRanglisteOption.Wert;
        if (ausgewaehlteRangliste is null)
        {
            _kontext.FehlerMelden("Bitte zuerst eine Rangliste auswählen.");
            return;
        }

        try { await LostoepfeNachRanglisteBefuellenAsync(ausgewaehlteRangliste.Id); }
        catch (Exception ex) { _kontext.FehlerMelden(ex.Message); }
    }

    /// <summary>Kern von <see cref="LostoepfeAutomatischBefuellen"/>, auch beim Anlegen einer Auslosung mit
    /// Standard-Rangliste genutzt (#349). Setzt ein geladenes Detail voraus und läuft unter dem Schutz des
    /// Aufrufers. Den Titelverteidiger des Turniers setzt sie in Topf 1, wenn die Runde das vorgibt
    /// (UEFA-Champions-League-Ligaphase).</summary>
    private async Task LostoepfeNachRanglisteBefuellenAsync(int ranglisteId)
    {
        if (_kontext.AktuellesDetail is not { } detail) return;
        if (Lostoepfe.Count == 0)
        {
            _kontext.FehlerMelden("Diese Auslosung hat keine Lostöpfe.");
            return;
        }

        var rangliste = await _ranglisteService.FindenNachIdAsync(ranglisteId);
        if (rangliste is null) return;

        var rangProMannschaft = rangliste.Eintraege.ToDictionary(e => e.MannschaftId, e => e.Rang);
        var titelverteidigerId = await TitelverteidigerFuerTopf1Async(detail);
        var teamsZuVerteilen = TeamsZuVerteilen(rangProMannschaft, titelverteidigerId);
        if (teamsZuVerteilen.Count == 0)
        {
            _kontext.FehlerMelden("Keine verfügbaren Mannschaften zum Zuordnen.");
            return;
        }

        var toepfe = Lostoepfe.OrderBy(l => l.Reihenfolge).ToList();
        var verteilung = LostopfVorbelegung.Verteilen(teamsZuVerteilen, rangProMannschaft, toepfe.Count, titelverteidigerId);
        for (int topfIndex = 0; topfIndex < toepfe.Count; topfIndex++)
        {
            foreach (var mannschaftId in verteilung[topfIndex])
                await _auslosungService.MannschaftZuLostopfHinzufuegenAsync(detail.Id, toepfe[topfIndex].Id, mannschaftId);
        }

        await _kontext.DetailNeuLadenAsync(detail.Id);
    }

    private async Task<int?> TitelverteidigerFuerTopf1Async(Auslosung detail) =>
        detail.TurnierRunde?.TitelverteidigerInTopf1 == true
            ? (await _instanzService.FindenNachIdMitRundenAsync(detail.TurnierInstanzId))?.TitelverteidigerId
            : null;

    /// <summary>Noch verfügbare Mannschaften in Setzreihenfolge (Titelverteidiger, dann Rang, dann Name) — höchstens
    /// so viele, wie der Runde noch fehlen.</summary>
    private List<int> TeamsZuVerteilen(Dictionary<int, int> rangProMannschaft, int? titelverteidigerId)
    {
        var teams = VerfuegbareTeilnehmer
            .Where(t => t.MannschaftId.HasValue)
            .OrderBy(t => t.MannschaftId == titelverteidigerId ? 0 : 1)
            .ThenBy(t => rangProMannschaft.TryGetValue(t.MannschaftId!.Value, out var rang) ? rang : int.MaxValue)
            .ThenBy(t => t.Name)
            .Select(t => t.MannschaftId!.Value);

        return TeamsBenoetigt > 0
            ? teams.Take(Math.Max(0, TeamsBenoetigt - TeamsZugewiesen)).ToList()
            : teams.ToList();
    }

    private bool KannLostoepfeAutomatischBefuellen() => _bearbeitbar;
    private bool KannAlleToepfeZuruecksetzen()       => _bearbeitbar && Lostoepfe.Any(l => l.Teilnehmer.Count > 0);
}

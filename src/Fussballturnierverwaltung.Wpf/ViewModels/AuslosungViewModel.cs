using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Wpf.Ribbon;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace Fussballturnierverwaltung.Wpf.ViewModels;

// ── Darstellungstypen ────────────────────────────────────────────────────────

public record TeilnehmerAnzeige(int? MannschaftId, string Name, string? Kuerzel, int EintragId = 0)
{
    public bool IstPlatzhalter => !MannschaftId.HasValue;

    public static TeilnehmerAnzeige AusMannschaft(Mannschaft m) =>
        new(m.Id, m.Name, m.Land?.Kurzname);

    public static TeilnehmerAnzeige AusPlatzhalter(string name) =>
        new(null, name, null);

    public static TeilnehmerAnzeige AusEintrag(LostopfEintrag e) =>
        e.MannschaftId.HasValue
            ? new(e.MannschaftId, e.Mannschaft?.Name ?? "?", e.Mannschaft?.Land?.Kurzname, e.Id)
            : new(null, e.PlatzhalterName ?? "?", null, e.Id);
}

public record ErgebnisAnzeige(int PaarungsNummer, string HeimName, string AuswaertsName, bool IstFreilos);
public record GruppenErgebnisAnzeige(int GruppenNummer, string GruppenName, IReadOnlyList<string> Teams);

public class LostopfViewModel : ObservableObject
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public int Reihenfolge { get; init; }
    public ObservableCollection<TeilnehmerAnzeige> Teilnehmer { get; init; } = [];
}

public class RegelViewModel : ObservableObject
{
    private readonly Func<bool, Task> _callback;
    private bool _istAktiv;

    public AuslosungsRegelArt Regelart { get; init; }
    public string Anzeige { get; init; } = "";

    public bool IstAktiv
    {
        get => _istAktiv;
        set { if (SetProperty(ref _istAktiv, value)) _ = _callback(value); }
    }

    public RegelViewModel(AuslosungsRegelArt art, string anzeige, bool initial, Func<bool, Task> callback)
    {
        Regelart = art; Anzeige = anzeige; _istAktiv = initial; _callback = callback;
    }
}

public record AuslosungsartOption(string Anzeige, Auslosungsart Wert);

// ── Haupt-ViewModel ─────────────────────────────────────────────────────────

public partial class AuslosungViewModel : ViewModelBase, IRibbonAware
{
    private readonly AuslosungService _auslosungService;
    private readonly TurnierInstanzService _instanzService;
    private readonly MannschaftService _mannschaftService;

    private Auslosung? _auslosungDetail;

    // ── Linke Spalte ──────────────────────────────────────────────────────────
    [ObservableProperty] private ObservableCollection<TurnierInstanz> _instanzen = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NeuCommand))]
    [NotifyPropertyChangedFor(nameof(KeinTurnierGewaehlt))]
    private TurnierInstanz? _ausgewaehlteInstanz;

    [ObservableProperty] private ObservableCollection<Auslosung> _auslosungen = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoeschenCommand))]
    private Auslosung? _ausgewaehlteAuslosung;

    [ObservableProperty] private ObservableCollection<TurnierRunde> _instanzRunden = [];

    // ── Neue-Auslosung-Formular ────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NeuCommand))]
    [NotifyCanExecuteChangedFor(nameof(SpeichernCommand))]
    [NotifyCanExecuteChangedFor(nameof(AbbrechenCommand))]
    [NotifyCanExecuteChangedFor(nameof(LoeschenCommand))]
    private bool _neuFormAktiv;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SpeichernCommand))]
    private string _neuName = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AuslosungsartHinweis))]
    [NotifyPropertyChangedFor(nameof(AuslosungsartWarnung))]
    private Auslosungsart _neuAuslosungsart = Auslosungsart.Zufall;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SpeichernCommand))]
    [NotifyPropertyChangedFor(nameof(AuslosungsartHinweis))]
    [NotifyPropertyChangedFor(nameof(AuslosungsartWarnung))]
    private TurnierRunde? _neuRunde;

    private string _autoNeuName = "";

    // ── Auslosung-Detail ──────────────────────────────────────────────────────
    [ObservableProperty] private bool _detailAktiv;
    [ObservableProperty] private bool _istBearbeitbar;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DetailAuslosungsartHinweis))]
    [NotifyPropertyChangedFor(nameof(DetailAuslosungsartWarnung))]
    private Auslosungsart _detailAuslosungsart;

    [ObservableProperty] private string _detailName = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IstPaarungsergebnis))]
    private bool _istAbgeschlossen;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IstPaarungsergebnis))]
    private bool _istGruppenergebnis;

    public bool IstPaarungsergebnis => IstAbgeschlossen && !IstGruppenergebnis;

    [ObservableProperty] private string _auslosungKopfzeile = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VerfuegbareTeilnehmerGefiltert))]
    private ObservableCollection<TeilnehmerAnzeige> _verfuegbareTeilnehmer = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VerfuegbareTeilnehmerGefiltert))]
    private string _teilnehmerFilter = "";

    [ObservableProperty] private TeilnehmerAnzeige? _ausgewaehlterVerfuegbarTeilnehmer;

    [ObservableProperty] private ObservableCollection<LostopfViewModel> _lostoepfe = [];
    [ObservableProperty] private ObservableCollection<RegelViewModel> _regeln = [];
    [ObservableProperty] private ObservableCollection<ErgebnisAnzeige> _ergebnisseAnzeige = [];
    [ObservableProperty] private ObservableCollection<GruppenErgebnisAnzeige> _gruppenErgebnisse = [];
    [ObservableProperty] private string _neuerLostopfName = "";

    // ── Status ────────────────────────────────────────────────────────────────
    [ObservableProperty] private bool _istLadend;
    [ObservableProperty] private string? _fehlermeldung;

    // ── Berechnete Eigenschaften ─────────────────────────────────────────────
    public bool KeinTurnierGewaehlt => AusgewaehlteInstanz is null;

    public IEnumerable<TeilnehmerAnzeige> VerfuegbareTeilnehmerGefiltert =>
        string.IsNullOrWhiteSpace(TeilnehmerFilter)
            ? VerfuegbareTeilnehmer
            : VerfuegbareTeilnehmer.Where(t =>
                t.Name.Contains(TeilnehmerFilter, StringComparison.OrdinalIgnoreCase));

    public IReadOnlyList<AuslosungsartOption> AuslosungsartOptionen =>
        Enum.GetValues<Auslosungsart>()
            .Select(a => new AuslosungsartOption(AuslosungsartAnzeigeName(a), a))
            .ToList();

    public string AuslosungsartHinweis         => AuslosungsartHinweisText(NeuAuslosungsart);
    public string? AuslosungsartWarnung        => AuslosungsartWarnungText(NeuAuslosungsart, NeuRunde?.Rundenart);
    public string DetailAuslosungsartHinweis   => AuslosungsartHinweisText(DetailAuslosungsart);
    public string? DetailAuslosungsartWarnung  => AuslosungsartWarnungText(DetailAuslosungsart, _auslosungDetail?.TurnierRunde?.Rundenart);

    private static string AuslosungsartHinweisText(Auslosungsart art) => art switch
    {
        Auslosungsart.Zufall =>
            "Alle Teams werden zufällig gemischt und paarweise zugelost. Passt zu jeder Rundenart.",
        Auslosungsart.NachSetzliste =>
            "Gesetzte Teams (Topf 1) werden gleichmäßig auf Gruppen oder Paare verteilt, " +
            "damit stärkere Teams erst in späteren Runden aufeinandertreffen. " +
            "Typisch für Nationenturniere und nationale Pokal-Auslosungen.",
        Auslosungsart.NachToepfen =>
            "Teams werden manuell auf Töpfe verteilt und dann topfübergreifend zugelost — " +
            "mindestens ein Lostopf ist erforderlich. " +
            "Typisch für UEFA Europa League und internationale Pokal-Wettbewerbe.",
        Auslosungsart.TopfZuordnung =>
            "Jeder Topf belegt genau einen Setzplatz: Topf 1 → Gruppenplatz A, Topf 2 → Gruppenplatz B usw. " +
            "Alle Töpfe müssen gleich groß sein. " +
            "Gängig in der UEFA Champions League (Gruppen- und K.O.-Phase).",
        Auslosungsart.GruppensiegerGegenGruppenzweite =>
            "Gruppensieger werden den Zweitplatzierten einer anderen Gruppe zugelost, " +
            "wobei Teams aus derselben Gruppe nicht aufeinandertreffen dürfen. " +
            "Typisch für K.O.-Runden nach einer Gruppenphase (z. B. WM-Achtelfinale, UEFA-Playoff).",
        Auslosungsart.Benutzerdefiniert =>
            "Teams werden manuell den Losplätzen zugewiesen — keine automatische Verlosung. " +
            "Für besondere Anforderungen oder wenn keine andere Methode passt.",
        _ => ""
    };

    private static string? AuslosungsartWarnungText(Auslosungsart art, Rundenart? rundenart) =>
        (art, rundenart) switch
        {
            (Auslosungsart.GruppensiegerGegenGruppenzweite, Rundenart.Liga) or
            (Auslosungsart.GruppensiegerGegenGruppenzweite, Rundenart.Gruppenphase) or
            (Auslosungsart.GruppensiegerGegenGruppenzweite, Rundenart.TopfLiga) or
            (Auslosungsart.GruppensiegerGegenGruppenzweite, Rundenart.SchweizerSystem) =>
                "⚠  Diese Auslosungsart setzt voraus, dass bereits eine Gruppenphase gespielt wurde. " +
                "Sie ist für K.O.-Runden gedacht, die auf eine Gruppenphase folgen.",
            (Auslosungsart.TopfZuordnung, Rundenart.Gruppenphase) =>
                "⚠  Für eine klassische Gruppenphase verwenden Sie 'Nach Töpfen' — " +
                "dabei wird ein Team pro Topf in jede Gruppe gelost. " +
                "'Topf-Zuordnung' ist für die UCL-Ligaphase (direkte Gegnerbestimmung) gedacht.",
            (Auslosungsart.TopfZuordnung, Rundenart.KoRunde) or
            (Auslosungsart.TopfZuordnung, Rundenart.Finale) or
            (Auslosungsart.TopfZuordnung, Rundenart.SpielUmPlatzDrei) =>
                "⚠  Topf-Zuordnung weist Teams direkt Gruppenplätzen zu. " +
                "Für K.O.-Runden ist 'Nach Töpfen' oder 'Zufällig' besser geeignet.",
            (Auslosungsart.NachSetzliste, Rundenart.SchweizerSystem) =>
                "⚠  Im Schweizer System wird die Paarung automatisch anhand der Tabelle berechnet. " +
                "Eine manuelle Setzlisten-Auslosung ist hier in der Regel nicht sinnvoll.",
            _ => null
        };

    // ── IRibbonAware ──────────────────────────────────────────────────────────
    public string RibbonTabTitel => "Auslosung";
    public ICommand NeuBefehl => NeuCommand;
    public ICommand SpeichernBefehl => SpeichernCommand;
    public ICommand AbbrechenBefehl => AbbrechenCommand;
    public ICommand LoeschenBefehl => LoeschenCommand;

    public AuslosungViewModel(
        AuslosungService auslosungService,
        TurnierInstanzService instanzService,
        MannschaftService mannschaftService)
    {
        _auslosungService  = auslosungService;
        _instanzService    = instanzService;
        _mannschaftService = mannschaftService;
    }

    public override async Task AktivierenAsync()
    {
        IstLadend = true;
        try
        {
            var instanzen = await _instanzService.AlleAbrufenAsync();
            Instanzen = new ObservableCollection<TurnierInstanz>(instanzen);
        }
        finally { IstLadend = false; }
    }

    // ── Ribbon-Befehle ────────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(KannNeu))]
    private void Neu()
    {
        NeuFormAktiv     = true;
        DetailAktiv      = false;
        _autoNeuName     = "";
        NeuName          = "";
        NeuAuslosungsart = Auslosungsart.Zufall;
        NeuRunde         = InstanzRunden.FirstOrDefault();
        Fehlermeldung    = null;
    }

    [RelayCommand(CanExecute = nameof(KannSpeichern))]
    private async Task Speichern()
    {
        if (AusgewaehlteInstanz is null || NeuRunde is null) return;
        Fehlermeldung = null;
        try
        {
            var eingabe = new AuslosungErstellenEingabe(
                AusgewaehlteInstanz.Id, NeuRunde.Id, NeuName.Trim(),
                NeuAuslosungsart, false);
            var neu = await _auslosungService.ErstellenAsync(eingabe);
            NeuFormAktiv = false;

            var auslosungen = await _auslosungService.AlleNachInstanzAbrufenAsync(AusgewaehlteInstanz.Id);
            Auslosungen = new ObservableCollection<Auslosung>(auslosungen);
            AusgewaehlteAuslosung = Auslosungen.FirstOrDefault(a => a.Id == neu.Id);
        }
        catch (Exception ex) { Fehlermeldung = ex.Message; }
    }

    [RelayCommand(CanExecute = nameof(KannAbbrechen))]
    private void Abbrechen()
    {
        NeuFormAktiv = false;
        if (AusgewaehlteAuslosung is not null)
            DetailAktiv = true;
    }

    [RelayCommand(CanExecute = nameof(KannLoeschen))]
    private async Task Loeschen()
    {
        if (AusgewaehlteAuslosung is null) return;
        var r = MessageBox.Show($"Soll '{AusgewaehlteAuslosung.Name}' wirklich gelöscht werden?",
            "Auslosung löschen", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (r != MessageBoxResult.Yes) return;
        Fehlermeldung = null;
        try
        {
            await _auslosungService.LoeschenAsync(AusgewaehlteAuslosung.Id);
            var auslosungen = await _auslosungService.AlleNachInstanzAbrufenAsync(AusgewaehlteInstanz!.Id);
            Auslosungen = new ObservableCollection<Auslosung>(auslosungen);
            AusgewaehlteAuslosung = null;
            DetailAktiv = false;
        }
        catch (Exception ex) { Fehlermeldung = ex.Message; }
    }

    // ── Workspace-Befehle ─────────────────────────────────────────────────────

    [RelayCommand]
    private async Task LostopfHinzufuegen()
    {
        if (_auslosungDetail is null || string.IsNullOrWhiteSpace(NeuerLostopfName)) return;
        Fehlermeldung = null;
        try
        {
            await _auslosungService.LostopfHinzufuegenAsync(_auslosungDetail.Id, NeuerLostopfName.Trim());
            NeuerLostopfName = "";
            await LadeDetailAsync(_auslosungDetail.Id);
        }
        catch (Exception ex) { Fehlermeldung = ex.Message; }
    }

    [RelayCommand]
    private async Task TeilnehmerZuTopfHinzufuegen(int topfId)
    {
        if (_auslosungDetail is null || AusgewaehlterVerfuegbarTeilnehmer is null) return;
        Fehlermeldung = null;
        var t = AusgewaehlterVerfuegbarTeilnehmer;
        try
        {
            if (t.MannschaftId.HasValue)
                await _auslosungService.MannschaftZuLostopfHinzufuegenAsync(_auslosungDetail.Id, topfId, t.MannschaftId.Value);
            else
                await _auslosungService.PlatzhalterZuLostopfHinzufuegenAsync(_auslosungDetail.Id, topfId, t.Name);
            await LadeDetailAsync(_auslosungDetail.Id);
        }
        catch (Exception ex) { Fehlermeldung = ex.Message; }
    }

    [RelayCommand]
    private async Task AuslosungAusfuehren()
    {
        if (_auslosungDetail is null) return;
        Fehlermeldung = null;
        try
        {
            await _auslosungService.AusfuehrenAsync(_auslosungDetail.Id);
            await LadeDetailAsync(_auslosungDetail.Id);
            await AktualisiereSeitenlisteAsync();
        }
        catch (Exception ex) { Fehlermeldung = ex.Message; }
    }

    [RelayCommand]
    private async Task DetailBearbeiten()
    {
        if (_auslosungDetail is null || string.IsNullOrWhiteSpace(DetailName)) return;
        Fehlermeldung = null;
        try
        {
            await _auslosungService.BearbeitenAsync(_auslosungDetail.Id, DetailName, DetailAuslosungsart);
            await LadeDetailAsync(_auslosungDetail.Id);
            await AktualisiereSeitenlisteAsync();
        }
        catch (Exception ex) { Fehlermeldung = ex.Message; }
    }

    [RelayCommand]
    private async Task AuslosungZuruecksetzen()
    {
        if (_auslosungDetail is null) return;
        Fehlermeldung = null;
        try
        {
            await _auslosungService.ZuruecksetzenAsync(_auslosungDetail.Id);
            await LadeDetailAsync(_auslosungDetail.Id);
            await AktualisiereSeitenlisteAsync();
        }
        catch (Exception ex) { Fehlermeldung = ex.Message; }
    }

    // ── Öffentliche Methoden für D&D und Kontextmenü ─────────────────────────

    public async Task TeilnehmerZuTopfVerschieben(TeilnehmerAnzeige teilnehmer, int? vonTopfId, int nachTopfId)
    {
        if (_auslosungDetail is null) return;
        Fehlermeldung = null;
        try
        {
            if (vonTopfId.HasValue)
                await _auslosungService.EintragAusLostopfEntfernenAsync(_auslosungDetail.Id, vonTopfId.Value, teilnehmer.EintragId);

            if (teilnehmer.MannschaftId.HasValue)
                await _auslosungService.MannschaftZuLostopfHinzufuegenAsync(_auslosungDetail.Id, nachTopfId, teilnehmer.MannschaftId.Value);
            else
                await _auslosungService.PlatzhalterZuLostopfHinzufuegenAsync(_auslosungDetail.Id, nachTopfId, teilnehmer.Name);

            await LadeDetailAsync(_auslosungDetail.Id);
        }
        catch (Exception ex) { Fehlermeldung = ex.Message; }
    }

    public async Task TeilnehmerZuVerfuegbarZurueck(TeilnehmerAnzeige teilnehmer, int vonTopfId)
    {
        if (_auslosungDetail is null) return;
        Fehlermeldung = null;
        try
        {
            await _auslosungService.EintragAusLostopfEntfernenAsync(_auslosungDetail.Id, vonTopfId, teilnehmer.EintragId);
            await LadeDetailAsync(_auslosungDetail.Id);
        }
        catch (Exception ex) { Fehlermeldung = ex.Message; }
    }

    // ── Interne Hilfsmethoden ─────────────────────────────────────────────────

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName == nameof(AusgewaehlteInstanz))
            _ = BeiInstanzAenderungAsync();
        if (e.PropertyName == nameof(AusgewaehlteAuslosung))
            _ = BeiAuslosungAenderungAsync();
    }

    private async Task BeiInstanzAenderungAsync()
    {
        Auslosungen.Clear();
        InstanzRunden.Clear();
        VerfuegbareTeilnehmer.Clear();
        TeilnehmerFilter = "";
        DetailAktiv  = false;
        NeuFormAktiv = false;
        if (AusgewaehlteInstanz is null) return;

        IstLadend = true;
        try
        {
            var auslosungen = await _auslosungService.AlleNachInstanzAbrufenAsync(AusgewaehlteInstanz.Id);
            Auslosungen = new ObservableCollection<Auslosung>(auslosungen);

            var detail = await _instanzService.FindenNachIdMitRundenAsync(AusgewaehlteInstanz.Id);
            if (detail is not null)
                InstanzRunden = new ObservableCollection<TurnierRunde>(detail.Runden.OrderBy(r => r.Reihenfolge));
        }
        finally { IstLadend = false; }
    }

    private async Task BeiAuslosungAenderungAsync()
    {
        NeuFormAktiv = false;
        if (AusgewaehlteAuslosung is null) { DetailAktiv = false; return; }
        await LadeDetailAsync(AusgewaehlteAuslosung.Id);
    }

    private async Task LadeDetailAsync(int auslosungId)
    {
        IstLadend = true;
        try
        {
            var detail = await _auslosungService.FindenNachIdAsync(auslosungId);
            if (detail is null) return;
            _auslosungDetail = detail;

            // Determine round order to decide real-teams vs placeholders
            var aktuelleRunde = InstanzRunden.FirstOrDefault(r => r.Id == detail.TurnierRundeId);
            var vorherigeRunde = aktuelleRunde is not null
                ? InstanzRunden.Where(r => r.Reihenfolge < aktuelleRunde.Reihenfolge)
                               .OrderByDescending(r => r.Reihenfolge)
                               .FirstOrDefault()
                : null;

            IReadOnlyList<TeilnehmerAnzeige> alleTeilnehmer;
            if (vorherigeRunde is null)
            {
                var teams = await _mannschaftService.AlleNachTurnierfilterAsync(
                    AusgewaehlteInstanz?.VerbandId,
                    AusgewaehlteInstanz?.MannschaftsartFilter ?? MannschaftsartFilter.Alle);
                alleTeilnehmer = teams.Select(TeilnehmerAnzeige.AusMannschaft).ToList();
            }
            else
            {
                alleTeilnehmer = PlatzhalterGenerieren(aktuelleRunde!, vorherigeRunde);
            }

            var assignedMannschaftIds = detail.Lostoepfe
                .SelectMany(l => l.Eintraege)
                .Where(e => e.MannschaftId.HasValue)
                .Select(e => e.MannschaftId!.Value)
                .ToHashSet();
            var assignedPlatzhalter = detail.Lostoepfe
                .SelectMany(l => l.Eintraege)
                .Where(e => e.PlatzhalterName != null)
                .Select(e => e.PlatzhalterName!)
                .ToHashSet();

            VerfuegbareTeilnehmer = new ObservableCollection<TeilnehmerAnzeige>(
                alleTeilnehmer
                    .Where(t => t.MannschaftId.HasValue
                        ? !assignedMannschaftIds.Contains(t.MannschaftId.Value)
                        : !assignedPlatzhalter.Contains(t.Name))
                    .OrderBy(t => t.Name));

            Lostoepfe = new ObservableCollection<LostopfViewModel>(
                detail.Lostoepfe.Select(l => new LostopfViewModel
                {
                    Id         = l.Id,
                    Name       = l.Name,
                    Reihenfolge = l.Reihenfolge,
                    Teilnehmer = new ObservableCollection<TeilnehmerAnzeige>(
                        l.Eintraege
                            .Select(TeilnehmerAnzeige.AusEintrag)
                            .OrderBy(t => t.Name))
                }));

            Regeln = new ObservableCollection<RegelViewModel>(
                Enum.GetValues<AuslosungsRegelArt>().Select(art =>
                {
                    var vorh = detail.Regeln.FirstOrDefault(r => r.Regelart == art);
                    return new RegelViewModel(art, RegelAnzeigeName(art), vorh?.IstAktiv ?? false,
                        async aktiv =>
                        {
                            try
                            {
                                if (aktiv) await _auslosungService.RegelHinzufuegenOderAktivierenAsync(auslosungId, art, RegelAnzeigeName(art));
                                else       await _auslosungService.RegelDeaktivierenAsync(auslosungId, art);
                            }
                            catch (Exception ex) { Fehlermeldung = ex.Message; }
                        });
                }));

            // Detect whether results are group assignments (Gruppenphase) or match pairings
            bool istGruppe = detail.Ergebnisse.Any() &&
                detail.Ergebnisse.All(e => !e.IstFreilos &&
                    e.AuswaertsMannschaftId == null &&
                    e.AuswaertsPlatzhalterName == null);

            IstGruppenergebnis = istGruppe;

            if (istGruppe)
            {
                GruppenErgebnisse = new ObservableCollection<GruppenErgebnisAnzeige>(
                    detail.Ergebnisse
                        .GroupBy(e => e.PaarungsNummer)
                        .OrderBy(g => g.Key)
                        .Select(g => new GruppenErgebnisAnzeige(
                            g.Key,
                            $"Gruppe {GruppenBuchstabe(g.Key)}",
                            g.Select(e => e.HeimMannschaft?.Name ?? e.HeimPlatzhalterName ?? "?")
                             .OrderBy(n => n)
                             .ToList())));
                ErgebnisseAnzeige = [];
            }
            else
            {
                GruppenErgebnisse = [];
                ErgebnisseAnzeige = new ObservableCollection<ErgebnisAnzeige>(
                    detail.Ergebnisse.Select(e => new ErgebnisAnzeige(
                        e.PaarungsNummer,
                        e.HeimMannschaft?.Name ?? e.HeimPlatzhalterName ?? "?",
                        e.AuswaertsMannschaft?.Name ?? e.AuswaertsPlatzhalterName ?? "?",
                        e.IstFreilos)));
            }

            IstBearbeitbar        = detail.Status is not (AuslosungsStatus.Abgeschlossen or AuslosungsStatus.Abgebrochen);
            IstAbgeschlossen      = detail.Status == AuslosungsStatus.Abgeschlossen;
            DetailName            = detail.Name;
            DetailAuslosungsart   = detail.Auslosungsart;

            AuslosungKopfzeile = $"{detail.Name}  ·  {detail.TurnierRunde.Name}  ·  " +
                                 $"{AuslosungsartAnzeigeName(detail.Auslosungsart)}  ·  " +
                                 $"Status: {StatusAnzeigeName(detail.Status)}";
            DetailAktiv = true;
            Fehlermeldung = null;
        }
        finally { IstLadend = false; }
    }

    private static IReadOnlyList<TeilnehmerAnzeige> PlatzhalterGenerieren(
        TurnierRunde aktuelleRunde, TurnierRunde vorherigeRunde)
    {
        int anzahl = aktuelleRunde.EingangsMannschaftsanzahl;

        if (vorherigeRunde.Rundenart == Rundenart.Gruppenphase && vorherigeRunde.GruppenAnzahl.HasValue)
        {
            var gruppen  = vorherigeRunde.GruppenAnzahl.Value;
            var proGruppe = vorherigeRunde.AusgangsMannschaftsanzahl / gruppen;
            var liste = new List<TeilnehmerAnzeige>();
            for (int platz = 1; platz <= proGruppe; platz++)
            {
                for (int g = 0; g < gruppen; g++)
                {
                    var buchstabe = (char)('A' + g);
                    liste.Add(TeilnehmerAnzeige.AusPlatzhalter($"{platz}. Gruppe {buchstabe}"));
                }
            }
            return liste.Take(anzahl).ToList();
        }

        if (vorherigeRunde.Rundenart is Rundenart.KoRunde or Rundenart.Finale or Rundenart.SpielUmPlatzDrei)
        {
            var spiele = vorherigeRunde.EingangsMannschaftsanzahl / 2;
            return Enumerable.Range(1, Math.Min(spiele, anzahl))
                .Select(i => TeilnehmerAnzeige.AusPlatzhalter($"Sieger Spiel {i} ({vorherigeRunde.Name})"))
                .ToList();
        }

        return Enumerable.Range(1, anzahl)
            .Select(i => TeilnehmerAnzeige.AusPlatzhalter($"Platz {i} ({vorherigeRunde.Name})"))
            .ToList();
    }

    private async Task AktualisiereSeitenlisteAsync()
    {
        if (AusgewaehlteInstanz is null) return;
        var auslosungen = await _auslosungService.AlleNachInstanzAbrufenAsync(AusgewaehlteInstanz.Id);
        Auslosungen = new ObservableCollection<Auslosung>(auslosungen);
        if (_auslosungDetail is not null)
            AusgewaehlteAuslosung = Auslosungen.FirstOrDefault(a => a.Id == _auslosungDetail.Id);
    }

    partial void OnNeuRundeChanged(TurnierRunde? value)
    {
        if (value is null) return;
        if (string.IsNullOrWhiteSpace(NeuName) || NeuName == _autoNeuName)
        {
            _autoNeuName = value.Name;
            NeuName      = value.Name;
        }
    }

    private bool KannNeu()       => !NeuFormAktiv && AusgewaehlteInstanz is not null;
    private bool KannSpeichern() => NeuFormAktiv && !string.IsNullOrWhiteSpace(NeuName) && NeuRunde is not null;
    private bool KannAbbrechen() => NeuFormAktiv;
    private bool KannLoeschen()  => AusgewaehlteAuslosung is not null && !NeuFormAktiv;

    private static string GruppenBuchstabe(int nr) =>
        nr <= 26 ? ((char)('A' + nr - 1)).ToString() : $"G{nr}";

    // ── Anzeigetexte ──────────────────────────────────────────────────────────

    public static string AuslosungsartAnzeigeName(Auslosungsart a) => a switch
    {
        Auslosungsart.Zufall                          => "Zufällig",
        Auslosungsart.NachSetzliste                   => "Nach Setzliste",
        Auslosungsart.NachToepfen                     => "Nach Töpfen",
        Auslosungsart.TopfZuordnung                   => "Topf-Zuordnung (UCL)",
        Auslosungsart.GruppensiegerGegenGruppenzweite => "Gruppensieger vs. Zweite",
        Auslosungsart.Benutzerdefiniert               => "Benutzerdefiniert",
        _                                             => a.ToString()
    };

    private static string StatusAnzeigeName(AuslosungsStatus s) => s switch
    {
        AuslosungsStatus.Vorbereitung  => "Vorbereitung",
        AuslosungsStatus.Laufend       => "Laufend",
        AuslosungsStatus.Abgeschlossen => "Abgeschlossen",
        AuslosungsStatus.Abgebrochen   => "Abgebrochen",
        _                              => s.ToString()
    };

    private static string RegelAnzeigeName(AuslosungsRegelArt art) => art switch
    {
        AuslosungsRegelArt.KeinGleichesLand                => "Kein gleiches Land",
        AuslosungsRegelArt.KeineGleicheGruppe              => "Keine gleiche Gruppe",
        AuslosungsRegelArt.GruppensiegerGegenGruppenzweite => "Gruppensieger vs. Gruppenzweite",
        AuslosungsRegelArt.GesetzteGegenUngesetzte         => "Gesetzte vs. Ungesetzte",
        AuslosungsRegelArt.HeimrechtNachLos                => "Heimrecht nach Los",
        AuslosungsRegelArt.HeimrechtNachSetzliste          => "Heimrecht nach Setzliste",
        AuslosungsRegelArt.FreilosMoeglich                 => "Freilos möglich",
        AuslosungsRegelArt.ManuelleEinschraenkung          => "Manuelle Einschränkung",
        _                                                  => art.ToString()
    };
}

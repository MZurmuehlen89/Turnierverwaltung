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

// ── Hilfsklassen ────────────────────────────────────────────────────────────

public class LostopfViewModel : ObservableObject
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public int Reihenfolge { get; init; }
    public ObservableCollection<Mannschaft> Mannschaften { get; init; } = [];
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
    private IReadOnlyList<Mannschaft> _alleMannschaften = [];

    // ── Linke Spalte ──────────────────────────────────────────────────────────
    [ObservableProperty] private ObservableCollection<TurnierInstanz> _instanzen = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NeuCommand))]
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

    [ObservableProperty] private Auslosungsart _neuAuslosungsart = Auslosungsart.Zufall;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SpeichernCommand))]
    private TurnierRunde? _neuRunde;

    // ── Auslosung-Detail ──────────────────────────────────────────────────────
    [ObservableProperty] private bool _detailAktiv;
    [ObservableProperty] private bool _istBearbeitbar;
    [ObservableProperty] private bool _istAbgeschlossen;
    [ObservableProperty] private string _auslosungKopfzeile = "";

    [ObservableProperty] private ObservableCollection<Mannschaft> _verfuegbareMannschaften = [];
    [ObservableProperty] private ObservableCollection<LostopfViewModel> _lostoepfe = [];
    [ObservableProperty] private ObservableCollection<RegelViewModel> _regeln = [];
    [ObservableProperty] private ObservableCollection<AuslosungsErgebnis> _ergebnisse = [];
    [ObservableProperty] private string _neuerLostopfName = "";

    // ── Status ────────────────────────────────────────────────────────────────
    [ObservableProperty] private bool _istLadend;
    [ObservableProperty] private string? _fehlermeldung;

    // ── Berechnete Listen ─────────────────────────────────────────────────────
    public IReadOnlyList<AuslosungsartOption> AuslosungsartOptionen =>
        Enum.GetValues<Auslosungsart>()
            .Select(a => new AuslosungsartOption(AuslosungsartAnzeigeName(a), a))
            .ToList();

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
        _auslosungService = auslosungService;
        _instanzService   = instanzService;
        _mannschaftService = mannschaftService;
    }

    public override async Task AktivierenAsync()
    {
        IstLadend = true;
        try
        {
            _alleMannschaften = await _mannschaftService.AlleAbrufenMitLandAsync();
            var instanzen = await _instanzService.AlleAbrufenAsync();
            Instanzen = new ObservableCollection<TurnierInstanz>(instanzen);
        }
        finally { IstLadend = false; }
    }

    // ── Ribbon-Befehle ────────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(KannNeu))]
    private void Neu()
    {
        NeuFormAktiv    = true;
        DetailAktiv     = false;
        NeuName         = "";
        NeuAuslosungsart = Auslosungsart.Zufall;
        NeuRunde        = InstanzRunden.FirstOrDefault();
        Fehlermeldung   = null;
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
    private async Task AuslosungAusfuehren()
    {
        if (_auslosungDetail is null) return;
        Fehlermeldung = null;
        try
        {
            var ergebnisse = await _auslosungService.AusfuehrenAsync(_auslosungDetail.Id);
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

    // ── Öffentliche Methoden für D&D (aufgerufen vom Code-Behind) ─────────────

    public async Task MannschaftZuTopfVerschieben(int mannschaftId, int? vonTopfId, int nachTopfId)
    {
        if (_auslosungDetail is null) return;
        Fehlermeldung = null;
        try
        {
            if (vonTopfId.HasValue)
                await _auslosungService.MannschaftAusLostopfEntfernenAsync(_auslosungDetail.Id, vonTopfId.Value, mannschaftId);
            await _auslosungService.MannschaftZuLostopfHinzufuegenAsync(_auslosungDetail.Id, nachTopfId, mannschaftId);
            await LadeDetailAsync(_auslosungDetail.Id);
        }
        catch (Exception ex) { Fehlermeldung = ex.Message; }
    }

    public async Task MannschaftZuVerfuegbarZurueck(int mannschaftId, int vonTopfId)
    {
        if (_auslosungDetail is null) return;
        Fehlermeldung = null;
        try
        {
            await _auslosungService.MannschaftAusLostopfEntfernenAsync(_auslosungDetail.Id, vonTopfId, mannschaftId);
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

            var assignedIds = detail.Lostoepfe
                .SelectMany(l => l.Eintraege)
                .Select(e => e.MannschaftId)
                .ToHashSet();

            VerfuegbareMannschaften = new ObservableCollection<Mannschaft>(
                _alleMannschaften.Where(m => !assignedIds.Contains(m.Id)).OrderBy(m => m.Name));

            Lostoepfe = new ObservableCollection<LostopfViewModel>(
                detail.Lostoepfe.Select(l => new LostopfViewModel
                {
                    Id          = l.Id,
                    Name        = l.Name,
                    Reihenfolge = l.Reihenfolge,
                    Mannschaften = new ObservableCollection<Mannschaft>(
                        l.Eintraege
                            .Where(e => e.Mannschaft is not null)
                            .OrderBy(e => e.Mannschaft!.Name)
                            .Select(e => e.Mannschaft!))
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

            Ergebnisse       = new ObservableCollection<AuslosungsErgebnis>(detail.Ergebnisse);
            IstBearbeitbar   = detail.Status is not (AuslosungsStatus.Abgeschlossen or AuslosungsStatus.Abgebrochen);
            IstAbgeschlossen = detail.Status == AuslosungsStatus.Abgeschlossen;

            AuslosungKopfzeile = $"{detail.Name}  ·  {detail.TurnierRunde.Name}  ·  " +
                                 $"{AuslosungsartAnzeigeName(detail.Auslosungsart)}  ·  " +
                                 $"Status: {StatusAnzeigeName(detail.Status)}";
            DetailAktiv = true;
            Fehlermeldung = null;
        }
        finally { IstLadend = false; }
    }

    private async Task AktualisiereSeitenlisteAsync()
    {
        if (AusgewaehlteInstanz is null) return;
        var auslosungen = await _auslosungService.AlleNachInstanzAbrufenAsync(AusgewaehlteInstanz.Id);
        Auslosungen = new ObservableCollection<Auslosung>(auslosungen);
        if (_auslosungDetail is not null)
            AusgewaehlteAuslosung = Auslosungen.FirstOrDefault(a => a.Id == _auslosungDetail.Id);
    }

    private bool KannNeu()      => !NeuFormAktiv && AusgewaehlteInstanz is not null;
    private bool KannSpeichern() => NeuFormAktiv && !string.IsNullOrWhiteSpace(NeuName) && NeuRunde is not null;
    private bool KannAbbrechen() => NeuFormAktiv;
    private bool KannLoeschen()  => AusgewaehlteAuslosung is not null && !NeuFormAktiv;

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
        AuslosungsStatus.Vorbereitung => "Vorbereitung",
        AuslosungsStatus.Laufend      => "Laufend",
        AuslosungsStatus.Abgeschlossen => "Abgeschlossen",
        AuslosungsStatus.Abgebrochen  => "Abgebrochen",
        _                             => s.ToString()
    };

    private static string RegelAnzeigeName(AuslosungsRegelArt art) => art switch
    {
        AuslosungsRegelArt.KeinGleichesLand                  => "Kein gleiches Land",
        AuslosungsRegelArt.KeineGleicheGruppe                => "Keine gleiche Gruppe",
        AuslosungsRegelArt.GruppensiegerGegenGruppenzweite   => "Gruppensieger vs. Gruppenzweite",
        AuslosungsRegelArt.GesetzteGegenUngesetzte           => "Gesetzte vs. Ungesetzte",
        AuslosungsRegelArt.HeimrechtNachLos                  => "Heimrecht nach Los",
        AuslosungsRegelArt.HeimrechtNachSetzliste            => "Heimrecht nach Setzliste",
        AuslosungsRegelArt.FreilosMoeglich                   => "Freilos möglich",
        AuslosungsRegelArt.ManuelleEinschraenkung            => "Manuelle Einschränkung",
        _                                                    => art.ToString()
    };
}

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Application.ImportExport;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Wpf.Ribbon;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Input;

namespace Fussballturnierverwaltung.Wpf.ViewModels;

public partial class ImportExportViewModel : ViewModelBase, IRibbonAware
{
    private readonly ImportExportService   _service;
    private readonly TurnierInstanzService _instanzService;

    private static readonly string StandardExportPfad =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Turnierverwaltung", "Exports");

    // ── Turnier für Export ────────────────────────────────────────────────────

    [ObservableProperty] private ObservableCollection<TurnierInstanz> _instanzen = [];
    [ObservableProperty] private TurnierInstanz? _ausgewaehlteInstanz;

    // ── Status ────────────────────────────────────────────────────────────────

    [ObservableProperty] private bool    _istLadend;
    [ObservableProperty] private string? _fehlermeldung;
    [ObservableProperty] private string? _statusMeldung;
    [ObservableProperty] private string? _importDetail;

    // ── Zusatz-Befehle (Panel-Buttons) ────────────────────────────────────────

    public AsyncRelayCommand SpielplanExportierenBefehl    { get; }
    public AsyncRelayCommand TurnierJsonExportierenBefehl  { get; }

    // ── IRibbonAware ──────────────────────────────────────────────────────────

    public string   RibbonTabTitel  => "Import / Export";
    public ICommand NeuBefehl       => _neuBefehl;
    public ICommand SpeichernBefehl => _speichernBefehl;
    public ICommand AbbrechenBefehl => _abbrechenBefehl;
    public ICommand LoeschenBefehl  => _loeschenBefehl;

    private readonly AsyncRelayCommand _neuBefehl;
    private readonly AsyncRelayCommand _speichernBefehl;
    private readonly RelayCommand      _abbrechenBefehl;
    private readonly RelayCommand      _loeschenBefehl;

    // ── Konstruktor ───────────────────────────────────────────────────────────

    public ImportExportViewModel(ImportExportService service, TurnierInstanzService instanzService)
    {
        _service        = service;
        _instanzService = instanzService;

        _neuBefehl       = new AsyncRelayCommand(MannschaftenImportierenAsync, () => !IstLadend);
        _speichernBefehl = new AsyncRelayCommand(MannschaftenExportierenAsync,  () => !IstLadend);
        _abbrechenBefehl = new RelayCommand(() => { }, () => false);
        _loeschenBefehl  = new RelayCommand(() => { }, () => false);

        SpielplanExportierenBefehl   = new AsyncRelayCommand(SpielplanExportierenAsync,   () => AusgewaehlteInstanz != null && !IstLadend);
        TurnierJsonExportierenBefehl = new AsyncRelayCommand(TurnierJsonExportierenAsync,  () => AusgewaehlteInstanz != null && !IstLadend);
    }

    // ── Initialisierung ───────────────────────────────────────────────────────

    public override async Task AktivierenAsync()
    {
        var liste = await _instanzService.AlleAbrufenAsync();
        Instanzen.Clear();
        foreach (var i in liste)
            Instanzen.Add(i);
    }

    // ── Property-Reaktionen ───────────────────────────────────────────────────

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (e.PropertyName is nameof(AusgewaehlteInstanz) or nameof(IstLadend))
        {
            SpielplanExportierenBefehl.NotifyCanExecuteChanged();
            TurnierJsonExportierenBefehl.NotifyCanExecuteChanged();
            _neuBefehl.NotifyCanExecuteChanged();
            _speichernBefehl.NotifyCanExecuteChanged();
        }
    }

    // ── Import ────────────────────────────────────────────────────────────────

    private async Task MannschaftenImportierenAsync()
    {
        var dlg = new OpenFileDialog
        {
            Title  = "CSV-Datei für Mannschaften-Import",
            Filter = "CSV-Dateien (*.csv)|*.csv|Alle Dateien (*.*)|*.*"
        };
        if (dlg.ShowDialog() != true) return;

        ResetStatus();
        IstLadend = true;
        try
        {
            var ergebnis = await _service.MannschaftenImportierenAsync(dlg.FileName);

            if (ergebnis.Neu > 0 || ergebnis.Aktualisiert > 0)
            {
                var teile = new List<string>();
                if (ergebnis.Neu          > 0) teile.Add($"{ergebnis.Neu} neu");
                if (ergebnis.Aktualisiert > 0) teile.Add($"{ergebnis.Aktualisiert} aktualisiert");
                StatusMeldung = $"Mannschaft(en) importiert: {string.Join(", ", teile)}.";
            }

            if (ergebnis.Fehler > 0)
            {
                var details = string.Join("\n", ergebnis.FehlerMeldungen.Take(10));
                var rest    = ergebnis.FehlerMeldungen.Count > 10
                    ? $"\n… und {ergebnis.FehlerMeldungen.Count - 10} weitere."
                    : "";
                ImportDetail  = details + rest;
                Fehlermeldung = $"{ergebnis.Fehler} Zeile(n) mit Fehlern.";
            }

            if (ergebnis.Neu == 0 && ergebnis.Aktualisiert == 0 && ergebnis.Fehler == 0)
                StatusMeldung = "Keine Daten in der Datei gefunden.";
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
        finally
        {
            IstLadend = false;
        }
    }

    // ── Export – Mannschaften ─────────────────────────────────────────────────

    private async Task MannschaftenExportierenAsync()
    {
        Directory.CreateDirectory(StandardExportPfad);
        var dlg = new SaveFileDialog
        {
            Title            = "Mannschaften als CSV exportieren",
            Filter           = "CSV-Dateien (*.csv)|*.csv",
            FileName         = "Mannschaften.csv",
            InitialDirectory = StandardExportPfad
        };
        if (dlg.ShowDialog() != true) return;

        ResetStatus();
        IstLadend = true;
        try
        {
            int anzahl = await _service.MannschaftenExportierenCsvAsync(dlg.FileName);
            StatusMeldung = $"{anzahl} Mannschaft(en) exportiert → {dlg.FileName}";
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
        finally
        {
            IstLadend = false;
        }
    }

    // ── Export – Spielplan CSV ────────────────────────────────────────────────

    private async Task SpielplanExportierenAsync()
    {
        if (AusgewaehlteInstanz is null) return;

        Directory.CreateDirectory(StandardExportPfad);
        var vorschlag = Sicher($"Spielplan_{AusgewaehlteInstanz.Name}_{AusgewaehlteInstanz.SaisonOderJahr}.csv");
        var dlg = new SaveFileDialog
        {
            Title            = "Spielplan als CSV exportieren",
            Filter           = "CSV-Dateien (*.csv)|*.csv",
            FileName         = vorschlag,
            InitialDirectory = StandardExportPfad
        };
        if (dlg.ShowDialog() != true) return;

        ResetStatus();
        IstLadend = true;
        try
        {
            int anzahl = await _service.SpielplanExportierenCsvAsync(AusgewaehlteInstanz.Id, dlg.FileName);
            StatusMeldung = $"{anzahl} Spiel(e) exportiert → {dlg.FileName}";
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
        finally
        {
            IstLadend = false;
        }
    }

    // ── Export – Turnier JSON ─────────────────────────────────────────────────

    private async Task TurnierJsonExportierenAsync()
    {
        if (AusgewaehlteInstanz is null) return;

        Directory.CreateDirectory(StandardExportPfad);
        var vorschlag = Sicher($"Turnier_{AusgewaehlteInstanz.Name}_{AusgewaehlteInstanz.SaisonOderJahr}.json");
        var dlg = new SaveFileDialog
        {
            Title            = "Turnierdaten als JSON exportieren",
            Filter           = "JSON-Dateien (*.json)|*.json",
            FileName         = vorschlag,
            InitialDirectory = StandardExportPfad
        };
        if (dlg.ShowDialog() != true) return;

        ResetStatus();
        IstLadend = true;
        try
        {
            await _service.TurnierExportierenJsonAsync(AusgewaehlteInstanz.Id, dlg.FileName);
            StatusMeldung = $"Turnierdaten exportiert → {dlg.FileName}";
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
        finally
        {
            IstLadend = false;
        }
    }

    // ── Hilfsmethoden ─────────────────────────────────────────────────────────

    private void ResetStatus()
    {
        Fehlermeldung = null;
        StatusMeldung = null;
        ImportDetail  = null;
    }

    private static string Sicher(string name)
        => string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
}

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Application.ImportExport;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.WinUi.Dialoge;
using Fussballturnierverwaltung.WinUi.Ribbon;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Input;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>
/// #290, 1:1-Port aus Wpf/ViewModels/ImportExportViewModel.cs — letzter Menüpunkt der WinUI3-Migration.
/// WPFs Microsoft.Win32.OpenFileDialog/SaveFileDialog ersetzt durch IDialogService.CsvDateiOeffnenAsync/
/// DateiSpeichernUnterAsync (FileOpenPicker/FileSavePicker brauchen unter WinUI3 Desktop ein Fenster-Handle,
/// das das ViewModel nicht kennt — Precedent: BilddateiWaehlenAsync).
/// </summary>
public partial class ImportExportViewModel : ViewModelBase, IRibbonAware
{
    private readonly ImportExportService   _service;
    private readonly TurnierInstanzService _instanzService;
    private readonly IDialogService        _dialogService;

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

    public AsyncRelayCommand SpielplanExportierenBefehl   { get; }
    public AsyncRelayCommand TurnierJsonExportierenBefehl { get; }

    // ── IRibbonAware ──────────────────────────────────────────────────────────

    // Ribbon-Standardaktionen (Neu/Speichern/Abbrechen) bewusst komplett ausgeblendet: die Seite hat für
    // Import/Mannschaften-Export/Spielplan-Export/Turnier-Export bereits eigene Buttons im Formular selbst,
    // ein Ribbon-Duplikat ist überflüssig (Nutzer-Feedback #290-Nachzug).
    public string    RibbonTabTitel  => "Import / Export";
    public ICommand   NeuBefehl       => _neuBefehl;
    public ICommand   SpeichernBefehl => _speichernBefehl;
    public ICommand?  AbbrechenBefehl => null;
    public ICommand?  LoeschenBefehl  => null;
    public bool       HatStandardAktionen => false;
    public bool       HatLoeschenAktion   => false;

    private readonly AsyncRelayCommand _neuBefehl;
    private readonly AsyncRelayCommand _speichernBefehl;

    // ── Konstruktor ───────────────────────────────────────────────────────────

    public ImportExportViewModel(ImportExportService service, TurnierInstanzService instanzService, IDialogService dialogService)
    {
        _service        = service;
        _instanzService = instanzService;
        _dialogService  = dialogService;

        _neuBefehl       = new AsyncRelayCommand(MannschaftenImportierenAsync, () => !IstLadend);
        _speichernBefehl = new AsyncRelayCommand(MannschaftenExportierenAsync,  () => !IstLadend);

        SpielplanExportierenBefehl   = new AsyncRelayCommand(SpielplanExportierenAsync,   () => AusgewaehlteInstanz != null && !IstLadend);
        TurnierJsonExportierenBefehl = new AsyncRelayCommand(TurnierJsonExportierenAsync,  () => AusgewaehlteInstanz != null && !IstLadend);
    }

    // ── Initialisierung ───────────────────────────────────────────────────────

    public override async Task AktivierenAsync()
    {
        // Nur tatsächliche Turniere zum Export anbieten, keine Turniervorlagen (TurnierStatus.Vorlage) —
        // AlleAbrufenAsync liefert seit der Vorlage/Instanz-Vereinheitlichung (#294/#297) beide in derselben
        // Tabelle zurück.
        var liste = await _instanzService.AlleAbrufenAsync();
        Instanzen.Clear();
        foreach (var i in liste.Where(i => i.Status != TurnierStatus.Vorlage))
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
        var pfad = await _dialogService.CsvDateiOeffnenAsync();
        if (pfad is null) return;

        ResetStatus();
        IstLadend = true;
        try
        {
            var ergebnis = await _service.MannschaftenImportierenAsync(pfad);

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
        var pfad = await _dialogService.DateiSpeichernUnterAsync("Mannschaften.csv", "csv", "CSV-Datei");
        if (pfad is null) return;

        ResetStatus();
        IstLadend = true;
        try
        {
            int anzahl = await _service.MannschaftenExportierenCsvAsync(pfad);
            StatusMeldung = $"{anzahl} Mannschaft(en) exportiert → {pfad}";
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
        var pfad = await _dialogService.DateiSpeichernUnterAsync(vorschlag, "csv", "CSV-Datei");
        if (pfad is null) return;

        ResetStatus();
        IstLadend = true;
        try
        {
            int anzahl = await _service.SpielplanExportierenCsvAsync(AusgewaehlteInstanz.Id, pfad);
            StatusMeldung = $"{anzahl} Spiel(e) exportiert → {pfad}";
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
        var pfad = await _dialogService.DateiSpeichernUnterAsync(vorschlag, "json", "JSON-Datei");
        if (pfad is null) return;

        ResetStatus();
        IstLadend = true;
        try
        {
            await _service.TurnierExportierenJsonAsync(AusgewaehlteInstanz.Id, pfad);
            StatusMeldung = $"Turnierdaten exportiert → {pfad}";
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

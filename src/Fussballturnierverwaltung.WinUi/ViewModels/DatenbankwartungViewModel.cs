using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Application.Einstellungen;
using Fussballturnierverwaltung.Infrastructure.Wartung;
using Fussballturnierverwaltung.WinUi.Helpers;
using Fussballturnierverwaltung.WinUi.Ribbon;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Issue #370: Seite "Datenbankwartung" — zeigt den Migrationsstand und führt per Hand denselben
/// Ablauf wie beim App-Start aus (Migrationen einspielen + fehlende Stammdaten ergänzen). Ergebnis als
/// Protokoll auf der Seite statt Dialog/Erfolgsmeldung. Ort für das spätere Zurücksetzen (#371).</summary>
public partial class DatenbankwartungViewModel : ViewModelBase, IRibbonAware
{
    private readonly DatenbankWartungService _wartung;
    private readonly Ablaufprotokoll _ablaufprotokoll;

    // ── Status ────────────────────────────────────────────────────────────────

    [ObservableProperty] private string? _server;
    [ObservableProperty] private string? _datenbank;
    [ObservableProperty] private string? _migrationsStand;
    [ObservableProperty] private bool    _istLadend;
    [ObservableProperty] private string? _fehlermeldung;

    // ── Protokoll der letzten Prüfung ─────────────────────────────────────────

    [ObservableProperty] private string? _letztePruefung;
    public ObservableCollection<string> ProtokollZeilen { get; } = [];

    public AsyncRelayCommand PruefenBefehl { get; }

    // ── Ablaufprotokoll (#413) ────────────────────────────────────────────────

    /// <summary>Schreibt Nutzeraktionen, Wechsel und Ladevorgänge mit Dauer ins Log; wirkt sofort.</summary>
    public bool AblaufprotokollAktiv
    {
        get => _ablaufprotokoll.IstAktiv;
        set
        {
            if (value == _ablaufprotokoll.IstAktiv) return;
            _ablaufprotokoll.IstAktiv = value;
            OnPropertyChanged();
        }
    }

    public string LogOrdner { get; }

    // ── IRibbonAware ──────────────────────────────────────────────────────────

    public string   RibbonTabTitel      => "Datenbankwartung";
    public ICommand? NeuBefehl          => null;
    public ICommand? SpeichernBefehl    => null;
    public ICommand? AbbrechenBefehl    => null;
    public ICommand? LoeschenBefehl     => null;
    public bool     HatStandardAktionen => false;
    public bool     HatLoeschenAktion   => false;

    public IReadOnlyList<RibbonGruppe> RibbonZusatzGruppen =>
    [
        new RibbonGruppe
        {
            Header = "Datenbank",
            Aktionen =
            [
                new RibbonAktion { Header = "Prüfen / ergänzen", Befehl = PruefenBefehl, IconGlyph = "" },
            ]
        }
    ];

    public DatenbankwartungViewModel(
        DatenbankWartungService wartung, Ablaufprotokoll ablaufprotokoll, EinstellungenService einstellungenService)
    {
        _wartung = wartung;
        _ablaufprotokoll = ablaufprotokoll;
        LogOrdner = einstellungenService.LogPfad;
        PruefenBefehl = new AsyncRelayCommand(PruefenUndErgaenzenAsync, () => !IstLadend);
    }

    public override async Task AktivierenAsync() => await StatusLadenAsync();

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName == nameof(IstLadend))
            PruefenBefehl.NotifyCanExecuteChanged();
    }

    private async Task StatusLadenAsync()
    {
        try
        {
            var status = await _wartung.StatusAbrufenAsync();
            Server    = status.Server;
            Datenbank = status.Datenbank;
            MigrationsStand = status.AusstehendeMigrationen.Count == 0
                ? $"{status.AnzahlEingespielteMigrationen} eingespielt, keine ausstehend"
                : $"{status.AnzahlEingespielteMigrationen} eingespielt, {status.AusstehendeMigrationen.Count} ausstehend";
        }
        catch (Exception ex)
        {
            Fehlermeldung = $"Datenbankstatus konnte nicht ermittelt werden: {ex.Message}";
        }
    }

    private async Task PruefenUndErgaenzenAsync()
    {
        Fehlermeldung = null;
        IstLadend = true;
        try
        {
            var ergebnis = await _wartung.PruefenUndErgaenzenAsync();

            ProtokollZeilen.Clear();
            if (ergebnis.EingespielteMigrationen.Count > 0)
                ProtokollZeilen.Add($"{ergebnis.EingespielteMigrationen.Count} Migration(en) eingespielt: "
                    + string.Join(", ", ergebnis.EingespielteMigrationen));
            foreach (var eintrag in ergebnis.StammdatenErgaenzungen)
                ProtokollZeilen.Add($"{eintrag.Anzahl} {eintrag.Text}");
            if (ProtokollZeilen.Count == 0)
                ProtokollZeilen.Add("Datenbank und Stammdaten sind vollständig — nichts ergänzt.");

            LetztePruefung = $"Letzte Prüfung: {DateTime.Now:dd.MM.yyyy HH:mm:ss}";
        }
        catch (Exception ex)
        {
            Fehlermeldung = $"Prüfen/Ergänzen fehlgeschlagen: {ex.Message}";
        }
        finally
        {
            IstLadend = false;
        }

        await StatusLadenAsync();
    }
}

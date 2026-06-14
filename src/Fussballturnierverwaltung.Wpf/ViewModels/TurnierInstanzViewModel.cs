using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Application.Turniere.Eingaben;
using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Wpf.Converters;
using Fussballturnierverwaltung.Wpf.Ribbon;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace Fussballturnierverwaltung.Wpf.ViewModels;

public record TurnierStatusOption(string Anzeige, TurnierStatus? Wert);
public record TurnierVorlageOption(string Anzeige, TurnierVorlage? Wert);
public record RegelwerkOption(string Anzeige, Regelwerk? Wert);

public partial class RundenEintrag : ObservableObject
{
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(Anzeige))] private string _name = RundenartZuTextConverter.RundenartAnzeigeName(Rundenart.Liga);
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(Anzeige))] private Rundenart _rundenart = Rundenart.Liga;

    partial void OnRundenartChanged(Rundenart oldValue, Rundenart newValue)
    {
        if (Name == RundenartZuTextConverter.RundenartAnzeigeName(oldValue))
            Name = RundenartZuTextConverter.RundenartAnzeigeName(newValue);
    }
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(Anzeige))] private int _eingangsMannschaften = 4;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(Anzeige))] private int _ausgangsMannschaften = 4;
    [ObservableProperty] private int _regelwerkId;
    [ObservableProperty] private string _regelwerkName = "";
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(Anzeige))] private bool _mitRueckspiel;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(Anzeige))] private bool _auswaertstorRegelAktiv;

    public string Anzeige =>
        $"{Name} · {RundenartZuTextConverter.RundenartAnzeigeName(Rundenart)} · " +
        $"{EingangsMannschaften}→{AusgangsMannschaften}" +
        (MitRueckspiel ? " · H+R" : "") +
        (AuswaertstorRegelAktiv ? " · Auswärtstor" : "");
}

public partial class TurnierInstanzViewModel : ViewModelBase, IRibbonAware
{
    private readonly TurnierInstanzService _service;
    private int _populiertGerade;

    private static readonly TurnierStatusOption _alleStatus = new("— Alle —", null);

    // ── Liste ──────────────────────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InstanzenGefiltert))]
    private ObservableCollection<TurnierInstanz> _instanzen = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InstanzenGefiltert))]
    private string _suchbegriff = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InstanzenGefiltert))]
    private TurnierStatusOption _statusFilter = _alleStatus;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoeschenCommand))]
    [NotifyPropertyChangedFor(nameof(RundenSichtbar))]
    [NotifyPropertyChangedFor(nameof(StatusAnzeige))]
    private TurnierInstanz? _ausgewaehltes;

    // ── Formular-Zustand ───────────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SpeichernCommand))]
    [NotifyCanExecuteChangedFor(nameof(AbbrechenCommand))]
    [NotifyCanExecuteChangedFor(nameof(LoeschenCommand))]
    [NotifyPropertyChangedFor(nameof(RundenSichtbar))]
    private bool _formularAktiv;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SpeichernCommand))]
    [NotifyCanExecuteChangedFor(nameof(AbbrechenCommand))]
    private bool _isDirty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoeschenCommand))]
    [NotifyPropertyChangedFor(nameof(RundenSichtbar))]
    private bool _istNeuModus;

    [ObservableProperty] private bool _istLadend;
    [ObservableProperty] private string? _fehlermeldung;

    // ── Basisdaten ────────────────────────────────────────────────────────────
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _saisonOderJahr = "";
    [ObservableProperty] private string _startdatum = "";
    [ObservableProperty] private string _enddatum = "";

    // ── Berechnete Eigenschaften ──────────────────────────────────────────────
    public bool RundenSichtbar => FormularAktiv && !IstNeuModus;

    public string StatusAnzeige => Ausgewaehltes is not null
        ? TurnierStatusZuTextConverter.StatusAnzeigeName(Ausgewaehltes.Status)
        : "";

    public IEnumerable<TurnierInstanz> InstanzenGefiltert => Instanzen
        .Where(i =>
            (string.IsNullOrWhiteSpace(Suchbegriff)
                || i.Name.Contains(Suchbegriff, StringComparison.OrdinalIgnoreCase)
                || i.SaisonOderJahr.Contains(Suchbegriff, StringComparison.OrdinalIgnoreCase))
            && (StatusFilter.Wert is null || i.Status == StatusFilter.Wert));

    public IReadOnlyList<TurnierStatusOption> StatusFilterOptionen =>
        [_alleStatus, .. Enum.GetValues<TurnierStatus>()
            .Select(s => new TurnierStatusOption(TurnierStatusZuTextConverter.StatusAnzeigeName(s), s))];

    private static readonly HashSet<string> _formFelder =
        [nameof(Name), nameof(SaisonOderJahr), nameof(Startdatum), nameof(Enddatum)];

    // ── IRibbonAware ───────────────────────────────────────────────────────────
    public string RibbonTabTitel => "Turniere";
    public ICommand NeuBefehl => NeuCommand;
    public ICommand SpeichernBefehl => SpeichernCommand;
    public ICommand AbbrechenBefehl => AbbrechenCommand;
    public ICommand LoeschenBefehl => LoeschenCommand;

    public TurnierInstanzViewModel(TurnierInstanzService service)
    {
        _service = service;
    }

    public override async Task AktivierenAsync() => await LadenAsync();

    private async Task LadenAsync()
    {
        IstLadend = true;
        try
        {
            var instanzen = await _service.AlleAbrufenAsync();
            Instanzen = new ObservableCollection<TurnierInstanz>(instanzen);
        }
        finally
        {
            IstLadend = false;
        }
    }

    private void FormularAusFuellenMit(TurnierInstanz? instanz)
    {
        _populiertGerade++;
        try
        {
            if (instanz is null)
            {
                FormularAktiv         = false;
                IstNeuModus           = false;
                Name = SaisonOderJahr = Startdatum = Enddatum = "";
            }
            else
            {
                FormularAktiv  = true;
                IstNeuModus    = false;
                Name           = instanz.Name;
                SaisonOderJahr = instanz.SaisonOderJahr;
                Startdatum     = instanz.Startdatum?.Year.ToString() ?? "";
                Enddatum       = instanz.Enddatum?.Year.ToString() ?? "";
            }
            IsDirty = false;
            Fehlermeldung = null;
        }
        finally
        {
            _populiertGerade--;
        }
    }

    // ── Ribbon-Befehle ────────────────────────────────────────────────────────

    [RelayCommand]
    private void Neu()
    {
        _populiertGerade++;
        try
        {
            Ausgewaehltes         = null;
            FormularAktiv         = true;
            IstNeuModus           = true;
            Name = SaisonOderJahr = Startdatum = Enddatum = "";
            Fehlermeldung = null;
        }
        finally
        {
            _populiertGerade--;
        }
        IsDirty = true;
    }

    [RelayCommand(CanExecute = nameof(KannSpeichern))]
    private async Task Speichern()
    {
        Fehlermeldung = null;
        try
        {
            var startdatum = ParseJahr(Startdatum);
            var enddatum   = ParseJahr(Enddatum);

            if (IstNeuModus)
            {
                var eingabe = new TurnierInstanzErstellenEingabe(
                    Name, SaisonOderJahr, null, startdatum, enddatum);
                var neu = await _service.ErstellenAsync(eingabe);
                await LadenAsync();
                Ausgewaehltes = Instanzen.FirstOrDefault(i => i.Id == neu.Id);
            }
            else if (Ausgewaehltes is not null)
            {
                var id = Ausgewaehltes.Id;
                var eingabe = new TurnierInstanzVollAktualisierenEingabe(
                    Name, SaisonOderJahr, null, startdatum, enddatum, Ausgewaehltes.Status);
                await _service.VollAktualisierenAsync(id, eingabe);
                await LadenAsync();
                Ausgewaehltes = Instanzen.FirstOrDefault(i => i.Id == id);
            }
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
    }

    [RelayCommand(CanExecute = nameof(KannAbbrechen))]
    private void Abbrechen()
    {
        if (IstNeuModus)
            FormularAusFuellenMit(null);
        else
            _ = BeiAuswaehlungsAenderungAsync();
    }

    [RelayCommand(CanExecute = nameof(KannLoeschen))]
    private async Task Loeschen()
    {
        if (Ausgewaehltes is null) return;
        var ergebnis = MessageBox.Show(
            $"Soll '{Ausgewaehltes.Name}' wirklich gelöscht werden?\n\nDabei werden alle Spiele, Auslosungen und Simulationsläufe mitgelöscht.",
            "Turnier löschen", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (ergebnis != MessageBoxResult.Yes) return;
        Fehlermeldung = null;
        try
        {
            var id = Ausgewaehltes.Id;
            await _service.LoeschenAsync(id);
            await LadenAsync();
            Ausgewaehltes = null;
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName == nameof(Ausgewaehltes))
            _ = BeiAuswaehlungsAenderungAsync();
        if (_populiertGerade == 0 && _formFelder.Contains(e.PropertyName!))
            IsDirty = true;
    }

    private async Task BeiAuswaehlungsAenderungAsync()
    {
        if (Ausgewaehltes is null)
        {
            FormularAusFuellenMit(null);
            return;
        }
        IstLadend = true;
        try
        {
            var detail = await _service.FindenNachIdMitRundenAsync(Ausgewaehltes.Id);
            FormularAusFuellenMit(detail);
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

    private bool KannSpeichern() => FormularAktiv && IsDirty;
    private bool KannAbbrechen() => FormularAktiv && IsDirty;
    private bool KannLoeschen()  => Ausgewaehltes is not null && !IstNeuModus;

    private static DateOnly? ParseJahr(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        return int.TryParse(text.Trim(), out var jahr) && jahr >= 1800 && jahr <= 2100
            ? new DateOnly(jahr, 1, 1)
            : null;
    }
}

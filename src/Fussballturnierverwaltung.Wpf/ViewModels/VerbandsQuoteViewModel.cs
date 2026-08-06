using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Application.Turniere.Eingaben;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Wpf.Navigation;
using Fussballturnierverwaltung.Wpf.Ribbon;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace Fussballturnierverwaltung.Wpf.ViewModels;

public partial class VerbandsQuoteViewModel : ViewModelBase, IRibbonAware, IHatUngespeicherteAenderungen
{
    private readonly TurnierInstanzService _instanzService;
    private readonly VerbandService _verbandService;
    private readonly VerbandsQuoteService _quoteService;
    private readonly AusgewaehlteTurnierInstanzService _turnierAuswahlService;
    private int _populiertGerade;

    // ── Turnier-Auswahl (bestimmt den Kontext für die Quoten-Liste) ───────────
    [ObservableProperty]
    private ObservableCollection<TurnierInstanz> _turniere = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TurnierGewaehlt))]
    [NotifyCanExecuteChangedFor(nameof(NeuCommand))]
    private TurnierInstanz? _ausgewaehltesTurnier;

    public bool TurnierGewaehlt => AusgewaehltesTurnier is not null;

    // ── Liste der Quoten des gewählten Turniers ───────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VerbandOptionen))]
    private ObservableCollection<VerbandsQuote> _quoten = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoeschenCommand))]
    [NotifyPropertyChangedFor(nameof(VerbandOptionen))]
    private VerbandsQuote? _ausgewaehltes;

    // ── Alle Verbände (Stammdaten, Quelle für die Verband-Auswahl) ────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VerbandOptionen))]
    private ObservableCollection<Verband> _verbaende = [];

    // ── Formular-Zustand ───────────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SpeichernCommand))]
    [NotifyCanExecuteChangedFor(nameof(AbbrechenCommand))]
    [NotifyCanExecuteChangedFor(nameof(LoeschenCommand))]
    private bool _formularAktiv;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SpeichernCommand))]
    [NotifyCanExecuteChangedFor(nameof(AbbrechenCommand))]
    private bool _isDirty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoeschenCommand))]
    private bool _istNeuModus;

    [ObservableProperty] private bool _istLadend;
    [ObservableProperty] private string? _fehlermeldung;

    // ── Formularfelder ─────────────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VerbandOptionen))]
    private Verband? _ausgewaehlterVerband;

    [ObservableProperty] private string _maxAnzahl = "1";
    [ObservableProperty] private string _minAnzahl = "";

    // Verbände, die in diesem Turnier noch keine Quote haben — der aktuell im Formular
    // ausgewählte Verband bleibt in der Liste, damit die ComboBox-Auswahl beim Bearbeiten
    // nicht verloren geht (analog ZielRundenOptionen in UebergangsregelViewModel).
    public IEnumerable<Verband> VerbandOptionen =>
        Verbaende.Where(v => v.Id == AusgewaehlterVerband?.Id ||
                             Quoten.Where(q => Ausgewaehltes is null || q.Id != Ausgewaehltes.Id)
                                   .All(q => q.VerbandId != v.Id));

    private static readonly HashSet<string> _formFelder =
        [nameof(AusgewaehlterVerband), nameof(MaxAnzahl), nameof(MinAnzahl)];

    // ── IRibbonAware ───────────────────────────────────────────────────────────
    public string RibbonTabTitel => "Verband-Quoten";
    public ICommand NeuBefehl => NeuCommand;
    public ICommand SpeichernBefehl => SpeichernCommand;
    public ICommand AbbrechenBefehl => AbbrechenCommand;
    public ICommand LoeschenBefehl => LoeschenCommand;

    public VerbandsQuoteViewModel(
        TurnierInstanzService instanzService,
        VerbandService verbandService,
        VerbandsQuoteService quoteService,
        AusgewaehlteTurnierInstanzService turnierAuswahlService)
    {
        _instanzService = instanzService;
        _verbandService = verbandService;
        _quoteService   = quoteService;
        _turnierAuswahlService = turnierAuswahlService;
    }

    public override async Task AktivierenAsync() => await LadenAsync();

    private async Task LadenAsync()
    {
        IstLadend = true;
        try
        {
            var instanzen = await _instanzService.AlleAbrufenAsync();
            Turniere = new ObservableCollection<TurnierInstanz>(
                instanzen.OrderByDescending(i => i.SaisonOderJahr).ThenBy(i => i.Name));

            var verbaende = await _verbandService.AlleAbrufenAsync();
            Verbaende = new ObservableCollection<Verband>(verbaende.OrderBy(v => v.Name));

            AusgewaehltesTurnier = Turniere.FirstOrDefault(
                i => i.Id == _turnierAuswahlService.TurnierInstanzId);
        }
        finally
        {
            IstLadend = false;
        }
    }

    partial void OnAusgewaehltesTurnierChanged(TurnierInstanz? value)
    {
        _turnierAuswahlService.TurnierInstanzId = value?.Id;
        _ = QuotenFuerTurnierLadenAsync(value);
    }

    private async Task QuotenFuerTurnierLadenAsync(TurnierInstanz? turnier)
    {
        if (turnier is null)
        {
            Quoten = [];
            FormularAusFuellenMit(null);
            return;
        }

        IstLadend = true;
        try
        {
            var quoten = await _quoteService.AlleNachTurnierInstanzIdAsync(turnier.Id);
            Quoten = new ObservableCollection<VerbandsQuote>(quoten.OrderBy(q => q.Verband.Name));
            Ausgewaehltes = null;
            FormularAusFuellenMit(null);
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

    private void FormularAusFuellenMit(VerbandsQuote? quote)
    {
        _populiertGerade++;
        try
        {
            if (quote is null)
            {
                FormularAktiv = false;
                IstNeuModus = false;
                AusgewaehlterVerband = null;
                MaxAnzahl = "1";
                MinAnzahl = "";
            }
            else
            {
                FormularAktiv = true;
                IstNeuModus = false;
                AusgewaehlterVerband = Verbaende.FirstOrDefault(v => v.Id == quote.VerbandId);
                MaxAnzahl = quote.MaxAnzahl.ToString();
                MinAnzahl = quote.MinAnzahl?.ToString() ?? "";
            }
            IsDirty = false;
            Fehlermeldung = null;
        }
        finally
        {
            _populiertGerade--;
        }
    }

    // ── Ribbon-Befehle ─────────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(KannNeu))]
    private void Neu()
    {
        _populiertGerade++;
        try
        {
            Ausgewaehltes = null;
            FormularAktiv = true;
            IstNeuModus = true;
            AusgewaehlterVerband = null;
            MaxAnzahl = "1";
            MinAnzahl = "";
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
        if (AusgewaehltesTurnier is null) return;
        Fehlermeldung = null;

        if (AusgewaehlterVerband is null)
        {
            Fehlermeldung = "Bitte einen Verband auswählen.";
            return;
        }

        try
        {
            var eingabe = new VerbandsQuoteEingabe(
                AusgewaehltesTurnier.Id, AusgewaehlterVerband.Id,
                ParseInt(MaxAnzahl), ParseNullableInt(MinAnzahl));

            int gespeicherteId;
            if (IstNeuModus)
            {
                var neu = await _quoteService.ErstellenAsync(eingabe);
                gespeicherteId = neu.Id;
            }
            else if (Ausgewaehltes is not null)
            {
                gespeicherteId = Ausgewaehltes.Id;
                await _quoteService.AktualisierenAsync(gespeicherteId, eingabe);
            }
            else
            {
                return;
            }

            var quoten = await _quoteService.AlleNachTurnierInstanzIdAsync(AusgewaehltesTurnier.Id);
            Quoten = new ObservableCollection<VerbandsQuote>(quoten.OrderBy(q => q.Verband.Name));

            var gespeichert = Quoten.FirstOrDefault(q => q.Id == gespeicherteId);
            // Explizit statt nur über die Ausgewaehltes-Zuweisung, siehe UebergangsregelViewModel.
            Ausgewaehltes = gespeichert;
            FormularAusFuellenMit(gespeichert);
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
    }

    [RelayCommand(CanExecute = nameof(KannAbbrechen))]
    private void Abbrechen() => FormularAusFuellenMit(IstNeuModus ? null : Ausgewaehltes);

    [RelayCommand(CanExecute = nameof(KannLoeschen))]
    private async Task Loeschen()
    {
        if (Ausgewaehltes is null || AusgewaehltesTurnier is null) return;
        var ergebnis = MessageBox.Show(
            $"Soll die Quote für Verband '{Ausgewaehltes.Verband.Name}' wirklich gelöscht werden?",
            "Verband-Quote löschen", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (ergebnis != MessageBoxResult.Yes) return;

        Fehlermeldung = null;
        try
        {
            await _quoteService.LoeschenAsync(Ausgewaehltes.Id);
            var quoten = await _quoteService.AlleNachTurnierInstanzIdAsync(AusgewaehltesTurnier.Id);
            Quoten = new ObservableCollection<VerbandsQuote>(quoten.OrderBy(q => q.Verband.Name));
            Ausgewaehltes = null;
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName == nameof(Ausgewaehltes))
            FormularAusFuellenMit(Ausgewaehltes);
        if (_populiertGerade == 0 && e.PropertyName is not null && _formFelder.Contains(e.PropertyName))
            IsDirty = true;
    }

    private bool KannNeu() => TurnierGewaehlt;
    private bool KannSpeichern() => FormularAktiv && IsDirty;
    private bool KannAbbrechen() => FormularAktiv && IsDirty;
    private bool KannLoeschen() => Ausgewaehltes is not null && !IstNeuModus;

    public bool HatUngespeicherteAenderungen => FormularAktiv && IsDirty;

    private static int ParseInt(string text) => int.TryParse(text.Trim(), out var n) ? n : 1;

    private static int? ParseNullableInt(string text)
        => string.IsNullOrWhiteSpace(text) ? null : int.TryParse(text.Trim(), out var n) ? n : null;
}

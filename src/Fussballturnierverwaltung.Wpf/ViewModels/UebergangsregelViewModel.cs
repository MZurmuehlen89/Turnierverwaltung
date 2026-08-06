using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Application.Spielplan.Eingaben;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Wpf.Navigation;
using Fussballturnierverwaltung.Wpf.Ribbon;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace Fussballturnierverwaltung.Wpf.ViewModels;

public partial class UebergangsregelViewModel : ViewModelBase, IRibbonAware, IHatUngespeicherteAenderungen
{
    private readonly TurnierInstanzService _instanzService;
    private readonly UebergangsregelService _regelService;
    private readonly AusgewaehlteTurnierInstanzService _turnierAuswahlService;
    private int _populiertGerade;

    // ── Turnier-Auswahl (bestimmt den Kontext für Runden/Regeln) ──────────────
    [ObservableProperty]
    private ObservableCollection<TurnierInstanz> _turniere = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TurnierGewaehlt))]
    [NotifyCanExecuteChangedFor(nameof(NeuCommand))]
    private TurnierInstanz? _ausgewaehltesTurnier;

    public bool TurnierGewaehlt => AusgewaehltesTurnier is not null;

    // ── Liste der Regeln des gewählten Turniers ───────────────────────────────
    [ObservableProperty]
    private ObservableCollection<Uebergangsregel> _regeln = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoeschenCommand))]
    private Uebergangsregel? _ausgewaehltes;

    // ── Runden des gewählten Turniers (Quelle für Quell-/Zielrunden-Auswahl) ──
    [ObservableProperty]
    private ObservableCollection<TurnierRunde> _runden = [];

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
    [NotifyPropertyChangedFor(nameof(ZielRundenOptionen))]
    [NotifyPropertyChangedFor(nameof(GruppeIndexLabel))]
    [NotifyPropertyChangedFor(nameof(GruppeIndexSichtbar))]
    [NotifyPropertyChangedFor(nameof(PlatzierungHinweis))]
    private TurnierRunde? _quellRunde;

    [ObservableProperty] private TurnierRunde? _zielRunde;
    [ObservableProperty] private string _gruppeIndex = "";
    [ObservableProperty] private string _platzierungVon = "1";
    [ObservableProperty] private string _platzierungBis = "1";

    // ── Berechnete Eigenschaften ───────────────────────────────────────────────
    public IEnumerable<TurnierRunde> ZielRundenOptionen =>
        Runden.Where(r => QuellRunde is null || r.Id != QuellRunde.Id);

    public string? GruppeIndexLabel => QuellRunde?.Rundenart switch
    {
        Rundenart.Gruppenphase                                             => "Gruppennummer (1=A, 2=B, ...)",
        Rundenart.KoRunde or Rundenart.Finale or Rundenart.SpielUmPlatzDrei => "Spielnummer innerhalb der Runde",
        _                                                                  => null
    };

    public bool GruppeIndexSichtbar => GruppeIndexLabel is not null;

    public string PlatzierungHinweis =>
        QuellRunde?.Rundenart is Rundenart.KoRunde or Rundenart.Finale or Rundenart.SpielUmPlatzDrei
            ? " (1 = Sieger, 2 = Verlierer)"
            : "";

    private static readonly HashSet<string> _formFelder =
        [nameof(QuellRunde), nameof(ZielRunde), nameof(GruppeIndex), nameof(PlatzierungVon), nameof(PlatzierungBis)];

    // ── IRibbonAware ───────────────────────────────────────────────────────────
    public string RibbonTabTitel => "Übergangsregeln";
    public ICommand NeuBefehl => NeuCommand;
    public ICommand SpeichernBefehl => SpeichernCommand;
    public ICommand AbbrechenBefehl => AbbrechenCommand;
    public ICommand LoeschenBefehl => LoeschenCommand;

    public UebergangsregelViewModel(
        TurnierInstanzService instanzService,
        UebergangsregelService regelService,
        AusgewaehlteTurnierInstanzService turnierAuswahlService)
    {
        _instanzService = instanzService;
        _regelService = regelService;
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
        _ = KontextFuerTurnierLadenAsync(value);
    }

    private async Task KontextFuerTurnierLadenAsync(TurnierInstanz? turnier)
    {
        if (turnier is null)
        {
            Runden = [];
            Regeln = [];
            FormularAusFuellenMit(null);
            return;
        }

        IstLadend = true;
        try
        {
            var detail = await _instanzService.FindenNachIdMitRundenAsync(turnier.Id);
            var regeln = await _regelService.AlleNachTurnierInstanzIdAsync(turnier.Id);

            Runden = new ObservableCollection<TurnierRunde>(
                (detail?.Runden ?? []).OrderBy(r => r.Reihenfolge));
            Regeln = new ObservableCollection<Uebergangsregel>(
                regeln.OrderBy(r => r.QuellRunde.Reihenfolge).ThenBy(r => r.GruppeIndex).ThenBy(r => r.PlatzierungVon));
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

    private void FormularAusFuellenMit(Uebergangsregel? regel)
    {
        _populiertGerade++;
        try
        {
            if (regel is null)
            {
                FormularAktiv = false;
                IstNeuModus = false;
                QuellRunde = null;
                ZielRunde = null;
                GruppeIndex = "";
                PlatzierungVon = "1";
                PlatzierungBis = "1";
            }
            else
            {
                FormularAktiv = true;
                IstNeuModus = false;
                QuellRunde = Runden.FirstOrDefault(r => r.Id == regel.QuellRundeId);
                ZielRunde = Runden.FirstOrDefault(r => r.Id == regel.ZielRundeId);
                GruppeIndex = regel.GruppeIndex?.ToString() ?? "";
                PlatzierungVon = regel.PlatzierungVon.ToString();
                PlatzierungBis = regel.PlatzierungBis.ToString();
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
            QuellRunde = null;
            ZielRunde = null;
            GruppeIndex = "";
            PlatzierungVon = "1";
            PlatzierungBis = "1";
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

        if (QuellRunde is null || ZielRunde is null)
        {
            Fehlermeldung = "Bitte Quell- und Zielrunde auswählen.";
            return;
        }

        try
        {
            var eingabe = new UebergangsregelEingabe(
                QuellRunde.Id, ZielRunde.Id,
                GruppeIndexSichtbar ? ParseNullableInt(GruppeIndex) : null,
                ParseInt(PlatzierungVon), ParseInt(PlatzierungBis));

            int gespeicherteId;
            if (IstNeuModus)
            {
                var neu = await _regelService.ErstellenAsync(eingabe);
                gespeicherteId = neu.Id;
            }
            else if (Ausgewaehltes is not null)
            {
                gespeicherteId = Ausgewaehltes.Id;
                await _regelService.AktualisierenAsync(gespeicherteId, eingabe);
            }
            else
            {
                return;
            }

            var regeln = await _regelService.AlleNachTurnierInstanzIdAsync(AusgewaehltesTurnier.Id);
            Regeln = new ObservableCollection<Uebergangsregel>(
                regeln.OrderBy(r => r.QuellRunde.Reihenfolge).ThenBy(r => r.GruppeIndex).ThenBy(r => r.PlatzierungVon));

            var gespeichert = Regeln.FirstOrDefault(r => r.Id == gespeicherteId);
            // Explizit statt nur über die Ausgewaehltes-Zuweisung, da EF Core beim Neuladen
            // innerhalb desselben DbContext-Scopes für bereits getrackte Entitäten dieselbe
            // Instanz zurückgeben kann (Identity Resolution) — siehe RanglisteViewModel.
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
            $"Soll die Übergangsregel '{Ausgewaehltes.QuellRunde.Name}' → '{Ausgewaehltes.ZielRunde.Name}' wirklich gelöscht werden?",
            "Übergangsregel löschen", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (ergebnis != MessageBoxResult.Yes) return;

        Fehlermeldung = null;
        try
        {
            await _regelService.LoeschenAsync(Ausgewaehltes.Id);
            var regeln = await _regelService.AlleNachTurnierInstanzIdAsync(AusgewaehltesTurnier.Id);
            Regeln = new ObservableCollection<Uebergangsregel>(
                regeln.OrderBy(r => r.QuellRunde.Reihenfolge).ThenBy(r => r.GruppeIndex).ThenBy(r => r.PlatzierungVon));
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

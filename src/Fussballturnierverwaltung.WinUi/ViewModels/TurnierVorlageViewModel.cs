using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Application.Turniere.Eingaben;
using Fussballturnierverwaltung.WinUi.Ribbon;
using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

public record RegelwerkOption(string Anzeige, Regelwerk? Wert);

/// <summary>Spike-Nachbau (#278) von VorlagenRundeZeile aus dem WPF-Original — auf das für den Control-Vergleich
/// relevante Feld-Set reduziert. WinUI 3 hat kein gepflegtes DataGrid-Control mehr (Recherchefund #278), daher
/// hier als ListView-Zeile statt DataGrid-Zeile verwendet — das ViewModel selbst bleibt Control-agnostisch.</summary>
public partial class VorlagenRundeZeile : ObservableObject
{
    public int? DbId { get; init; }

    [ObservableProperty] private string _name = "Liga";
    [ObservableProperty] private Rundenart _rundenart = Rundenart.Liga;
    [ObservableProperty] private int _eingangsMannschaftsanzahl = 8;
    [ObservableProperty] private int _ausgangsMannschaftsanzahl = 4;
    [ObservableProperty] private bool _mitRueckspiel;
    [ObservableProperty] private bool _auswaertstorRegelAktiv;
    [ObservableProperty] private RegelwerkOption _regelwerk = new("— Wählen —", null);
}

/// <summary>Spike-Nachbau von VorlagenUebergangsRegelZeile — Quelle/Ziel bewusst als Objektreferenz auf eine
/// VorlagenRundeZeile (nicht als Id), analog zum WPF-Original, da neue Runden vor dem Speichern noch keine
/// reale Id haben.</summary>
public partial class VorlagenUebergangsRegelZeile : ObservableObject
{
    [ObservableProperty] private VorlagenRundeZeile? _quelle;
    [ObservableProperty] private VorlagenRundeZeile? _ziel;
    [ObservableProperty] private int? _gruppeIndex;
    [ObservableProperty] private int _platzierungVon = 1;
    [ObservableProperty] private int _platzierungBis = 1;
}

public partial class TurnierVorlageViewModel : ViewModelBase, IRibbonAware
{
    private readonly TurnierVorlageService _service;
    private readonly RegelwerkService _regelwerkService;
    private int _populiertGerade;

    private static readonly RegelwerkOption _keinRegelwerk = new("— Wählen —", null);

    [ObservableProperty] private ObservableCollection<TurnierVorlage> _vorlagen = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoeschenCommand))]
    private TurnierVorlage? _ausgewaehltes;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SpeichernCommand))]
    [NotifyCanExecuteChangedFor(nameof(AbbrechenCommand))]
    private bool _formularAktiv;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SpeichernCommand))]
    [NotifyCanExecuteChangedFor(nameof(AbbrechenCommand))]
    private bool _isDirty;

    [ObservableProperty] private bool _istNeuModus;
    [ObservableProperty] private bool _istLadend;
    [ObservableProperty] private string? _fehlermeldung;

    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _beschreibung = "";
    [ObservableProperty] private string _anzahlMannschaften = "8";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RundeEntfernenCommand))]
    private VorlagenRundeZeile? _ausgewaehlteRunde;

    [ObservableProperty] private ObservableCollection<VorlagenRundeZeile> _rundenZeilen = [];
    [ObservableProperty] private ObservableCollection<VorlagenUebergangsRegelZeile> _uebergangsregelZeilen = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UebergangsregelEntfernenCommand))]
    private VorlagenUebergangsRegelZeile? _ausgewaehlteUebergangsregel;

    [ObservableProperty] private ObservableCollection<Regelwerk> _regelwerke = [];

    public IEnumerable<Rundenart> RundenartOptionen => Enum.GetValues<Rundenart>();

    public IReadOnlyList<RegelwerkOption> RegelwerkOptionen =>
        [_keinRegelwerk, .. Regelwerke.Select(r => new RegelwerkOption(r.Name, r))];

    public string RibbonTabTitel => "Turniervorlagen (WinUI3-Spike)";
    public ICommand NeuBefehl => NeuCommand;
    public ICommand SpeichernBefehl => SpeichernCommand;
    public ICommand AbbrechenBefehl => AbbrechenCommand;
    public ICommand LoeschenBefehl => LoeschenCommand;

    public TurnierVorlageViewModel(TurnierVorlageService service, RegelwerkService regelwerkService)
    {
        _service = service;
        _regelwerkService = regelwerkService;
    }

    public override async Task AktivierenAsync() => await LadenAsync();

    private async Task LadenAsync()
    {
        IstLadend = true;
        try
        {
            var vorlagen = await _service.AlleAbrufenAsync();
            var regelwerke = await _regelwerkService.AlleAbrufenAsync();
            Vorlagen = new ObservableCollection<TurnierVorlage>(vorlagen);
            Regelwerke = new ObservableCollection<Regelwerk>(regelwerke);
        }
        finally
        {
            IstLadend = false;
        }
    }

    partial void OnAusgewaehltesChanged(TurnierVorlage? value) => _ = AusgewaehltesGeaendertAsync(value);

    private async Task AusgewaehltesGeaendertAsync(TurnierVorlage? vorlage)
    {
        _populiertGerade++;
        try
        {
            if (vorlage is null)
            {
                FormularAktiv = false;
                IstNeuModus = false;
                Name = Beschreibung = "";
                AnzahlMannschaften = "8";
                RundenZeilen = [];
                UebergangsregelZeilen = [];
            }
            else
            {
                FormularAktiv = true;
                IstNeuModus = false;
                Name = vorlage.Name;
                Beschreibung = vorlage.Beschreibung;
                AnzahlMannschaften = vorlage.ErforderlicheMannschaftsanzahl.ToString();
            }
            IsDirty = false;
            Fehlermeldung = null;
        }
        finally
        {
            _populiertGerade--;
        }

        if (vorlage is null) return;

        IstLadend = true;
        try
        {
            var mitRunden = await _service.FindenNachIdMitRundenAsync(vorlage.Id);
            _populiertGerade++;
            try
            {
                var vorlagenRunden = (mitRunden?.VorlagenRunden ?? []).OrderBy(r => r.Reihenfolge).ToList();
                var zeilen = vorlagenRunden.Select(r => new VorlagenRundeZeile
                {
                    DbId = r.Id,
                    Name = r.Name,
                    Rundenart = r.Rundenart,
                    EingangsMannschaftsanzahl = r.EingangsMannschaftsanzahl,
                    AusgangsMannschaftsanzahl = r.AusgangsMannschaftsanzahl,
                    MitRueckspiel = r.MitRueckspiel,
                    AuswaertstorRegelAktiv = r.AuswaertstorRegelAktiv,
                    Regelwerk = RegelwerkOptionen.FirstOrDefault(o => o.Wert?.Id == r.RegelwerkId) ?? _keinRegelwerk
                }).ToList();
                RundenZeilen = new ObservableCollection<VorlagenRundeZeile>(zeilen);
                AbonniereAenderungen(RundenZeilen.Cast<ObservableObject>());

                var zeileNachVorlagenRundeId = vorlagenRunden.Zip(zeilen, (r, z) => (r.Id, z))
                    .ToDictionary(p => p.Id, p => p.z);
                UebergangsregelZeilen = new ObservableCollection<VorlagenUebergangsRegelZeile>(
                    vorlagenRunden.SelectMany(r => r.AusgehendeUebergangsregeln)
                        .Where(regel => zeileNachVorlagenRundeId.ContainsKey(regel.ZielVorlagenRundeId))
                        .Select(regel => new VorlagenUebergangsRegelZeile
                        {
                            Quelle = zeileNachVorlagenRundeId[regel.QuellVorlagenRundeId],
                            Ziel = zeileNachVorlagenRundeId[regel.ZielVorlagenRundeId],
                            GruppeIndex = regel.GruppeIndex,
                            PlatzierungVon = regel.PlatzierungVon,
                            PlatzierungBis = regel.PlatzierungBis
                        }));
                AbonniereAenderungen(UebergangsregelZeilen.Cast<ObservableObject>());
                IsDirty = false;
            }
            finally
            {
                _populiertGerade--;
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

    private void AbonniereAenderungen(IEnumerable<ObservableObject> zeilen)
    {
        foreach (var zeile in zeilen)
            zeile.PropertyChanged += OnZeileGeaendert;
    }

    private void OnZeileGeaendert(object? sender, PropertyChangedEventArgs e)
    {
        if (_populiertGerade == 0)
            IsDirty = true;
    }

    [RelayCommand]
    private void RundeHinzufuegen()
    {
        var letzte = RundenZeilen.LastOrDefault();
        var neu = new VorlagenRundeZeile
        {
            Rundenart = Rundenart.Liga,
            EingangsMannschaftsanzahl = letzte?.AusgangsMannschaftsanzahl ?? 8,
            AusgangsMannschaftsanzahl = Math.Max(1, (letzte?.AusgangsMannschaftsanzahl ?? 8) / 2)
        };
        neu.PropertyChanged += OnZeileGeaendert;
        RundenZeilen.Add(neu);
        AusgewaehlteRunde = neu;
        IsDirty = true;
    }

    [RelayCommand(CanExecute = nameof(KannRundeEntfernen))]
    private void RundeEntfernen()
    {
        if (AusgewaehlteRunde is null) return;
        var entfernt = AusgewaehlteRunde;
        RundenZeilen.Remove(entfernt);
        AusgewaehlteRunde = RundenZeilen.LastOrDefault();
        foreach (var regel in UebergangsregelZeilen.Where(z => z.Quelle == entfernt || z.Ziel == entfernt).ToList())
            UebergangsregelZeilen.Remove(regel);
        IsDirty = true;
    }

    private bool KannRundeEntfernen() => AusgewaehlteRunde is not null;

    [RelayCommand]
    private void UebergangsregelHinzufuegen()
    {
        var neu = new VorlagenUebergangsRegelZeile { Quelle = AusgewaehlteRunde };
        neu.PropertyChanged += OnZeileGeaendert;
        UebergangsregelZeilen.Add(neu);
        AusgewaehlteUebergangsregel = neu;
        IsDirty = true;
    }

    [RelayCommand(CanExecute = nameof(KannUebergangsregelEntfernen))]
    private void UebergangsregelEntfernen()
    {
        if (AusgewaehlteUebergangsregel is null) return;
        UebergangsregelZeilen.Remove(AusgewaehlteUebergangsregel);
        AusgewaehlteUebergangsregel = UebergangsregelZeilen.LastOrDefault();
        IsDirty = true;
    }

    private bool KannUebergangsregelEntfernen() => AusgewaehlteUebergangsregel is not null;

    [RelayCommand]
    private void Neu()
    {
        _populiertGerade++;
        try
        {
            Ausgewaehltes = null;
            FormularAktiv = true;
            IstNeuModus = true;
            Name = Beschreibung = "";
            AnzahlMannschaften = "8";
            RundenZeilen = [];
            UebergangsregelZeilen = [];
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
            var anzahl = int.TryParse(AnzahlMannschaften.Trim(), out var n) && n >= 2 ? n : 2;
            var rundenEingaben = RundenZeilen.Select(z => new VorlagenRundeEingabe(
                z.Name, z.Rundenart, z.EingangsMannschaftsanzahl, z.AusgangsMannschaftsanzahl,
                z.MitRueckspiel, z.AuswaertstorRegelAktiv, z.Regelwerk.Wert?.Id)).ToList();

            var uebergangsregelEingaben = UebergangsregelZeilen.Select(z =>
            {
                if (z.Quelle is null || z.Ziel is null)
                    throw new ArgumentException("Übergangsregel benötigt Quell- und Zielrunde.");
                return new VorlagenUebergangsRegelEingabe(
                    RundenZeilen.IndexOf(z.Quelle) + 1, RundenZeilen.IndexOf(z.Ziel) + 1,
                    z.GruppeIndex, z.PlatzierungVon, z.PlatzierungBis);
            }).ToList();

            if (IstNeuModus)
            {
                var eingabe = new TurnierVorlageErstellenEingabe(
                    Name, Beschreibung, anzahl, VorlagenKategorie.Benutzerdefiniert, false, null, null, null);
                var neu = await _service.ErstellenAsync(eingabe);
                await _service.RundenSynchronisierenAsync(neu.Id, rundenEingaben, uebergangsregelEingaben);
                await LadenAsync();
                Ausgewaehltes = Vorlagen.FirstOrDefault(v => v.Id == neu.Id);
            }
            else if (Ausgewaehltes is not null)
            {
                var id = Ausgewaehltes.Id;
                var eingabe = new TurnierVorlageAktualisierenEingabe(
                    Name, Beschreibung, anzahl, Ausgewaehltes.VorlagenKategorie, Ausgewaehltes.IstHistorisch,
                    Ausgewaehltes.GueltigVon, Ausgewaehltes.GueltigBis, Ausgewaehltes.Logo);
                await _service.AktualisierenAsync(id, eingabe);
                await _service.RundenSynchronisierenAsync(id, rundenEingaben, uebergangsregelEingaben);
                await LadenAsync();
                Ausgewaehltes = Vorlagen.FirstOrDefault(v => v.Id == id);
            }

            IsDirty = false;
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
    }

    [RelayCommand(CanExecute = nameof(KannAbbrechen))]
    private void Abbrechen() => _ = AusgewaehltesGeaendertAsync(IstNeuModus ? null : Ausgewaehltes);

    [RelayCommand(CanExecute = nameof(KannLoeschen))]
    private async Task Loeschen()
    {
        if (Ausgewaehltes is null) return;
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

    private bool KannSpeichern() => FormularAktiv && IsDirty;
    private bool KannAbbrechen() => FormularAktiv && IsDirty;
    private bool KannLoeschen() => Ausgewaehltes is not null && !IstNeuModus;
}

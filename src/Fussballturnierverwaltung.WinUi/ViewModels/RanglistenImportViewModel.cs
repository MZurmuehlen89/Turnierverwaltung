using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Application.Ranglisten;
using Fussballturnierverwaltung.Application.Ranglisten.Eingaben;
using Fussballturnierverwaltung.Application.Ranglisten.Import;
using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Ranglisten;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.WinUi.Dialoge;
using Fussballturnierverwaltung.WinUi.Navigation;
using Fussballturnierverwaltung.WinUi.Ribbon;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Reiter „Import“ der Ranglisten-Seite (#477, vorher Dialog aus #120/#285): Quelle wählen, abrufen, alle
/// Zeilen in Quellreihenfolge in einer Tabelle zuordnen und in eine neue oder bestehende Rangliste importieren. Eine
/// abgerufene, nicht importierte Vorschau zählt als ungespeichert.</summary>
public partial class RanglistenImportViewModel(
    RanglistenImportService importService,
    RanglisteService ranglisteService,
    MannschaftService mannschaftService,
    LandService landService,
    IDialogService dialogService) : ObservableObject, IRibbonAware, IHatUngespeicherteAenderungen
{
    private RanglistenImportVorschau? _vorschau;
    // Quelle, zu der _vorschau gehört — nicht AusgewaehlteQuelle, die sich nach dem Abruf noch ändern kann (#349).
    private int? _vorschauQuelleId;
    // Ziel aus „Aus Quelle aktualisieren“; wird nach dem Abruf angewendet, weil die Ziele erst dann geladen sind.
    private int? _vorgemerktesZielId;
    private IReadOnlyList<Land>? _laender;

    // ── Quelle ────────────────────────────────────────────────────────────────
    [ObservableProperty] private ObservableCollection<RanglistenQuelle> _quellen = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(QuelleAuswahl))]
    [NotifyPropertyChangedFor(nameof(QuelleGewaehlt))]
    [NotifyCanExecuteChangedFor(nameof(AbrufenCommand))]
    [NotifyCanExecuteChangedFor(nameof(QuelleBearbeitenCommand))]
    private RanglistenQuelle? _ausgewaehlteQuelle;

    public bool QuelleGewaehlt => AusgewaehlteQuelle is not null;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AbrufenCommand))]
    private bool _istLadend;

    [ObservableProperty] private string? _fehlermeldung;

    // ── Vorschau ──────────────────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ImportierenCommand))]
    [NotifyCanExecuteChangedFor(nameof(VerwerfenCommand))]
    [NotifyPropertyChangedFor(nameof(HatUngespeicherteAenderungen))]
    private bool _vorschauVorhanden;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ZeilenAnzeige))]
    [NotifyPropertyChangedFor(nameof(ZusammenfassungText))]
    private IReadOnlyList<RanglistenImportZeileZuordnung> _zeilen = [];

    // Filtert nach „nicht erkannt“, nicht nach dem Status — eine Zeile verschwindet also nicht, sobald sie zugeordnet ist.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ZeilenAnzeige))]
    private bool _nurNichtErkannte;

    public IReadOnlyList<RanglistenImportZeileZuordnung> ZeilenAnzeige =>
        NurNichtErkannte ? Zeilen.Where(z => z.IstNichtErkannt).ToList() : Zeilen;

    public string ZusammenfassungText => ImportZusammenfassung.Text(Zeilen.Select(z => z.Status));

    // ── Ziel ──────────────────────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BestehendeAktualisieren))]
    private bool _neueRanglisteErstellen = true;

    [ObservableProperty] private string _name = "";
    // CalendarDatePicker liefert DateTimeOffset?; leer importiert = heute.
    [ObservableProperty] private DateTimeOffset? _datum = DateTimeOffset.Now.Date;
    [ObservableProperty] private string _quelleText = "";
    [ObservableProperty] private ObservableCollection<Rangliste> _vorhandeneRanglisten = [];
    [ObservableProperty] private Rangliste? _zielRangliste;

    /// <summary>Inverse von NeueRanglisteErstellen für die zweite RadioButton-Option (TwoWay-Binding ohne Converter).</summary>
    public bool BestehendeAktualisieren
    {
        get => !NeueRanglisteErstellen;
        set => NeueRanglisteErstellen = !value;
    }

    /// <summary>Der Reiter „Quellen“ soll diese Quelle zum Bearbeiten öffnen.</summary>
    public event EventHandler<int>? QuelleBearbeitenAngefordert;

    /// <summary>Import gespeichert; Wert = Id der importierten Rangliste.</summary>
    public event EventHandler<int>? Importiert;

    // ── IRibbonAware ──────────────────────────────────────────────────────────
    public string RibbonTabTitel => "Ranglisten";
    public ICommand? NeuBefehl => null;
    public ICommand? SpeichernBefehl => null;
    public ICommand? AbbrechenBefehl => VerwerfenCommand;
    public ICommand? LoeschenBefehl => null;
    public bool HatStandardAktionen => false;
    public bool HatLoeschenAktion => false;

    public IReadOnlyList<RibbonGruppe> RibbonZusatzGruppen =>
    [
        new RibbonGruppe
        {
            Header = "Import",
            Aktionen =
            [
                new RibbonAktion { Header = "Import ausführen", Befehl = ImportierenCommand, IconGlyph = "" },
                new RibbonAktion { Header = "Vorschau verwerfen", Befehl = VerwerfenCommand, IconGlyph = "" }
            ]
        }
    ];

    public bool HatUngespeicherteAenderungen => VorschauVorhanden;

    // ── Quellen ───────────────────────────────────────────────────────────────

    /// <summary>Übernimmt die Quellen des Reiters „Quellen“ (beim Reiterwechsel). Die Auswahl bleibt, solange es
    /// die Quelle noch gibt — samt abgerufener Vorschau.</summary>
    public void QuellenSetzen(IReadOnlyList<RanglistenQuelle> quellen)
    {
        if (quellen.SequenceEqual(Quellen)) return;
        var id = AusgewaehlteQuelle?.Id;
        Quellen = new ObservableCollection<RanglistenQuelle>(quellen);
        AusgewaehlteQuelle = Quellen.FirstOrDefault(q => q.Id == id);
    }

    // Bindungshülle der Liste: WinUI schreibt beim Tausch der ItemsSource null über TwoWay zurück (#377, #308) — das
    // würde die Vorschau verwerfen. null wird ignoriert und die Anzeige wiederhergestellt.
    public RanglistenQuelle? QuelleAuswahl
    {
        get => AusgewaehlteQuelle;
        set
        {
            if (value is not null) AusgewaehlteQuelle = value;
            else Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread()?.TryEnqueue(
                Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () => OnPropertyChanged(nameof(QuelleAuswahl)));
        }
    }

    // Eine Vorschau gehört zu genau einer Quelle; ein Wechsel verwirft sie.
    partial void OnAusgewaehlteQuelleChanged(RanglistenQuelle? oldValue, RanglistenQuelle? newValue)
    {
        if (oldValue?.Id != newValue?.Id)
            VorschauVerwerfen();
    }

    [RelayCommand(CanExecute = nameof(QuelleGewaehlt))]
    private void QuelleBearbeiten()
    {
        if (AusgewaehlteQuelle is not null)
            QuelleBearbeitenAngefordert?.Invoke(this, AusgewaehlteQuelle.Id);
    }

    /// <summary>„Aus Quelle aktualisieren“ (#477): wählt die Quelle der Rangliste, merkt sie als Ziel vor und ruft ab.</summary>
    public async Task FuerRanglisteAbrufenAsync(Rangliste rangliste)
    {
        var quelle = Quellen.FirstOrDefault(q => q.Id == rangliste.RanglistenQuelleId);
        if (quelle is null)
        {
            Fehlermeldung = $"Die Quelle von '{rangliste.Name}' gibt es nicht mehr.";
            return;
        }

        AusgewaehlteQuelle = quelle;
        _vorgemerktesZielId = rangliste.Id;
        await AbrufenAsync();
    }

    // ── Abrufen ───────────────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(KannAbrufen))]
    private async Task AbrufenAsync()
    {
        if (AusgewaehlteQuelle is not { } quelle) return;

        VorschauVerwerfen();
        IstLadend = true;
        try
        {
            var vorschau = await importService.VorschauAbrufenAsync(quelle);
            var alleTeams = await mannschaftService.AlleAbrufenAsync();
            _laender ??= (await landService.AlleAbrufenAsync()).OrderBy(l => l.Name).ToList();
            VorhandeneRanglisten = new ObservableCollection<Rangliste>(await ranglisteService.AlleAbrufenAsync());

            _vorschau = vorschau;
            _vorschauQuelleId = quelle.Id;
            Zeilen = ZeilenAufbauen(vorschau, alleTeams);
            ZielVorbelegen(quelle);
            VorschauVorhanden = true;
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
        finally
        {
            _vorgemerktesZielId = null;
            IstLadend = false;
        }
    }

    private bool KannAbrufen() => AusgewaehlteQuelle is not null && !IstLadend;

    private List<RanglistenImportZeileZuordnung> ZeilenAufbauen(RanglistenImportVorschau vorschau, IEnumerable<Mannschaft> alleTeams)
    {
        var vergeben = vorschau.Aufgeloest.Select(z => z.Mannschaft.Id).ToHashSet();
        var optionen = new List<MannschaftAuswahlOption>
        {
            RanglistenImportZeileZuordnung.OffenOption,
            RanglistenImportZeileZuordnung.UeberspringenOption,
            RanglistenImportZeileZuordnung.NeuAnlegenOption
        };
        optionen.AddRange(alleTeams.Where(t => !vergeben.Contains(t.Id)).OrderBy(t => t.Name)
            .Select(t => new MannschaftAuswahlOption(t.Name, t)));

        var zeilen = vorschau.Aufgeloest.Select(RanglistenImportZeileZuordnung.Automatisch)
            .Concat(vorschau.Unaufgeloest.Select(z => RanglistenImportZeileZuordnung.NichtErkannt(
                z, optionen, _laender!, mannschaftService, MannschaftAngelegtAsync)))
            .OrderBy(z => z.Quelle.Index)
            .ToList();
        foreach (var zeile in zeilen)
            zeile.StatusGeaendert += (_, _) => OnPropertyChanged(nameof(ZusammenfassungText));
        return zeilen;
    }

    private void ZielVorbelegen(RanglistenQuelle quelle)
    {
        QuelleText = $"{quelle.Name} ({quelle.Url})";
        Datum = DateTimeOffset.Now.Date;
        var ziel = VorhandeneRanglisten.FirstOrDefault(r => r.Id == _vorgemerktesZielId);
        NeueRanglisteErstellen = ziel is null;
        ZielRangliste = ziel;
        Name = ziel?.Name ?? quelle.Name;
    }

    // Der Name folgt dem Ziel: eine bestehende Rangliste behält ihren Namen, eine neue heißt wie die Quelle.
    partial void OnZielRanglisteChanged(Rangliste? value)
    {
        if (value is not null && BestehendeAktualisieren) Name = value.Name;
    }

    partial void OnNeueRanglisteErstellenChanged(bool value)
    {
        if (!value && ZielRangliste is not null) Name = ZielRangliste.Name;
        else if (value && AusgewaehlteQuelle is not null) Name = AusgewaehlteQuelle.Name;
    }

    /// <summary>Bietet eine über eine Zeile neu angelegte Mannschaft allen anderen nicht erkannten Zeilen als Option
    /// an und übernimmt sie in der auslösenden Zeile selbst (Issue #227).</summary>
    private Task MannschaftAngelegtAsync(RanglistenImportZeileZuordnung zeile, Mannschaft neu)
    {
        var option = new MannschaftAuswahlOption(neu.Name, neu);
        foreach (var z in Zeilen.Where(z => z.IstNichtErkannt))
            z.Optionen.Add(option);

        zeile.Ausgewaehlt = option;
        return Task.CompletedTask;
    }

    // ── Import ────────────────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(VorschauVorhanden))]
    private async Task ImportierenAsync()
    {
        if (_vorschau is null) return;

        Fehlermeldung = null;
        try
        {
            var zuordnung = Zeilen.Where(z => z.IstNichtErkannt).ToDictionary(z => z.Quelle.Index, z => z.Mannschaft);
            var eintraege = importService.EintraegeErstellen(_vorschau, zuordnung);
            if (eintraege.Count == 0)
            {
                Fehlermeldung = "Keine Einträge zum Importieren (keine Zeile zugeordnet).";
                return;
            }
            if (BestehendeAktualisieren && ZielRangliste is null)
            {
                Fehlermeldung = "Bitte eine Ziel-Rangliste wählen.";
                return;
            }

            var offen = Zeilen.Count(z => z.Status == ImportZeilenStatus.Offen);
            var rueckfrage = ImportZusammenfassung.Rueckfrage(offen, BestehendeAktualisieren ? ZielRangliste!.Name : null);
            if (rueckfrage is not null && !await dialogService.BestaetigenAsync("Import ausführen", rueckfrage))
                return;

            var datum = DateOnly.FromDateTime((Datum ?? DateTimeOffset.Now).Date);
            int id;
            if (NeueRanglisteErstellen)
            {
                var neu = await ranglisteService.ErstellenAsync(
                    new RanglisteErstellenEingabe(Name, datum, QuelleText, eintraege, _vorschauQuelleId));
                id = neu.Id;
            }
            else
            {
                id = ZielRangliste!.Id;
                // Profigrenze und Abschnitte gehören zur Rangliste, nicht zum Import — sie bleiben (#393).
                await ranglisteService.AktualisierenAsync(id, new RanglisteAktualisierenEingabe(
                    Name, datum, QuelleText, eintraege, _vorschauQuelleId, ZielRangliste.ProfigrenzeSpielklasse));
            }

            VorschauVerwerfen();
            Importiert?.Invoke(this, id);
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
    }

    [RelayCommand(CanExecute = nameof(VorschauVorhanden))]
    private void Verwerfen() => VorschauVerwerfen();

    private void VorschauVerwerfen()
    {
        _vorschau = null;
        _vorschauQuelleId = null;
        Zeilen = [];
        NurNichtErkannte = false;
        VorschauVorhanden = false;
        Fehlermeldung = null;
    }
}

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Application.Ranglisten;
using Fussballturnierverwaltung.Application.Ranglisten.Eingaben;
using Fussballturnierverwaltung.Application.Ranglisten.Import;
using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Ranglisten;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.WinUi.Dialoge;
using System.Collections.ObjectModel;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

public partial class RanglistenImportViewModel(
    RanglistenImportService importService,
    RanglistenQuelleService quelleService,
    RanglisteService ranglisteService,
    MannschaftService mannschaftService,
    LandService landService,
    IDialogService dialogService) : ObservableObject
{
    private static readonly MannschaftAuswahlOption Ueberspringen = new("— Überspringen —", null);
    private static readonly MannschaftAuswahlOption NeuAnlegenOption = new("+ Neue Mannschaft anlegen…", null, IstNeuAnlegen: true);

    private RanglistenImportVorschau? _vorschau;
    // Quelle, zu der _vorschau gehört — nicht AusgewaehlteQuelle, die sich nach dem Abruf noch ändern kann (#349).
    private int? _vorschauQuelleId;
    private IReadOnlyList<Land> _laender = [];

    [ObservableProperty] private ObservableCollection<RanglistenQuelle> _quellen = [];
    [ObservableProperty] private RanglistenQuelle? _ausgewaehlteQuelle;
    [ObservableProperty] private bool _istLadend;
    [ObservableProperty] private string? _fehlermeldung;

    [ObservableProperty] private bool _vorschauVorhanden;
    [ObservableProperty] private ObservableCollection<RanglistenImportZeile> _aufgeloest = [];
    [ObservableProperty] private ObservableCollection<UnaufgeloesteZeileZuordnung> _unaufgeloest = [];

    [ObservableProperty] private bool _neueRanglisteErstellen = true;
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _datum = DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd");
    [ObservableProperty] private string _quelleText = "";
    [ObservableProperty] private ObservableCollection<Rangliste> _vorhandeneRanglisten = [];
    [ObservableProperty] private Rangliste? _zielRangliste;

    [ObservableProperty] private bool _importAbgeschlossen;

    /// <summary>Inverse von NeueRanglisteErstellen für die zweite RadioButton-Option
    /// (TwoWay-Binding ohne Converter).</summary>
    public bool BestehendeAktualisieren
    {
        get => !NeueRanglisteErstellen;
        set => NeueRanglisteErstellen = !value;
    }

    partial void OnNeueRanglisteErstellenChanged(bool value) => OnPropertyChanged(nameof(BestehendeAktualisieren));

    public async Task InitialisierenAsync()
    {
        IstLadend = true;
        try
        {
            Quellen = new ObservableCollection<RanglistenQuelle>(await quelleService.AlleAbrufenAsync());
            AusgewaehlteQuelle = Quellen.FirstOrDefault();
            VorhandeneRanglisten = new ObservableCollection<Rangliste>(await ranglisteService.AlleAbrufenAsync());
            _laender = (await landService.AlleAbrufenAsync()).OrderBy(l => l.Name).ToList();
        }
        finally { IstLadend = false; }
    }

    [RelayCommand]
    private async Task AbrufenAsync()
    {
        if (AusgewaehlteQuelle is null) return;

        Fehlermeldung = null;
        IstLadend = true;
        try
        {
            _vorschau = await importService.VorschauAbrufenAsync(AusgewaehlteQuelle);
            _vorschauQuelleId = AusgewaehlteQuelle.Id;
            Aufgeloest = new ObservableCollection<RanglistenImportZeile>(_vorschau.Aufgeloest);

            var alleTeams = await mannschaftService.AlleAbrufenAsync();
            var vergeben = _vorschau.Aufgeloest.Select(z => z.Mannschaft.Id).ToHashSet();
            var verfuegbar = alleTeams.Where(t => !vergeben.Contains(t.Id)).OrderBy(t => t.Name).ToList();
            var optionen = new List<MannschaftAuswahlOption> { Ueberspringen, NeuAnlegenOption };
            optionen.AddRange(verfuegbar.Select(t => new MannschaftAuswahlOption(t.Name, t)));

            Unaufgeloest = new ObservableCollection<UnaufgeloesteZeileZuordnung>(_vorschau.Unaufgeloest
                .OrderBy(z => z.Index)
                .Select(z => new UnaufgeloesteZeileZuordnung(z, optionen, _laender, mannschaftService, MannschaftAngelegtAsync)));

            Name = AusgewaehlteQuelle.Name;
            QuelleText = $"{AusgewaehlteQuelle.Name} ({AusgewaehlteQuelle.Url})";
            VorschauVorhanden = true;
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
        finally { IstLadend = false; }
    }

    /// <summary>Bietet eine über eine Zeile neu angelegte Mannschaft allen anderen offenen Zeilen als
    /// Option an und übernimmt sie in der auslösenden Zeile selbst (Issue #227).</summary>
    private Task MannschaftAngelegtAsync(UnaufgeloesteZeileZuordnung zeile, Mannschaft neu)
    {
        var option = new MannschaftAuswahlOption(neu.Name, neu);
        foreach (var z in Unaufgeloest)
            z.Optionen.Add(option);

        zeile.Ausgewaehlt = option;
        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task ImportierenAsync()
    {
        if (_vorschau is null) return;

        Fehlermeldung = null;
        try
        {
            var zuordnung = Unaufgeloest.ToDictionary(z => z.Quelle.Index, z => z.Ausgewaehlt.Mannschaft);
            var eintraege = importService.EintraegeErstellen(_vorschau, zuordnung);
            if (eintraege.Count == 0)
            {
                Fehlermeldung = "Keine Einträge zum Importieren (alle Zeilen übersprungen).";
                return;
            }

            var datum = DateOnly.TryParse(Datum, out var d) ? d : DateOnly.FromDateTime(DateTime.Today);

            if (NeueRanglisteErstellen)
            {
                var eingabe = new RanglisteErstellenEingabe(Name, datum, QuelleText, eintraege, _vorschauQuelleId);
                await ranglisteService.ErstellenAsync(eingabe);
            }
            else
            {
                if (ZielRangliste is null)
                {
                    Fehlermeldung = "Bitte eine Ziel-Rangliste wählen.";
                    return;
                }

                var bestaetigt = await dialogService.BestaetigenAsync(
                    "Rangliste überschreiben",
                    $"Alle bisherigen Einträge von '{ZielRangliste.Name}' werden ersetzt. Fortfahren?");
                if (!bestaetigt) return;

                // Die Profigrenze gehört zur Rangliste, nicht zum Import — sie bleibt (#393).
                var eingabe = new RanglisteAktualisierenEingabe(
                    Name, datum, QuelleText, eintraege, _vorschauQuelleId, ZielRangliste.ProfigrenzeSpielklasse);
                await ranglisteService.AktualisierenAsync(ZielRangliste.Id, eingabe);
            }

            ImportAbgeschlossen = true;
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
    }
}

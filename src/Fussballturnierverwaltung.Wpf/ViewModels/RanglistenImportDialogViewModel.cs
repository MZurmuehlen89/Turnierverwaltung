using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Application.Ranglisten;
using Fussballturnierverwaltung.Application.Ranglisten.Eingaben;
using Fussballturnierverwaltung.Application.Ranglisten.Import;
using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Ranglisten;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using System.Collections.ObjectModel;
using System.Windows;

namespace Fussballturnierverwaltung.Wpf.ViewModels;

/// <summary>Auswahloption für die Zuordnungs-ComboBox einer nicht aufgelösten Zeile —
/// Mannschaft null bedeutet "Überspringen".</summary>
public record MannschaftAuswahlOption(string Anzeige, Mannschaft? Mannschaft);

public partial class UnaufgeloesteZeileZuordnung : ObservableObject
{
    public RanglistenRohZeile Quelle { get; }
    public IReadOnlyList<MannschaftAuswahlOption> Optionen { get; }

    [ObservableProperty] private MannschaftAuswahlOption _ausgewaehlt;

    public UnaufgeloesteZeileZuordnung(RanglistenRohZeile quelle, IReadOnlyList<MannschaftAuswahlOption> optionen)
    {
        Quelle = quelle;
        Optionen = optionen;
        _ausgewaehlt = optionen[0];
    }
}

public partial class RanglistenImportDialogViewModel(
    RanglistenImportService importService,
    RanglistenQuelleService quelleService,
    RanglisteService ranglisteService,
    MannschaftService mannschaftService) : ObservableObject
{
    private static readonly MannschaftAuswahlOption Ueberspringen = new("— Überspringen —", null);

    private RanglistenImportVorschau? _vorschau;

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
        }
        finally { IstLadend = false; }
    }

    public async Task QuellenNeuLadenAsync()
    {
        var quellen = await quelleService.AlleAbrufenAsync();
        var ausgewaehlteId = AusgewaehlteQuelle?.Id;
        Quellen = new ObservableCollection<RanglistenQuelle>(quellen);
        AusgewaehlteQuelle = Quellen.FirstOrDefault(q => q.Id == ausgewaehlteId) ?? Quellen.FirstOrDefault();
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
            Aufgeloest = new ObservableCollection<RanglistenImportZeile>(_vorschau.Aufgeloest);

            var alleTeams = await mannschaftService.AlleAbrufenAsync();
            var vergeben = _vorschau.Aufgeloest.Select(z => z.Mannschaft.Id).ToHashSet();
            var verfuegbar = alleTeams.Where(t => !vergeben.Contains(t.Id)).OrderBy(t => t.Name).ToList();
            var optionen = new List<MannschaftAuswahlOption> { Ueberspringen };
            optionen.AddRange(verfuegbar.Select(t => new MannschaftAuswahlOption(t.Name, t)));

            Unaufgeloest = new ObservableCollection<UnaufgeloesteZeileZuordnung>(_vorschau.Unaufgeloest
                .OrderBy(z => z.Index)
                .Select(z => new UnaufgeloesteZeileZuordnung(z, optionen)));

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
                var eingabe = new RanglisteErstellenEingabe(Name, datum, QuelleText, eintraege);
                await ranglisteService.ErstellenAsync(eingabe);
            }
            else
            {
                if (ZielRangliste is null)
                {
                    Fehlermeldung = "Bitte eine Ziel-Rangliste wählen.";
                    return;
                }

                var bestaetigt = MessageBox.Show(
                    $"Alle bisherigen Einträge von '{ZielRangliste.Name}' werden ersetzt. Fortfahren?",
                    "Rangliste überschreiben", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (bestaetigt != MessageBoxResult.Yes) return;

                var eingabe = new RanglisteAktualisierenEingabe(Name, datum, QuelleText, eintraege);
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

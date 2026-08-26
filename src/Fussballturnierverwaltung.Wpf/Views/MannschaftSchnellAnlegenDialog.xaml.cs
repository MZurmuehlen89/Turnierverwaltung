using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Application.Stammdaten.Eingaben;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;
using System.Windows;

namespace Fussballturnierverwaltung.Wpf.Views;

/// <summary>Kompakter Anlegen-Dialog für Mannschaften, die während eines Ranglisten-Imports
/// als nicht in den Stammdaten vorhanden erkannt wurden (Issue #227) — im Gegensatz zur
/// vollständigen Mannschaften-Verwaltungsseite als einfaches modales Fenster, analog zu
/// RanglistenQuelleDialog.</summary>
public partial class MannschaftSchnellAnlegenDialog : Window
{
    private readonly MannschaftService _mannschaftService;

    public Mannschaft? Erstellt { get; private set; }

    public MannschaftSchnellAnlegenDialog(MannschaftService mannschaftService, LandService landService, string vorschlagName)
    {
        InitializeComponent();
        _mannschaftService = mannschaftService;

        NameBox.Text = vorschlagName;
        KurznameBox.Text = new string(vorschlagName.Where(char.IsLetter).Take(3).ToArray()).ToUpperInvariant();

        Loaded += async (_, _) =>
        {
            var laender = await landService.AlleAbrufenAsync();
            LandBox.ItemsSource = laender.OrderBy(l => l.Name).ToList();
        };
    }

    private async void AnlegenButton_Click(object sender, RoutedEventArgs e)
    {
        FehlerText.Text = "";

        if (LandBox.SelectedItem is not Land land)
        {
            FehlerText.Text = "Bitte ein Land auswählen.";
            return;
        }

        try
        {
            var eingabe = new MannschaftErstellenEingabe(
                NameBox.Text,
                KurznameBox.Text,
                NationalmannschaftRadio.IsChecked == true ? Mannschaftsart.Nationalmannschaft : Mannschaftsart.Verein,
                land.Id,
                RatingParsen(OffensivBox.Text),
                RatingParsen(MittelfeldBox.Text),
                RatingParsen(DefensivBox.Text),
                RatingParsen(HeimstaerkeBox.Text),
                RatingParsen(AuswaertsstaerkeBox.Text));

            Erstellt = await _mannschaftService.ErstellenAsync(eingabe);
            DialogResult = true;
        }
        catch (Exception ex)
        {
            FehlerText.Text = ex.Message;
        }
    }

    private static int RatingParsen(string text) => int.TryParse(text, out var wert) ? wert : 0;

    private void AbbrechenButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}

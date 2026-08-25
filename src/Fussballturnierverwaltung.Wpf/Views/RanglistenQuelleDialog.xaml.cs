using Fussballturnierverwaltung.Application.Ranglisten;
using Fussballturnierverwaltung.Application.Ranglisten.Eingaben;
using Fussballturnierverwaltung.Domain.Entitaeten.Ranglisten;
using System.Windows;

namespace Fussballturnierverwaltung.Wpf.Views;

public partial class RanglistenQuelleDialog : Window
{
    private readonly RanglistenQuelleService _service;
    private readonly RanglistenQuelle? _bearbeitet;

    public RanglistenQuelleDialog(RanglistenQuelleService service, RanglistenQuelle? bearbeitet = null)
    {
        InitializeComponent();
        _service = service;
        _bearbeitet = bearbeitet;
        Title = bearbeitet is null ? "Neue Ranglisten-Quelle" : $"Quelle bearbeiten: {bearbeitet.Name}";

        NameBox.Text             = bearbeitet?.Name ?? "";
        UrlBox.Text              = bearbeitet?.Url ?? "";
        TabellenSelektorBox.Text = bearbeitet?.TabellenSelektor ?? "";
        TabellenIndexBox.Text    = (bearbeitet?.TabellenIndex ?? 0).ToString();
        MannschaftSpalteBox.Text = (bearbeitet?.MannschaftSpalte ?? 1).ToString();
        PunkteSpalteCheckbox.IsChecked = bearbeitet is null || bearbeitet.PunkteSpalte is not null;
        PunkteSpalteBox.Text     = (bearbeitet?.PunkteSpalte ?? 2).ToString();
        KopfzeileCheckbox.IsChecked = bearbeitet?.KopfzeileUeberspringen ?? true;
    }

    private async void SpeichernButton_Click(object sender, RoutedEventArgs e)
    {
        FehlerText.Text = "";
        try
        {
            var eingabe = new RanglistenQuelleErstellenEingabe(
                NameBox.Text,
                UrlBox.Text,
                int.TryParse(TabellenIndexBox.Text, out var tabellenIndex) ? tabellenIndex : 0,
                string.IsNullOrWhiteSpace(TabellenSelektorBox.Text) ? null : TabellenSelektorBox.Text,
                int.TryParse(MannschaftSpalteBox.Text, out var mannschaftSpalte) ? mannschaftSpalte : 0,
                PunkteSpalteCheckbox.IsChecked == true && int.TryParse(PunkteSpalteBox.Text, out var punkteSpalte)
                    ? punkteSpalte : null,
                KopfzeileCheckbox.IsChecked == true);

            if (_bearbeitet is null)
            {
                await _service.ErstellenAsync(eingabe);
            }
            else
            {
                var aktualisierenEingabe = new RanglistenQuelleAktualisierenEingabe(
                    eingabe.Name, eingabe.Url, eingabe.TabellenIndex, eingabe.TabellenSelektor,
                    eingabe.MannschaftSpalte, eingabe.PunkteSpalte, eingabe.KopfzeileUeberspringen);
                await _service.AktualisierenAsync(_bearbeitet.Id, aktualisierenEingabe);
            }

            DialogResult = true;
        }
        catch (Exception ex)
        {
            FehlerText.Text = ex.Message;
        }
    }

    private void AbbrechenButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}

using Fussballturnierverwaltung.Application.Ranglisten;
using Fussballturnierverwaltung.Application.Ranglisten.Eingaben;
using Fussballturnierverwaltung.Application.Ranglisten.Import;
using Fussballturnierverwaltung.Domain.Entitaeten.Ranglisten;
using System.Windows;

namespace Fussballturnierverwaltung.Wpf.Views;

/// <summary>Auswahloption der Tabellen-Liste (Issue #225) — Beschreibung fasst Zeilen-/Spaltenzahl
/// und die erste Vorschauzeile zusammen, damit der Nutzer die richtige Tabelle ohne HTML-Kenntnis
/// per Klick erkennt.</summary>
public record TabellenAuswahlOption(RanglistenTabellenVorschau Vorschau, string Beschreibung);

/// <summary>Auswahloption einer Spalten-Liste — SpaltenIndex null steht für "— keine —"
/// (nur bei der optionalen Punkte-Spalte verwendet).</summary>
public record SpaltenAuswahlOption(int? SpaltenIndex, string Beschreibung);

public partial class RanglistenQuelleDialog : Window
{
    private readonly RanglistenQuelleService _service;
    private readonly RanglistenQuelle? _bearbeitet;

    private IReadOnlyList<RanglistenTabellenVorschau> _tabellen = [];

    public RanglistenQuelleDialog(RanglistenQuelleService service, RanglistenQuelle? bearbeitet = null)
    {
        InitializeComponent();
        _service = service;
        _bearbeitet = bearbeitet;
        Title = bearbeitet is null ? "Neue Ranglisten-Quelle" : $"Quelle bearbeiten: {bearbeitet.Name}";

        NameBox.Text = bearbeitet?.Name ?? "";
        UrlBox.Text  = bearbeitet?.Url ?? "";
    }

    private async void PruefenButton_Click(object sender, RoutedEventArgs e)
    {
        FehlerText.Text = "";
        TabellenPanel.Visibility = Visibility.Collapsed;
        SpaltenPanel.Visibility = Visibility.Collapsed;

        var url = UrlBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(url))
        {
            FehlerText.Text = "Bitte zuerst eine URL eingeben.";
            return;
        }

        LadeText.Visibility = Visibility.Visible;
        PruefenButton.IsEnabled = false;
        try
        {
            _tabellen = await _service.WebseitePruefenAsync(url);
            if (_tabellen.Count == 0)
            {
                FehlerText.Text = "Auf der Seite wurden keine Tabellen gefunden.";
                return;
            }

            TabellenListBox.ItemsSource = _tabellen.Select(TabellenBeschreiben).ToList();
            TabellenPanel.Visibility = Visibility.Visible;

            var vorauswahlIndex = _bearbeitet is not null
                ? _tabellen.ToList().FindIndex(t => t.Index == _bearbeitet.TabellenIndex)
                : -1;
            TabellenListBox.SelectedIndex = vorauswahlIndex >= 0 ? vorauswahlIndex : 0;
        }
        catch (Exception ex)
        {
            FehlerText.Text = $"Seite konnte nicht geladen werden: {ex.Message}";
        }
        finally
        {
            LadeText.Visibility = Visibility.Collapsed;
            PruefenButton.IsEnabled = true;
        }
    }

    private void TabellenListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (TabellenListBox.SelectedItem is not TabellenAuswahlOption ausgewaehlt)
        {
            SpaltenPanel.Visibility = Visibility.Collapsed;
            return;
        }

        var vorschau = ausgewaehlt.Vorschau;
        var spalten = SpaltenBeschreiben(vorschau);

        MannschaftListBox.ItemsSource = spalten;
        PunkteListBox.ItemsSource = new[] { new SpaltenAuswahlOption(null, "— keine —") }.Concat(spalten).ToList();

        var mannschaftVorauswahl = _bearbeitet is not null && vorschau.Index == _bearbeitet.TabellenIndex
            ? spalten.ToList().FindIndex(s => s.SpaltenIndex == _bearbeitet.MannschaftSpalte)
            : -1;
        MannschaftListBox.SelectedIndex = mannschaftVorauswahl >= 0 ? mannschaftVorauswahl : 0;

        if (_bearbeitet is not null && vorschau.Index == _bearbeitet.TabellenIndex && _bearbeitet.PunkteSpalte is not null)
        {
            var punkteOptionen = (IReadOnlyList<SpaltenAuswahlOption>)PunkteListBox.ItemsSource;
            var punkteVorauswahl = punkteOptionen.ToList().FindIndex(s => s.SpaltenIndex == _bearbeitet.PunkteSpalte);
            PunkteListBox.SelectedIndex = punkteVorauswahl >= 0 ? punkteVorauswahl : 0;
        }
        else
        {
            PunkteListBox.SelectedIndex = 0;
        }

        KopfzeileCheckbox.IsChecked = _bearbeitet is not null && vorschau.Index == _bearbeitet.TabellenIndex
            ? _bearbeitet.KopfzeileUeberspringen
            : vorschau.ErkannteKopfzeile;

        SpaltenPanel.Visibility = Visibility.Visible;
    }

    private static TabellenAuswahlOption TabellenBeschreiben(RanglistenTabellenVorschau vorschau)
    {
        var ersteZeile = vorschau.VorschauZeilen.Count > 0
            ? string.Join(" | ", vorschau.VorschauZeilen[0])
            : "";

        var beschreibung = $"Tabelle {vorschau.Index + 1}: {vorschau.ZeilenAnzahl} Zeilen × {vorschau.SpaltenAnzahl} Spalten"
            + (string.IsNullOrWhiteSpace(ersteZeile) ? "" : $"\n{ersteZeile}");

        return new TabellenAuswahlOption(vorschau, beschreibung);
    }

    private static List<SpaltenAuswahlOption> SpaltenBeschreiben(RanglistenTabellenVorschau vorschau)
    {
        var datenZeilen = vorschau.ErkannteKopfzeile ? vorschau.VorschauZeilen.Skip(1) : vorschau.VorschauZeilen;
        var optionen = new List<SpaltenAuswahlOption>();

        for (int spalte = 0; spalte < vorschau.SpaltenAnzahl; spalte++)
        {
            var beispiele = datenZeilen
                .Select(zeile => spalte < zeile.Count ? zeile[spalte] : "")
                .Where(wert => !string.IsNullOrWhiteSpace(wert))
                .Take(3)
                .ToList();

            var kopf = vorschau.ErkannteKopfzeile && spalte < vorschau.VorschauZeilen[0].Count
                ? vorschau.VorschauZeilen[0][spalte]
                : $"Spalte {spalte + 1}";

            var beschreibung = beispiele.Count > 0
                ? $"{kopf} ({string.Join(", ", beispiele)})"
                : kopf;

            optionen.Add(new SpaltenAuswahlOption(spalte, beschreibung));
        }

        return optionen;
    }

    private async void SpeichernButton_Click(object sender, RoutedEventArgs e)
    {
        FehlerText.Text = "";

        if (TabellenListBox.SelectedItem is not TabellenAuswahlOption tabelle)
        {
            FehlerText.Text = "Bitte zuerst die Webseite prüfen und eine Tabelle auswählen.";
            return;
        }

        if (MannschaftListBox.SelectedItem is not SpaltenAuswahlOption mannschaftSpalte || mannschaftSpalte.SpaltenIndex is null)
        {
            FehlerText.Text = "Bitte die Team-/Land-Spalte auswählen.";
            return;
        }

        var punkteSpalte = (PunkteListBox.SelectedItem as SpaltenAuswahlOption)?.SpaltenIndex;

        try
        {
            var eingabe = new RanglistenQuelleErstellenEingabe(
                NameBox.Text,
                UrlBox.Text,
                tabelle.Vorschau.Index,
                null,
                mannschaftSpalte.SpaltenIndex.Value,
                punkteSpalte,
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

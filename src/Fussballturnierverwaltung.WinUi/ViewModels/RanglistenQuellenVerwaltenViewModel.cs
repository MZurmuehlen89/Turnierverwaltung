using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Application.Ranglisten;
using Fussballturnierverwaltung.Application.Ranglisten.Eingaben;
using Fussballturnierverwaltung.Application.Ranglisten.Import;
using Fussballturnierverwaltung.Domain.Entitaeten.Ranglisten;
using Fussballturnierverwaltung.WinUi.Dialoge;
using System.Collections.ObjectModel;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Auswahloption der Tabellen-Liste (Issue #225) — Beschreibung fasst Zeilen-/Spaltenzahl und
/// die erste Vorschauzeile zusammen, damit der Nutzer die richtige Tabelle ohne HTML-Kenntnis per
/// Klick erkennt.</summary>
public record TabellenAuswahlOption(RanglistenTabellenVorschau Vorschau, string Beschreibung);

/// <summary>Auswahloption einer Spalten-Liste — SpaltenIndex null steht für "— keine —" (nur bei der
/// optionalen Punkte-Spalte verwendet).</summary>
public record SpaltenAuswahlOption(int? SpaltenIndex, string Beschreibung);

/// <summary>Datenhaltung des Quellen-Verwalten-Dialogs (#285) — im Gegensatz zum WPF-Original
/// (zwei getrennte Fenster: Liste + separater Anlegen/Bearbeiten-Dialog) hier als ein ContentDialog mit
/// Liste+Formular nach dem app-weiten Muster (RanglistenViewModel etc.): WinUI 3 kann ohnehin immer nur
/// einen ContentDialog gleichzeitig anzeigen, ein zweiter verschachtelter Dialog wäre nicht möglich.</summary>
public partial class RanglistenQuellenVerwaltenViewModel : ObservableObject
{
    private readonly RanglistenQuelleService _service;
    private readonly IDialogService _dialogService;
    private int _populiertGerade;
    private RanglistenQuelle? _bearbeitet;

    [ObservableProperty] private ObservableCollection<RanglistenQuelle> _quellen = [];
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(BearbeitenCommand))]
    [NotifyCanExecuteChangedFor(nameof(LoeschenCommand))]
    private RanglistenQuelle? _ausgewaehlte;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SpeichernCommand))]
    private bool _formularAktiv;

    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _url = "";
    [ObservableProperty] private string? _fehlermeldung;
    [ObservableProperty] private bool _istPruefend;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SpeichernCommand))]
    [NotifyPropertyChangedFor(nameof(TabellenGefunden))]
    private ObservableCollection<TabellenAuswahlOption> _tabellenOptionen = [];

    public bool TabellenGefunden => TabellenOptionen.Count > 0;

    [ObservableProperty] private TabellenAuswahlOption? _ausgewaehlteTabelle;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SpeichernCommand))]
    private ObservableCollection<SpaltenAuswahlOption> _mannschaftSpaltenOptionen = [];

    [ObservableProperty] private SpaltenAuswahlOption? _ausgewaehlteMannschaftSpalte;

    [ObservableProperty] private ObservableCollection<SpaltenAuswahlOption> _punkteSpaltenOptionen = [];
    [ObservableProperty] private SpaltenAuswahlOption? _ausgewaehltePunkteSpalte;

    [ObservableProperty] private bool _kopfzeileUeberspringen;

    public RanglistenQuellenVerwaltenViewModel(RanglistenQuelleService service, IDialogService dialogService)
    {
        _service = service;
        _dialogService = dialogService;
    }

    public async Task LadenAsync()
    {
        var quellen = await _service.AlleAbrufenAsync();
        Quellen = new ObservableCollection<RanglistenQuelle>(quellen);
    }

    [RelayCommand]
    private void Neu()
    {
        Ausgewaehlte = null;
        _bearbeitet = null;
        FormularZuruecksetzen();
        FormularAktiv = true;
    }

    [RelayCommand(CanExecute = nameof(KannBearbeiten))]
    private void Bearbeiten()
    {
        if (Ausgewaehlte is null) return;
        _bearbeitet = Ausgewaehlte;
        _populiertGerade++;
        try
        {
            Name = _bearbeitet.Name;
            Url = _bearbeitet.Url;
            TabellenOptionen = [];
            AusgewaehlteTabelle = null;
            MannschaftSpaltenOptionen = [];
            PunkteSpaltenOptionen = [];
            AusgewaehlteMannschaftSpalte = null;
            AusgewaehltePunkteSpalte = null;
            KopfzeileUeberspringen = _bearbeitet.KopfzeileUeberspringen;
            Fehlermeldung = null;
        }
        finally { _populiertGerade--; }
        FormularAktiv = true;
    }

    [RelayCommand(CanExecute = nameof(KannLoeschen))]
    private async Task Loeschen()
    {
        if (Ausgewaehlte is null) return;
        var bestaetigt = await _dialogService.BestaetigenAsync(
            "Quelle löschen", $"'{Ausgewaehlte.Name}' wirklich löschen?");
        if (!bestaetigt) return;

        await _service.LoeschenAsync(Ausgewaehlte.Id);
        Ausgewaehlte = null;
        await LadenAsync();
    }

    [RelayCommand]
    private void FormularAbbrechen()
    {
        FormularAktiv = false;
        _bearbeitet = null;
        FormularZuruecksetzen();
    }

    private void FormularZuruecksetzen()
    {
        _populiertGerade++;
        try
        {
            Name = "";
            Url = "";
            TabellenOptionen = [];
            AusgewaehlteTabelle = null;
            MannschaftSpaltenOptionen = [];
            PunkteSpaltenOptionen = [];
            AusgewaehlteMannschaftSpalte = null;
            AusgewaehltePunkteSpalte = null;
            KopfzeileUeberspringen = true;
            Fehlermeldung = null;
        }
        finally { _populiertGerade--; }
    }

    [RelayCommand]
    private async Task WebseitePruefen()
    {
        Fehlermeldung = null;
        if (string.IsNullOrWhiteSpace(Url))
        {
            Fehlermeldung = "Bitte zuerst eine URL eingeben.";
            return;
        }

        IstPruefend = true;
        try
        {
            var tabellen = await _service.WebseitePruefenAsync(Url.Trim());
            if (tabellen.Count == 0)
            {
                Fehlermeldung = "Auf der Seite wurden keine Tabellen gefunden.";
                TabellenOptionen = [];
                return;
            }

            TabellenOptionen = new ObservableCollection<TabellenAuswahlOption>(tabellen.Select(TabellenBeschreiben));

            var vorauswahlIndex = _bearbeitet is not null
                ? TabellenOptionen.ToList().FindIndex(t => t.Vorschau.Index == _bearbeitet.TabellenIndex)
                : -1;
            AusgewaehlteTabelle = vorauswahlIndex >= 0 ? TabellenOptionen[vorauswahlIndex] : TabellenOptionen[0];
        }
        catch (Exception ex)
        {
            Fehlermeldung = $"Seite konnte nicht geladen werden: {ex.Message}";
        }
        finally
        {
            IstPruefend = false;
        }
    }

    partial void OnAusgewaehlteTabelleChanged(TabellenAuswahlOption? value)
    {
        if (value is null)
        {
            MannschaftSpaltenOptionen = [];
            PunkteSpaltenOptionen = [];
            return;
        }

        var vorschau = value.Vorschau;
        var spalten = SpaltenBeschreiben(vorschau);

        MannschaftSpaltenOptionen = new ObservableCollection<SpaltenAuswahlOption>(spalten);
        PunkteSpaltenOptionen = new ObservableCollection<SpaltenAuswahlOption>(
            new[] { new SpaltenAuswahlOption(null, "— keine —") }.Concat(spalten));

        var mannschaftVorauswahl = _bearbeitet is not null && vorschau.Index == _bearbeitet.TabellenIndex
            ? spalten.FindIndex(s => s.SpaltenIndex == _bearbeitet.MannschaftSpalte)
            : -1;
        AusgewaehlteMannschaftSpalte = mannschaftVorauswahl >= 0 ? MannschaftSpaltenOptionen[mannschaftVorauswahl] : MannschaftSpaltenOptionen[0];

        if (_bearbeitet is not null && vorschau.Index == _bearbeitet.TabellenIndex && _bearbeitet.PunkteSpalte is not null)
        {
            var punkteVorauswahl = PunkteSpaltenOptionen.ToList().FindIndex(s => s.SpaltenIndex == _bearbeitet.PunkteSpalte);
            AusgewaehltePunkteSpalte = punkteVorauswahl >= 0 ? PunkteSpaltenOptionen[punkteVorauswahl] : PunkteSpaltenOptionen[0];
        }
        else
        {
            AusgewaehltePunkteSpalte = PunkteSpaltenOptionen[0];
        }

        if (_populiertGerade == 0)
            KopfzeileUeberspringen = _bearbeitet is not null && vorschau.Index == _bearbeitet.TabellenIndex
                ? _bearbeitet.KopfzeileUeberspringen
                : vorschau.ErkannteKopfzeile;
    }

    private static TabellenAuswahlOption TabellenBeschreiben(RanglistenTabellenVorschau vorschau)
    {
        var ersteZeile = vorschau.VorschauZeilen.Count > 0 ? string.Join(" | ", vorschau.VorschauZeilen[0]) : "";
        var beschreibung = $"Tabelle {vorschau.Index + 1}: {vorschau.ZeilenAnzahl} Zeilen × {vorschau.SpaltenAnzahl} Spalten"
            + (string.IsNullOrWhiteSpace(ersteZeile) ? "" : $" — {ersteZeile}");
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

            var beschreibung = beispiele.Count > 0 ? $"{kopf} ({string.Join(", ", beispiele)})" : kopf;
            optionen.Add(new SpaltenAuswahlOption(spalte, beschreibung));
        }

        return optionen;
    }

    [RelayCommand(CanExecute = nameof(KannSpeichern))]
    private async Task Speichern()
    {
        Fehlermeldung = null;
        if (AusgewaehlteTabelle is null)
        {
            Fehlermeldung = "Bitte zuerst die Webseite prüfen und eine Tabelle auswählen.";
            return;
        }
        if (AusgewaehlteMannschaftSpalte is null || AusgewaehlteMannschaftSpalte.SpaltenIndex is null)
        {
            Fehlermeldung = "Bitte die Team-/Land-Spalte auswählen.";
            return;
        }

        try
        {
            var eingabe = new RanglistenQuelleErstellenEingabe(
                Name, Url, AusgewaehlteTabelle.Vorschau.Index, null,
                AusgewaehlteMannschaftSpalte.SpaltenIndex.Value,
                AusgewaehltePunkteSpalte?.SpaltenIndex,
                KopfzeileUeberspringen);

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

            FormularAktiv = false;
            _bearbeitet = null;
            FormularZuruecksetzen();
            await LadenAsync();
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
    }

    private bool KannBearbeiten() => Ausgewaehlte is not null;
    private bool KannLoeschen() => Ausgewaehlte is not null;
    private bool KannSpeichern() => !string.IsNullOrWhiteSpace(Name) && !string.IsNullOrWhiteSpace(Url);
}

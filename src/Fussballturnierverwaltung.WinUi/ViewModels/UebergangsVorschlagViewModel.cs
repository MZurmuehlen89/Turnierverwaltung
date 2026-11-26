using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Application.Spielplan.Eingaben;
using Fussballturnierverwaltung.Application.Turniere;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Eine wählbare Möglichkeit eines Vorschlags mit fertigem Anzeigetext und Herkunft.</summary>
public sealed record VorschlagsOptionAnzeige(VorschlagsOption Option, string Beschreibung, string Herkunft);

/// <summary>Vorschläge für eingehende Übergangsregeln (#167): für die gewählte Runde die Optionen mit Hinweis und
/// „Übernehmen“/„Selbst anlegen“, für alle Runden die Marke in der Rundenliste und der Sammelknopf „Vorschläge
/// übernehmen“. Kind-ViewModel von <see cref="RundenViewModel"/>, das Datenhalter bleibt: übernommen wird über dessen
/// <see cref="RundenViewModel.UebergangHinzufuegen"/>, nichts davon ist gespeichert, bevor „Speichern“ läuft.</summary>
public partial class UebergangsVorschlagViewModel : ObservableObject
{
    private readonly RundenViewModel _rundenVm;
    private IReadOnlyList<VorlagenMuster> _vorlagen = [];
    private IReadOnlyList<UebergangsVorschlag> _alle = [];

    public UebergangsVorschlagViewModel(RundenViewModel rundenVm)
    {
        _rundenVm = rundenVm;
        // Vor dem Übergangsregel-Editor angemeldet (er entsteht danach): sein Kopf liest schon den neuen Stand.
        _rundenVm.UebergaengeGeaendert += Aktualisieren;
        _rundenVm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(RundenViewModel.AusgewaehlteRunde)) AuswahlAktualisieren();
        };
    }

    /// <summary>Optionen für die gewählte Runde; mehrere = mehrdeutig, keine ist vorgewählt.</summary>
    public ObservableCollection<VorschlagsOptionAnzeige> Optionen { get; } = [];

    [ObservableProperty] private string? _hinweis;

    public bool HatVorschlag => Optionen.Count > 0;
    public bool HatHinweis => Hinweis is not null;

    /// <summary>Kopfzeilen-Teil des Übergänge-Bereichs der gewählten Runde, sonst <c>null</c>.</summary>
    public string? KopfText => HatVorschlag ? "1 Vorschlag" : null;

    private IReadOnlyList<VorlagenMuster>? _geladeneVorlagen;

    /// <summary>Lädt die Vorlagen als Muster für den Abgleich (Quelle A) beim ersten Mal und merkt sie; die eigene
    /// Instanz zählt nicht als Vorlage ihrer selbst. Ein Ladefehler lässt nur die Rechenregel übrig.</summary>
    public async Task VorlagenLadenAsync(UebergangsVorlagenQuelle quelle, int eigeneInstanzId, ILogger logger)
    {
        try
        {
            _geladeneVorlagen ??= await quelle.LadenAsync();
        }
        catch (Exception ex)
        {
            // Nicht gemerkt: der nächste Turnierwechsel versucht es noch einmal; bis dahin gilt nur die Rechenregel.
            logger.LogWarning(ex, "Vorlagen für den Übergangsvorschlag konnten nicht geladen werden");
        }
        _vorlagen = (_geladeneVorlagen ?? []).Where(v => v.InstanzId != eigeneInstanzId).ToList();
    }

    private void Aktualisieren()
    {
        var runden = _rundenVm.RundenFuerBerechnung();
        _alle = UebergangsVorschlaege.Berechnen(runden, _rundenVm.UebergangsregelnZuEingaben(), _vorlagen);
        foreach (var (zeile, index) in _rundenVm.Runden.Select((z, i) => (z, i)))
        {
            zeile.VorschlagVorhanden = _alle.Any(v => v.ZielReihenfolge == index + 1);
            zeile.HatEingehendeRegel = index > 0 && _rundenVm.AlleUebergaenge.Any(b => b.ZielRunde == zeile);
        }
        AuswahlAktualisieren();
        UebernehmenAllerCommand.NotifyCanExecuteChanged();
    }

    private void AuswahlAktualisieren()
    {
        var index = _rundenVm.AusgewaehlteRunde is { } runde ? _rundenVm.Runden.IndexOf(runde) : -1;
        var vorschlag = _alle.FirstOrDefault(v => v.ZielReihenfolge == index + 1);

        Optionen.Clear();
        if (vorschlag is not null)
        {
            var runden = _rundenVm.RundenFuerBerechnung();
            foreach (var option in vorschlag.Optionen)
                Optionen.Add(new VorschlagsOptionAnzeige(option, UebergangsVorschlagText.Beschreiben(option, runden),
                    $"nach {string.Join(", ", option.Herkuenfte)}"));
        }
        Hinweis = vorschlag?.Hinweis;
        OnPropertyChanged(nameof(HatVorschlag));
        OnPropertyChanged(nameof(HatHinweis));
        OnPropertyChanged(nameof(KopfText));
    }

    // ── Befehle ───────────────────────────────────────────────────────────────

    [RelayCommand]
    private void Uebernehmen(VorschlagsOptionAnzeige option) => RegelnAnlegen(option.Option.Regeln);

    /// <summary>Öffnet das leere Regelformular wie „+ Neu“.</summary>
    [RelayCommand]
    private void SelbstAnlegen() => _rundenVm.UebergangsregelEditor.NeueEingehendeRegelCommand.Execute(null);

    /// <summary>Ribbon: übernimmt alle eindeutigen Vorschläge (genau eine Option) auf einmal; mehrdeutige bleiben zur Wahl.</summary>
    [RelayCommand(CanExecute = nameof(HatEindeutigenVorschlag))]
    private void UebernehmenAller()
    {
        var regeln = _alle.Where(v => v.Optionen.Count == 1).SelectMany(v => v.Optionen[0].Regeln).ToList();
        RegelnAnlegen(regeln);
    }

    private bool HatEindeutigenVorschlag() => _alle.Any(v => v.Optionen.Count == 1);

    // ── Regeln über den vorhandenen Editor anlegen ────────────────────────────

    private void RegelnAnlegen(IEnumerable<UebergangsregelPositionsEingabe> regeln)
    {
        var runden = _rundenVm.Runden;
        foreach (var gruppe in regeln.GroupBy(r => (r.QuellReihenfolge, r.ZielReihenfolge)))
        {
            var quelle = runden[gruppe.Key.QuellReihenfolge - 1];
            var ziel = runden[gruppe.Key.ZielReihenfolge - 1];
            var zeilen = gruppe.Select(r => new UebergangsregelZeile
            {
                GruppeIndex = r.GruppeIndex,
                PlatzierungVon = r.PlatzierungVon,
                PlatzierungBis = r.PlatzierungBis,
                AnzahlBeste = r.AnzahlBeste
            }).ToList();

            if (_rundenVm.AlleUebergaenge.FirstOrDefault(b => b.QuellRunde == quelle && b.ZielRunde == ziel) is { } vorhanden)
                foreach (var zeile in zeilen) vorhanden.ZeileHinzufuegen(zeile);
            else
                _rundenVm.UebergangHinzufuegen(UebergangsregelBlock.AusBestehendenZeilenErstellen(quelle, ziel, zeilen));
        }
    }
}

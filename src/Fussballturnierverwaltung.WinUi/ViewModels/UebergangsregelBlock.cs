using CommunityToolkit.Mvvm.ComponentModel;
using Fussballturnierverwaltung.Domain.Enums;
using System.Collections.ObjectModel;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Eine Übergangsregel (Quelle→Ziel + Detailzeilen) — zentrale Datenhaltung in
/// <see cref="RundenViewModel"/>.AlleUebergaenge. Bearbeitet wird sie AUSSCHLIESSLICH
/// über das stabile Regelformular in <see cref="UebergangsregelEditorViewModel"/> (Populier-/Zurückschreib-
/// Muster) — dieser Typ selbst enthält keine TwoWay-Bindungspunkte für Quelle/Ziel (siehe #284-Redesign-
/// Historie: eine frühere Fassung mit eingebetteter ComboBox pro Karte hat bei jedem Rundenfokus-Wechsel
/// wiederholt zu Datenkorruption geführt, weil die Karte selbst bei jedem Wechsel neu aufgebaut wurde). Nur
/// die einzelnen <see cref="Zeilen"/> (PlatzierungVon/Bis, Sieger/Verlierer) bleiben direkt TwoWay-editierbar —
/// das ist unproblematisch, da sie in TextBoxen/CheckBoxen ohne eigenen Selektions-State liegen.</summary>
public partial class UebergangsregelBlock : ObservableObject
{
    private bool _wirdInitialisiert;

    /// <summary>Feuert bei jeder inhaltlichen Änderung (Quelle/Ziel/Zeilen) — <see cref="RundenViewModel"/>
    /// aktualisiert daraufhin IsDirty, die Validierung und feuert <see cref="RundenViewModel.UebergaengeGeaendert"/>.</summary>
    public event Action? Geaendert;

    [ObservableProperty] private RundeZeile? _quellRunde;
    [ObservableProperty] private RundeZeile? _zielRunde;
    [ObservableProperty] private ObservableCollection<UebergangsregelZeile> _zeilen = [];

    /// <summary>Vom Übergangsregel-Editor gesetzt, wenn dieser Block gerade im Regelformular bearbeitet wird — reine
    /// Anzeige-Markierung für die Hervorhebung in den Eingehend-/Ausgehend-Listen, keine Nutzereingabe.</summary>
    [ObservableProperty] private bool _istAusgewaehlt;

    public static UebergangsregelBlock NeuLeer(RundeZeile? quelle, RundeZeile? ziel)
    {
        var block = new UebergangsregelBlock();
        block._wirdInitialisiert = true;
        try
        {
            block.QuellRunde = quelle;
            block.ZielRunde = ziel;
        }
        finally
        {
            block._wirdInitialisiert = false;
        }
        if (quelle is not null) block.BefuelleStandardZeilen();
        return block;
    }

    public static UebergangsregelBlock AusBestehendenZeilenErstellen(
        RundeZeile quelle, RundeZeile ziel, IEnumerable<UebergangsregelZeile> zeilen)
    {
        var block = new UebergangsregelBlock();
        block._wirdInitialisiert = true;
        try
        {
            block.QuellRunde = quelle;
            block.ZielRunde = ziel;
            block.Zeilen = new ObservableCollection<UebergangsregelZeile>(zeilen);
        }
        finally
        {
            block._wirdInitialisiert = false;
        }
        foreach (var z in block.Zeilen)
            block.ZeileBeobachten(z);
        return block;
    }

    partial void OnQuellRundeChanged(RundeZeile? value)
    {
        if (!_wirdInitialisiert) Geaendert?.Invoke();
        if (_wirdInitialisiert || value is null || Zeilen.Count > 0) return;
        BefuelleStandardZeilen();
    }

    partial void OnZielRundeChanged(RundeZeile? value)
    {
        if (!_wirdInitialisiert) Geaendert?.Invoke();
    }

    private void BefuelleStandardZeilen()
    {
        var quelle = QuellRunde;
        if (quelle is null) return;

        void ZeileHinzufuegen(int? gruppeIndex, int von, int bis)
        {
            var zeile = new UebergangsregelZeile { GruppeIndex = gruppeIndex, PlatzierungVon = von, PlatzierungBis = bis };
            ZeileBeobachten(zeile);
            Zeilen.Add(zeile);
        }

        if (quelle.Rundenart == Rundenart.Gruppenphase && quelle.GruppenAnzahl is > 0)
        {
            var bis = Math.Max(1, quelle.AusgangsMannschaftsanzahl / quelle.GruppenAnzahl.Value);
            for (int i = 1; i <= quelle.GruppenAnzahl.Value; i++)
                ZeileHinzufuegen(i, 1, bis);
        }
        else if (quelle.Rundenart is Rundenart.KoRunde or Rundenart.Finale or Rundenart.SpielUmPlatzDrei)
        {
            var anzahlSpiele = Math.Max(1, quelle.EingangsMannschaftsanzahl / 2);
            for (int i = 1; i <= anzahlSpiele; i++)
                ZeileHinzufuegen(i, 1, 1);
        }
        else
        {
            ZeileHinzufuegen(null, 1, 1);
        }
        Geaendert?.Invoke();
    }

    /// <summary>Fügt eine gruppenübergreifende Rangfolge-Regel-Zeile hinzu (z.B. "2 beste Gruppendritte",
    /// EM/WM-Klassiker) — nur bei Gruppenphase als Quellrunde sinnvoll. Anders als die per-Gruppe-Zeilen aus
    /// <see cref="BefuelleStandardZeilen"/> bleibt <see cref="UebergangsregelZeile.GruppeIndex"/> hier null,
    /// <see cref="UebergangsregelZeile.AnzahlBeste"/> markiert die Zeile als gruppenübergreifend (siehe
    /// <see cref="UebergangsregelEditorViewModel.GruppenuebergreifendeZeilen"/>). Default "Platz 3, beste 1"
    /// analog WPF-Original.</summary>
    public void GruppenuebergreifendeZeileHinzufuegen()
    {
        var zeile = new UebergangsregelZeile { GruppeIndex = null, AnzahlBeste = 1, PlatzierungVon = 3, PlatzierungBis = 3 };
        ZeileBeobachten(zeile);
        Zeilen.Add(zeile);
        Geaendert?.Invoke();
    }

    /// <summary>Eingaben einer Zeile sind Änderungen; ihre Prüffehler (#601) setzt erst die Prüfung, die daraus folgt –
    /// sie zählten sonst als neue Änderung und lösten die Prüfung endlos wieder aus.</summary>
    private void ZeileBeobachten(UebergangsregelZeile zeile) =>
        zeile.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(UebergangsregelZeile.Meldungen)) Geaendert?.Invoke();
        };

    /// <summary>Entfernt eine einzelne Zeile (aktuell nur für gruppenübergreifende Regeln genutzt — die
    /// per-Gruppe-Zeilen werden weiterhin automatisch aus der Quellrundenstruktur abgeleitet, nicht einzeln
    /// entfernbar).</summary>
    public void ZeileEntfernen(UebergangsregelZeile zeile)
    {
        Zeilen.Remove(zeile);
        Geaendert?.Invoke();
    }
}

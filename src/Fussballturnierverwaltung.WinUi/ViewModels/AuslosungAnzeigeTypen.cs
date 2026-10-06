using CommunityToolkit.Mvvm.ComponentModel;
using Fussballturnierverwaltung.Domain.Entitaeten.Ranglisten;
using Fussballturnierverwaltung.Domain.Enums;
using System.Collections.ObjectModel;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

// ── Darstellungstypen — 1:1 aus Wpf/ViewModels/AuslosungViewModel.cs portiert, ohne TV-Modus (#303-Backlog)
// und ohne Auslosungsart.Manuell (bleibt beim bestehenden VorbelegungViewModel, #297) ──────────────────────

public class LostopfViewModel : ObservableObject
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public int Reihenfolge { get; init; }
    public ObservableCollection<TeilnehmerAnzeige> Teilnehmer { get; init; } = [];
}

public record ErgebnisAnzeige(int PaarungsNummer, string HeimName, string AuswaertsName, bool IstFreilos,
    byte[]? HeimWappen = null, byte[]? AuswaertsWappen = null);
public record GruppenTeamAnzeige(string Name, byte[]? Wappen);
public record GruppenErgebnisAnzeige(int GruppenNummer, string GruppenName, IReadOnlyList<GruppenTeamAnzeige> Teams);

public class RegelViewModel : ObservableObject
{
    private readonly Func<bool, Task> _callback;
    private bool _istAktiv;

    public AuslosungsRegelArt Regelart { get; init; }
    public string Anzeige { get; init; } = "";
    public string Beschreibung { get; init; } = "";

    /// <summary>Warum die Regel bei der aktuellen Auslosungsart/Rundenart nicht wirkt — dann ausgegraut (#331).</summary>
    public string? SperrHinweis { get; set; }

    /// <summary>Warum die Regel bei der aktuellen Auslosungsart ohnehin immer gilt — dann ausgegraut und angehakt.</summary>
    public string? ImmerAktivHinweis { get; set; }

    public bool IstAktiv
    {
        get => _istAktiv;
        set { if (SetProperty(ref _istAktiv, value)) _ = _callback(value); }
    }

    /// <summary>Setzt den Zustand ohne Speicher-Callback — für bereits gespeicherte Nebenwirkungen (gegenseitiger
    /// Ausschluss der Heimrecht-Regeln).</summary>
    public void OhneSpeichernSetzen(bool aktiv) => SetProperty(ref _istAktiv, aktiv, nameof(IstAktiv));

    public RegelViewModel(AuslosungsRegelArt art, string anzeige, string beschreibung, bool initial, Func<bool, Task> callback)
    {
        Regelart = art; Anzeige = anzeige; Beschreibung = beschreibung; _istAktiv = initial; _callback = callback;
    }
}

public class VerbandsQuoteAnzeigeViewModel
{
    public string VerbandName { get; init; } = "";
    public int Aktuell { get; init; }
    public int Anzahl { get; init; }

    public bool UeberAnzahl => Aktuell > Anzahl;
    public bool UnterAnzahl => Aktuell < Anzahl;
    public string AnzeigeText => $"{Aktuell} / {Anzahl}";
}

/// <summary>Live-Anzeige einer Landes-Quote im Auslosungseditor (Issue #189), analog
/// <see cref="VerbandsQuoteAnzeigeViewModel"/>.</summary>
public class LandesQuoteAnzeigeViewModel
{
    public string LandName { get; init; } = "";
    public int Aktuell { get; init; }
    public int Anzahl { get; init; }

    public bool UeberAnzahl => Aktuell > Anzahl;
    public bool UnterAnzahl => Aktuell < Anzahl;
    public string AnzeigeText => $"{Aktuell} / {Anzahl}";
}

public record AuslosungsartOption(string Anzeige, Auslosungsart Wert);
public record RanglisteOption(string Anzeige, Rangliste? Wert);
public record TeilnehmerLandOption(string Anzeige, int? LandId);

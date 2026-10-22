using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

// ── Ziehung von Hand (#482) ──
// Feste Anzeige-Objekte: sie entstehen einmal beim Start und ändern danach nur ihre Eigenschaften — so baut WinUI
// nach einem Zug nichts neu auf (#428: die Aufbauzeit wächst mit der Elementzahl). Anklickbar ist, was der aktuelle
// Schritt anbietet und nicht gesperrt ist; gesperrt heißt ausgegraut mit Grund als Tooltip.

/// <summary>Wahl-Zustand eines anklickbaren Elements (Team, Gruppe, Platz) — als Teil des Elements, nicht als
/// Basisklasse.</summary>
public sealed partial class HandZiehungWahl(int wert, Action<int> waehlen) : ObservableObject
{
    [ObservableProperty] private bool _istWaehlbar;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Opacity))]
    private string? _sperrGrund;

    public double Opacity => SperrGrund is null ? 1.0 : 0.4;

    public void Waehlen()
    {
        if (IstWaehlbar) waehlen(wert);
    }

    internal void Setzen(bool waehlbar, string? sperrGrund)
    {
        IstWaehlbar = waehlbar;
        SperrGrund  = sperrGrund;
    }
}

/// <summary>Ein Team im Lostopf — gezogene bleiben stehen und werden nur ausgegraut, damit nichts verrutscht.</summary>
public sealed partial class HandZiehungTeam(int index, string name, string? platzhalter, byte[]? wappen, Action<int> waehlen)
    : ObservableObject
{
    public HandZiehungWahl Wahl { get; } = new(index, waehlen);
    public string Name { get; } = name;
    /// <summary>Der Platzhalter, hinter dem die Mannschaft steht (#483) — grau dahinter.</summary>
    public string? Platzhalter { get; } = platzhalter;
    public byte[]? Wappen { get; } = wappen;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GezogenOpacity))]
    private bool _istGezogen;

    public double GezogenOpacity => IstGezogen ? 0.35 : 1.0;
}

public sealed class HandZiehungTopf
{
    public string Name { get; init; } = "";
    public IReadOnlyList<HandZiehungTeam> Teams { get; init; } = [];
}

/// <summary>Ein Platz einer Gruppe; beim Positions-Zug anklickbar.</summary>
public sealed partial class HandZiehungPlatz(int position, Action<int> waehlen) : ObservableObject
{
    public HandZiehungWahl Wahl { get; } = new(position, waehlen);
    public int Position { get; } = position;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Anzeige))]
    [NotifyPropertyChangedFor(nameof(Wappen))]
    [NotifyPropertyChangedFor(nameof(BefuelltOpacity))]
    private HandZiehungTeam? _team;

    public string Anzeige => Team?.Name ?? "…";
    public byte[]? Wappen => Team?.Wappen;
    public double BefuelltOpacity => Team is null ? 0.4 : 1.0;
}

/// <summary>Eine Gruppe; beim Gruppen-Zug anklickbar, beim Positions-Zug als Ziel hervorgehoben.</summary>
public sealed partial class HandZiehungGruppe(int nr, string name, Action<int> waehlen) : ObservableObject
{
    public HandZiehungWahl Wahl { get; } = new(nr, waehlen);
    public string Name { get; } = name;
    public ObservableCollection<HandZiehungPlatz> Plaetze { get; } = [];

    [ObservableProperty] private bool _istZiel;
}

/// <summary>Eine Paarung der K.-o.-Ziehung; die gerade gezogene ist hervorgehoben.</summary>
public sealed partial class HandZiehungPaarung : ObservableObject
{
    public int PaarungsNr { get; init; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HeimName))]
    [NotifyPropertyChangedFor(nameof(HeimWappen))]
    [NotifyPropertyChangedFor(nameof(HeimOpacity))]
    private HandZiehungTeam? _heim;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AuswaertsName))]
    [NotifyPropertyChangedFor(nameof(AuswaertsWappen))]
    [NotifyPropertyChangedFor(nameof(AuswaertsOpacity))]
    private HandZiehungTeam? _auswaerts;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AuswaertsName))]
    [NotifyPropertyChangedFor(nameof(AuswaertsOpacity))]
    private bool _istFreilos;

    [ObservableProperty] private bool _istOffen;

    public string HeimName      => Heim?.Name ?? "…";
    public byte[]? HeimWappen   => Heim?.Wappen;
    public string AuswaertsName => IstFreilos ? "Freilos" : Auswaerts?.Name ?? "…";
    public byte[]? AuswaertsWappen => Auswaerts?.Wappen;
    public double HeimOpacity      => Heim is null ? 0.4 : 1.0;
    public double AuswaertsOpacity => Auswaerts is null && !IstFreilos ? 0.4 : 1.0;
}

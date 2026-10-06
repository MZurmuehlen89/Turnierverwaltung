using CommunityToolkit.Mvvm.ComponentModel;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

// ── Gegner-Matrix der Ligaphase (#362) ─────────────────────────────────────
// Vorbild: UEFA-TV-Grafik der CL-Ligaphasen-Auslosung — je Lostopf eine Matrix, Zeilen = Teams des Topfs,
// Spalten = je Gegner-Topf ein Heim- und ein Auswärtsgegner. Hervorhebung als eigene Property je Zelle/Zeile
// statt einer pro Fokus neu berechneten ItemsSource (siehe WinUi-Fallstricke, #284).

public class GegnerMatrixZelle : ObservableObject
{
    /// <summary>Team-Schlüssel (MannschaftId bzw. Platzhaltername), null = leere Zelle.</summary>
    public string? Schluessel { get; init; }
    public string Name { get; init; } = "";
    public string Kurzname { get; init; } = "";
    public string? Land { get; init; }
    public byte[]? Wappen { get; init; }
    private bool _istHervorgehoben;
    public bool IstHervorgehoben { get => _istHervorgehoben; set => SetProperty(ref _istHervorgehoben, value); }
    /// <summary>TV-Modus (#364): noch nicht aufgedeckte Zellen zeigen "…" statt des Gegners.</summary>
    private bool _istAufgedeckt = true;
    public bool IstAufgedeckt { get => _istAufgedeckt; set => SetProperty(ref _istAufgedeckt, value); }
}

public class GegnerMatrixZeile : ObservableObject
{
    public GegnerMatrixZelle Team { get; init; } = new();
    public IReadOnlyList<GegnerMatrixZelle> Gegner { get; init; } = [];
    private bool _istHervorgehoben;
    public bool IstHervorgehoben { get => _istHervorgehoben; set => SetProperty(ref _istHervorgehoben, value); }
    /// <summary>TV-Modus (#364): noch nicht gezogene Teams erscheinen abgeblendet.</summary>
    private bool _istGezogen = true;
    public bool IstGezogen
    {
        get => _istGezogen;
        set { if (SetProperty(ref _istGezogen, value)) OnPropertyChanged(nameof(TeamOpacity)); }
    }
    public double TeamOpacity => IstGezogen ? 1.0 : 0.45;
    /// <summary>TV-Modus (#364): Haken vor dem Namen, sobald das Team während der Live-Ziehung gezogen wurde
    /// (bleibt in der Ergebnisanzeige aus).</summary>
    private bool _zeigeGezogenHaken;
    public bool ZeigeGezogenHaken { get => _zeigeGezogenHaken; set => SetProperty(ref _zeigeGezogenHaken, value); }
}

public record GegnerMatrixSpalte(string TopfName, string Seite);

public record GegnerMatrixTopf(string Name, IReadOnlyList<GegnerMatrixSpalte> Spalten, IReadOnlyList<GegnerMatrixZeile> Zeilen);

public record FokusGegnerEintrag(string TopfName, GegnerMatrixZelle Heim, GegnerMatrixZelle Auswaerts);

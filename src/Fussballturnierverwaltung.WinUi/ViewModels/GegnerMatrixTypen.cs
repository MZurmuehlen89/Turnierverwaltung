using CommunityToolkit.Mvvm.ComponentModel;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

// ── Gegner-Matrix der Ligaphase (#362) ─────────────────────────────────────
// Vorbild: UEFA-TV-Grafik der CL-Ligaphasen-Auslosung — je Lostopf eine Matrix, Zeilen = Teams des Topfs,
// Spalten = je Gegner-Topf ein Heim- und ein Auswärtsgegner. Bei einem Gegner je Topf (UECL, #469) kompakt: eine
// Spalte je Topf, Heim/Auswärts steht in der Zelle, Topf-Paare mit Abstand. Hervorhebung als eigene Property je
// Zelle/Zeile statt einer pro Fokus neu berechneten ItemsSource (siehe WinUi-Fallstricke, #284).

public class GegnerMatrixZelle : ObservableObject
{
    /// <summary>Team-Schlüssel (MannschaftId bzw. Platzhaltername), null = leere Zelle.</summary>
    public string? Schluessel { get; init; }
    public string Name { get; init; } = "";
    public string Kurzname { get; init; } = "";
    public string? Land { get; init; }
    public byte[]? Wappen { get; init; }
    /// <summary>„Heim“ bzw. „Auswärts“ aus Sicht des Zeilen-Teams, null = leere Zelle.</summary>
    public string? Seite { get; init; }
    /// <summary>Kompakte Matrix (#469): die Seite steht als „H“/„A“ in der Zelle statt im Spaltenkopf.</summary>
    public bool ZeigeSeite { get; init; }
    /// <summary>Abstand zur nächsten Zelle — in der kompakten Matrix breiter nach jedem Topf-Paar.</summary>
    public Microsoft.UI.Xaml.Thickness Rand { get; init; } = new(0, 0, 2, 0);
    private bool _istHervorgehoben;
    public bool IstHervorgehoben { get => _istHervorgehoben; set => SetProperty(ref _istHervorgehoben, value); }
    /// <summary>TV-Modus (#364): noch nicht aufgedeckte Zellen zeigen "…" statt des Gegners.</summary>
    private bool _istAufgedeckt = true;
    public bool IstAufgedeckt
    {
        get => _istAufgedeckt;
        set
        {
            if (!SetProperty(ref _istAufgedeckt, value)) return;
            OnPropertyChanged(nameof(Anzeige));
            OnPropertyChanged(nameof(AnzeigeOpacity));
            OnPropertyChanged(nameof(SeiteKurz));
        }
    }
    /// <summary>Text der Matrix-Zelle — ein Textfeld für beide Zustände statt zweier umgeschalteter (#428).</summary>
    public string Anzeige => IstAufgedeckt ? Kurzname : "…";
    public double AnzeigeOpacity => IstAufgedeckt ? 1.0 : 0.4;
    /// <summary>„H“/„A“ vor dem Gegner, nur in der kompakten Matrix und erst nach dem Aufdecken.</summary>
    public string? SeiteKurz => ZeigeSeite && IstAufgedeckt && Seite is { Length: > 0 } s ? s[..1] : null;
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

/// <param name="Seite">„Heim“/„Auswärts“, in der kompakten Matrix leer.</param>
public record GegnerMatrixSpalte(string TopfName, string Seite, Microsoft.UI.Xaml.Thickness Rand);

/// <param name="IstKompakt">Eine Spalte je Topf statt je Topf Heim und Auswärts (#469).</param>
public record GegnerMatrixTopf(
    string Name, IReadOnlyList<GegnerMatrixSpalte> Spalten, IReadOnlyList<GegnerMatrixZeile> Zeilen, bool IstKompakt);

/// <summary>Eine Zeile der Gegnerliste des fokussierten Teams: je Gegner-Topf Heim- und Auswärtsgegner, in der
/// kompakten Matrix (#469) der eine Gegner in <paramref name="Heim"/> und die Seite in <paramref name="Seite"/>.</summary>
public record FokusGegnerEintrag(string TopfName, GegnerMatrixZelle Heim, GegnerMatrixZelle Auswaerts, string? Seite = null)
{
    public bool HatSeite => Seite is not null;
}

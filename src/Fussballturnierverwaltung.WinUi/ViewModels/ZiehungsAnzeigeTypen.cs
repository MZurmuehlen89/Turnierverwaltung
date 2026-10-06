using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

// ── TV-Modus: Live-Ziehungsanzeige (#303, Issue #5) ──
// WinUI3 kennt kein Style.Triggers/DataTrigger (siehe WinUi/CLAUDE.md-Fallstricke) — das WPF-Original graut
// gezogene/unbefüllte Slots per DataTrigger auf den Foreground aus, hier stattdessen direkt gebundene
// *Opacity-Properties (Precedent: SpielberichtControl aus #302).

public class AnimationsGruppenSlot : ObservableObject
{
    public int Position { get; init; }
    private string _anzeige = "…";
    public string Anzeige
    {
        get => _anzeige;
        set { if (SetProperty(ref _anzeige, value)) { OnPropertyChanged(nameof(Befuellt)); OnPropertyChanged(nameof(BefuelltOpacity)); } }
    }
    public bool Befuellt => _anzeige != "…";
    public double BefuelltOpacity => Befuellt ? 1.0 : 0.4;
    private byte[]? _wappen;
    public byte[]? Wappen { get => _wappen; set => SetProperty(ref _wappen, value); }
    /// <summary>Fokus-Ansicht (#408): der gerade befüllte Platz ist hervorgehoben.</summary>
    private bool _istNeu;
    public bool IstNeu { get => _istNeu; set => SetProperty(ref _istNeu, value); }
}

public class AnimationsGruppe : ObservableObject
{
    public int GruppenNummer { get; init; }
    public string GruppenName { get; init; } = "";
    /// <summary>Gruppenbuchstabe für die Mini-Kachel der Fokus-Ansicht (#408).</summary>
    public string Kurzname { get; init; } = "";
    public ObservableCollection<AnimationsGruppenSlot> Slots { get; init; } = [];
    /// <summary>Fokus-Ansicht (#408): Gruppe, in die das gerade gezogene Team kommt.</summary>
    private bool _istZiel;
    public bool IstZiel { get => _istZiel; set => SetProperty(ref _istZiel, value); }
    public string FuellstandText => $"{Slots.Count(s => s.Befuellt)}/{Slots.Count}";
    public void FuellstandAktualisieren() => OnPropertyChanged(nameof(FuellstandText));
}

public class AnimationsPaarung : ObservableObject
{
    public int PaarungsNr { get; init; }
    public bool IstFreilos { get; init; }
    private string _heim = "…";
    public string Heim
    {
        get => _heim;
        set { if (SetProperty(ref _heim, value)) { OnPropertyChanged(nameof(HeimBefuellt)); OnPropertyChanged(nameof(HeimBefuelltOpacity)); } }
    }
    public bool HeimBefuellt => _heim != "…";
    public double HeimBefuelltOpacity => HeimBefuellt ? 1.0 : 0.4;
    private byte[]? _heimWappen;
    public byte[]? HeimWappen { get => _heimWappen; set => SetProperty(ref _heimWappen, value); }
    private string _auswaerts = "…";
    public string Auswaerts
    {
        get => _auswaerts;
        set { if (SetProperty(ref _auswaerts, value)) { OnPropertyChanged(nameof(AuswaertsBefuellt)); OnPropertyChanged(nameof(AuswaertsBefuelltOpacity)); } }
    }
    public bool AuswaertsBefuellt => _auswaerts != "…";
    public double AuswaertsBefuelltOpacity => AuswaertsBefuellt ? 1.0 : 0.4;
    private byte[]? _auswaertsWappen;
    public byte[]? AuswaertsWappen { get => _auswaertsWappen; set => SetProperty(ref _auswaertsWappen, value); }
}

/// <summary>Feste Anzahl Topf-Plätze für die TV-Modus-Anzeige: die Slots werden nie hinzugefügt oder
/// entfernt, damit sich das Layout nicht verschiebt, sobald ein Topf leerer wird — nur Gezogen/Anzeige
/// ändern sich pro Slot.</summary>
public class AnimationsTopfSlot : ObservableObject
{
    public int EintragId { get; init; }
    public string Name { get; init; } = "";
    public byte[]? Wappen { get; init; }
    private bool _gezogen;
    public bool Gezogen
    {
        get => _gezogen;
        set { if (SetProperty(ref _gezogen, value)) { OnPropertyChanged(nameof(Anzeige)); OnPropertyChanged(nameof(GezogenOpacity)); } }
    }
    public string Anzeige => Gezogen ? "…" : Name;
    public double GezogenOpacity => Gezogen ? 0.4 : 1.0;
}

public class AnimationsTopf
{
    public int TopfId { get; init; }
    public string Name { get; init; } = "";
    public ObservableCollection<AnimationsTopfSlot> Slots { get; init; } = [];
}

/// <summary>TV-Modus Ligaphase (#364): ein Gegner im Live-Banner — teilt die Matrix-Zelle, damit er genau dann
/// erscheint, wenn die Zelle aufgedeckt wird. Bereits vorher feststehende Gegner werden abgeblendet gezeigt.</summary>
public record AnimationsBannerGegner(string Kopf, GegnerMatrixZelle Zelle, bool IstNeu)
{
    public double Opacity => IstNeu ? 1.0 : 0.55;
}

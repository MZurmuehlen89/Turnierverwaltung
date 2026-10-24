using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Fussballturnierverwaltung.WinUi.Views;

/// <summary>Issue #428: anklickbare Fläche ohne die Vorlage eines <see cref="Button"/> — für Raster mit sehr
/// vielen Zellen (Gegner-Matrix der Ligaphase: 324 Stück), in denen je Button rund fünf zusätzliche Elemente
/// den Aufbau der Anzeige auf mehrere Sekunden trieben. Hinterlegt sich unter dem Mauszeiger und trägt die
/// Hervorhebung selbst als Hintergrund und Rahmen, statt dafür ein eigenes Element zu brauchen. Der Klick kommt
/// über <c>Tapped</c>; Tastaturfokus und Gedrückt-Zustand eines Buttons gibt es nicht.
/// <para>#522: Handler hängen an <see cref="Ausgeloest"/> statt an <c>Tapped</c> – das feuert beim Tippen und über
/// das Invoke-Muster von UI Automation (GUI-Tests, Screenreader). Der Automation-Peer entsteht erst, wenn ein
/// UIA-Client fragt, und kostet beim normalen Aufbau nichts.</para></summary>
public sealed class KlickFlaeche : Grid
{
    public static readonly DependencyProperty IstHervorgehobenProperty = DependencyProperty.Register(
        nameof(IstHervorgehoben), typeof(bool), typeof(KlickFlaeche), new PropertyMetadata(false, ZustandGeaendert));

    public static readonly DependencyProperty HervorhebungHintergrundProperty = DependencyProperty.Register(
        nameof(HervorhebungHintergrund), typeof(Brush), typeof(KlickFlaeche), new PropertyMetadata(null, ZustandGeaendert));

    public static readonly DependencyProperty HervorhebungRahmenProperty = DependencyProperty.Register(
        nameof(HervorhebungRahmen), typeof(Brush), typeof(KlickFlaeche), new PropertyMetadata(null, ZustandGeaendert));

    public static readonly DependencyProperty ZeigerHintergrundProperty = DependencyProperty.Register(
        nameof(ZeigerHintergrund), typeof(Brush), typeof(KlickFlaeche), new PropertyMetadata(null, ZustandGeaendert));

    // Ohne Hintergrund nähme die Fläche zwischen ihren Kindern keine Zeigerereignisse an.
    private static SolidColorBrush? _durchsichtig;
    private static SolidColorBrush Durchsichtig() => _durchsichtig ??= new SolidColorBrush(Colors.Transparent);

    private bool _zeigerDarueber;

    public KlickFlaeche()
    {
        PointerEntered += (_, _) => ZeigerSetzen(true);
        PointerExited += (_, _) => ZeigerSetzen(false);
        PointerCanceled += (_, _) => ZeigerSetzen(false);
        PointerCaptureLost += (_, _) => ZeigerSetzen(false);
        Tapped += (_, e) => e.Handled = Ausloesen();
        Anwenden();
    }

    /// <summary>Klick per Zeiger oder UI Automation. Setzt ein Handler <see cref="KlickFlaecheAusgeloestEventArgs.Handled"/>,
    /// endet ein Tippen hier und erreicht keine umgebende KlickFlaeche.</summary>
    public event EventHandler<KlickFlaecheAusgeloestEventArgs>? Ausgeloest;

    internal bool Ausloesen()
    {
        var args = new KlickFlaecheAusgeloestEventArgs();
        Ausgeloest?.Invoke(this, args);
        return args.Handled;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new KlickFlaecheAutomationPeer(this);

    public bool IstHervorgehoben
    {
        get => (bool)GetValue(IstHervorgehobenProperty);
        set => SetValue(IstHervorgehobenProperty, value);
    }

    public Brush? HervorhebungHintergrund
    {
        get => (Brush?)GetValue(HervorhebungHintergrundProperty);
        set => SetValue(HervorhebungHintergrundProperty, value);
    }

    public Brush? HervorhebungRahmen
    {
        get => (Brush?)GetValue(HervorhebungRahmenProperty);
        set => SetValue(HervorhebungRahmenProperty, value);
    }

    /// <summary>Hintergrund unter dem Mauszeiger; die Hervorhebung geht vor.</summary>
    public Brush? ZeigerHintergrund
    {
        get => (Brush?)GetValue(ZeigerHintergrundProperty);
        set => SetValue(ZeigerHintergrundProperty, value);
    }

    private static void ZustandGeaendert(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((KlickFlaeche)d).Anwenden();

    private void ZeigerSetzen(bool darueber)
    {
        if (_zeigerDarueber == darueber) return;
        _zeigerDarueber = darueber;
        Anwenden();
    }

    private void Anwenden()
    {
        var durchsichtig = Durchsichtig();
        Background = IstHervorgehoben ? HervorhebungHintergrund ?? durchsichtig
                   : _zeigerDarueber ? ZeigerHintergrund ?? durchsichtig
                   : durchsichtig;
        BorderBrush = IstHervorgehoben ? HervorhebungRahmen ?? durchsichtig : durchsichtig;
    }
}

public sealed class KlickFlaecheAusgeloestEventArgs : EventArgs
{
    public bool Handled { get; set; }
}

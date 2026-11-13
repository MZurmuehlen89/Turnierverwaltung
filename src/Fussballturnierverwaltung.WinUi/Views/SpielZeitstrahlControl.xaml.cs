using Fussballturnierverwaltung.WinUi.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Specialized;

namespace Fussballturnierverwaltung.WinUi.Views;

/// <summary>Ein Schuss im Elfmeterschießen für die Punktreihe des <see cref="SpielZeitstrahlControl"/>.</summary>
public sealed record ElfmeterPunkt(bool IstTreffer)
{
    public Visibility Treffer    => IstTreffer ? Visibility.Visible : Visibility.Collapsed;
    public Visibility Fehlschuss => IstTreffer ? Visibility.Collapsed : Visibility.Visible;
}

/// <summary>Issue #341 Teil B: kompakter horizontaler Zeitstrahl über die Spieldauer. Liest dieselben
/// <see cref="EinzelspielEreignisZeile"/>n wie die vertikale Zeitleiste (siehe
/// <see cref="SimulationsProtokollHelfer.ZeitleisteHinzufuegen"/>) und ergänzt sie als Überblick.</summary>
public sealed partial class SpielZeitstrahlControl : UserControl
{
    // ── Geometrie ────────────────────────────────────────────────────────────
    // Segmente je Halbzeit bzw. Verlängerungshälfte: (von, bis) in Spielminuten — Breite proportional zur
    // Dauer, die Verlängerungshälften sind also je ein Drittel einer Halbzeit breit.
    private static readonly (int Von, int Bis)[] _segmenteRegulaer      = [(0, 45), (45, 90)];
    private static readonly (int Von, int Bis)[] _segmenteVerlaengerung = [(0, 45), (45, 90), (90, 105), (105, 120)];

    private const double SEGMENT_LUECKE = 3;
    private const double MARKER_GROESSE = 16;

    public static readonly DependencyProperty EreignisseProperty = DependencyProperty.Register(
        nameof(Ereignisse), typeof(object), typeof(SpielZeitstrahlControl),
        new PropertyMetadata(null, (d, e) => ((SpielZeitstrahlControl)d).QuelleGewechselt(e.OldValue, e.NewValue)));

    public static readonly DependencyProperty IconVorlageProperty = DependencyProperty.Register(
        nameof(IconVorlage), typeof(DataTemplate), typeof(SpielZeitstrahlControl),
        new PropertyMetadata(null, (d, _) => ((SpielZeitstrahlControl)d).NeuzeichnenAnfordern()));

    public static readonly DependencyProperty HeimKuerzelProperty = DependencyProperty.Register(
        nameof(HeimKuerzel), typeof(string), typeof(SpielZeitstrahlControl),
        new PropertyMetadata(null, (d, _) => ((SpielZeitstrahlControl)d).NeuzeichnenAnfordern()));

    public static readonly DependencyProperty AuswaertsKuerzelProperty = DependencyProperty.Register(
        nameof(AuswaertsKuerzel), typeof(string), typeof(SpielZeitstrahlControl),
        new PropertyMetadata(null, (d, _) => ((SpielZeitstrahlControl)d).NeuzeichnenAnfordern()));

    /// <summary>Kürzel des Teams, dessen Ereignisse oberhalb des Balkens stehen.</summary>
    public string? HeimKuerzel
    {
        get => (string?)GetValue(HeimKuerzelProperty);
        set => SetValue(HeimKuerzelProperty, value);
    }

    /// <summary>Kürzel des Teams, dessen Ereignisse unterhalb des Balkens stehen.</summary>
    public string? AuswaertsKuerzel
    {
        get => (string?)GetValue(AuswaertsKuerzelProperty);
        set => SetValue(AuswaertsKuerzelProperty, value);
    }

    /// <summary>Collection von <see cref="EinzelspielEreignisZeile"/> (typisch eine ObservableCollection
    /// aus dem ViewModel) — als object deklariert, damit die WinRT-Bindung ohne generische Typprüfung greift.</summary>
    public object? Ereignisse
    {
        get => GetValue(EreignisseProperty);
        set => SetValue(EreignisseProperty, value);
    }

    /// <summary>Icon-Vorlage des Aufrufers (EreignisIconVorlage/MiniEreignisIconVorlage), damit die Marker
    /// exakt den Icons der vertikalen Zeitleiste daneben entsprechen.</summary>
    public DataTemplate? IconVorlage
    {
        get => (DataTemplate?)GetValue(IconVorlageProperty);
        set => SetValue(IconVorlageProperty, value);
    }

    private bool _neuzeichnenAngefordert;

    public SpielZeitstrahlControl()
    {
        InitializeComponent();
        Loaded   += (_, _) => { Abonnieren(Ereignisse); NeuzeichnenAnfordern(); };
        Unloaded += (_, _) => Abbestellen(Ereignisse);
    }

    private void QuelleGewechselt(object? alt, object? neu)
    {
        Abbestellen(alt);
        if (IsLoaded) Abonnieren(neu);
        NeuzeichnenAnfordern();
    }

    private void Abonnieren(object? quelle)
    {
        if (quelle is INotifyCollectionChanged ncc)
        {
            ncc.CollectionChanged -= Quelle_CollectionChanged;
            ncc.CollectionChanged += Quelle_CollectionChanged;
        }
    }

    private void Abbestellen(object? quelle)
    {
        if (quelle is INotifyCollectionChanged ncc)
            ncc.CollectionChanged -= Quelle_CollectionChanged;
    }

    private void Quelle_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => NeuzeichnenAnfordern();

    private void Zeichenflaeche_SizeChanged(object sender, SizeChangedEventArgs e) => Neuzeichnen();

    // Die ViewModels füllen die Collection zeilenweise (Clear + viele Add) — ein Neuaufbau pro Änderung
    // wäre Verschwendung, daher auf einen Dispatcher-Tick bündeln.
    private void NeuzeichnenAnfordern()
    {
        if (_neuzeichnenAngefordert) return;
        _neuzeichnenAngefordert = true;
        DispatcherQueue.TryEnqueue(() =>
        {
            _neuzeichnenAngefordert = false;
            Neuzeichnen();
        });
    }

    private void Neuzeichnen()
    {
        var zeilen = (Ereignisse as IEnumerable<EinzelspielEreignisZeile>)?.ToList() ?? [];

        HeimKuerzelText.Text      = HeimKuerzel ?? "";
        AuswaertsKuerzelText.Text = AuswaertsKuerzel ?? "";
        KuerzelSpalte.Visibility  = string.IsNullOrEmpty(HeimKuerzel) && string.IsNullOrEmpty(AuswaertsKuerzel)
            ? Visibility.Collapsed : Visibility.Visible;

        // Verlängerung fand statt, sobald ein Abschnittstrenner jenseits der 90. Minute existiert
        // (HALBZEIT VERLÄNGERUNG/ENDE VERLÄNGERUNG, siehe SimulationsProtokollHelfer.SchwellenPruefen) —
        // unabhängig davon, ob in der Verlängerung selbst ein Ereignis fiel.
        bool hatVerlaengerung = zeilen.Any(z => z.IstTrenner && z.MinuteWert > 90);
        var segmente = hatVerlaengerung ? _segmenteVerlaengerung : _segmenteRegulaer;

        double breite = Zeichenflaeche.ActualWidth;
        double nutzbar = Math.Max(0, breite - (segmente.Length - 1) * SEGMENT_LUECKE);
        int gesamtMinuten = segmente[^1].Bis;

        // Segment-Spalten: Segment, Lücke, Segment, … — nicht benötigte Verlängerungsspalten auf 0
        var segmentBreiten = segmente.Select(s => nutzbar * (s.Bis - s.Von) / gesamtMinuten).ToArray();
        var spalten = SegmentLeiste.ColumnDefinitions;
        for (int i = 0; i < spalten.Count; i++)
        {
            int segmentIndex = i / 2;
            double w = i % 2 == 0
                ? (segmentIndex < segmentBreiten.Length ? segmentBreiten[segmentIndex] : 0)
                : (segmentIndex + 1 < segmentBreiten.Length ? SEGMENT_LUECKE : 0);
            spalten[i].Width = new GridLength(w);
        }

        double XFuerMinute(int minute)
        {
            double start = 0;
            for (int i = 0; i < segmente.Length; i++)
            {
                var (von, bis) = segmente[i];
                if (minute <= bis || i == segmente.Length - 1)
                {
                    double anteil = Math.Clamp((double)(minute - von) / (bis - von), 0, 1);
                    return start + anteil * segmentBreiten[i];
                }
                start += segmentBreiten[i] + SEGMENT_LUECKE;
            }
            return start;
        }

        MarkerFlaeche.Children.Clear();
        var spielEreignisse = zeilen.Where(z => !z.IstTrenner && !z.IstElfmeterschuss).ToList();
        if (breite > 0)
        {
            double auswaertsTop = Zeichenflaeche.Height - MARKER_GROESSE;
            foreach (var zeile in spielEreignisse)
            {
                var marker = new ZeitstrahlMarker
                {
                    Content = zeile,
                    ContentTemplate = IconVorlage,
                    Width = MARKER_GROESSE,
                    Height = MARKER_GROESSE,
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    VerticalContentAlignment = VerticalAlignment.Center
                };
                string detail = zeile.Detail is null ? "" : $" {zeile.Detail}";
                string beschreibung = $"{zeile.Minute} {zeile.Art}{detail} – {zeile.Seite}";
                ToolTipService.SetToolTip(marker, beschreibung);
                AutomationProperties.SetAutomationId(marker, zeile.IstHeimSeite ? "Zeitstrahl.HeimMarker" : "Zeitstrahl.AuswaertsMarker");
                AutomationProperties.SetName(marker, zeile.Art);
                AutomationProperties.SetHelpText(marker, beschreibung);
                Canvas.SetLeft(marker, XFuerMinute(zeile.MinuteWert) - MARKER_GROESSE / 2);
                Canvas.SetTop(marker, zeile.IstHeimSeite ? 0 : auswaertsTop);
                MarkerFlaeche.Children.Add(marker);
            }
        }

        var schuesse = zeilen.Where(z => z.IstElfmeterschuss).OrderBy(z => z.MinuteWert).ToList();
        HeimElfmeter.ItemsSource      = schuesse.Where(z => z.IstHeimSeite).Select(z => new ElfmeterPunkt(z.IstTor)).ToList();
        AuswaertsElfmeter.ItemsSource = schuesse.Where(z => !z.IstHeimSeite).Select(z => new ElfmeterPunkt(z.IstTor)).ToList();
        ElfmeterBereich.Visibility    = schuesse.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }
}

/// <summary>Marker eines Ereignisses auf dem Zeitstrahl. Ein <see cref="ContentPresenter"/> hat keinen eigenen
/// Automation-Peer und fehlt damit im UIA-Baum — der GUI-Test zählt die Marker je Seite (#534).</summary>
public sealed partial class ZeitstrahlMarker : ContentPresenter
{
    protected override AutomationPeer OnCreateAutomationPeer() => new FrameworkElementAutomationPeer(this);
}

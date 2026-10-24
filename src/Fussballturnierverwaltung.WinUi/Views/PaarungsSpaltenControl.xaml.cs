using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections;
using System.Collections.Specialized;

namespace Fussballturnierverwaltung.WinUi.Views;

/// <summary>Issue #407: Paarungen spaltenweise wie in der Fernsehauslosung (1–8 links, 9–16 daneben …),
/// höchstens <see cref="ZeilenJeSpalte"/> je Spalte. Zwei Spalten stehen immer nebeneinander, weitere nur
/// bei genügend Breite — sonst wird geblättert. Die Darstellung der einzelnen Paarung kommt per
/// <see cref="PaarungTemplate"/> vom Aufrufer, damit Ergebnisanzeige und Live-Ziehung (#408) dieselbe
/// Anordnung nutzen können.</summary>
public sealed partial class PaarungsSpaltenControl : UserControl
{
    public static readonly DependencyProperty PaarungenProperty = DependencyProperty.Register(
        nameof(Paarungen), typeof(IEnumerable), typeof(PaarungsSpaltenControl),
        new PropertyMetadata(null, (d, e) => ((PaarungsSpaltenControl)d).PaarungenGewechselt(e.OldValue, e.NewValue)));

    public static readonly DependencyProperty PaarungTemplateProperty = DependencyProperty.Register(
        nameof(PaarungTemplate), typeof(DataTemplate), typeof(PaarungsSpaltenControl),
        new PropertyMetadata(null, (d, _) => ((PaarungsSpaltenControl)d).Aufbauen(erzwingen: true)));

    public static readonly DependencyProperty ZeilenJeSpalteProperty = DependencyProperty.Register(
        nameof(ZeilenJeSpalte), typeof(int), typeof(PaarungsSpaltenControl),
        new PropertyMetadata(8, (d, _) => ((PaarungsSpaltenControl)d).Aufbauen(erzwingen: true)));

    public static readonly DependencyProperty MinSpaltenBreiteProperty = DependencyProperty.Register(
        nameof(MinSpaltenBreite), typeof(double), typeof(PaarungsSpaltenControl),
        new PropertyMetadata(380d, (d, _) => ((PaarungsSpaltenControl)d).Aufbauen(erzwingen: true)));

    public static readonly DependencyProperty MaxSpaltenBreiteProperty = DependencyProperty.Register(
        nameof(MaxSpaltenBreite), typeof(double), typeof(PaarungsSpaltenControl),
        new PropertyMetadata(480d, (d, _) => ((PaarungsSpaltenControl)d).Aufbauen(erzwingen: true)));

    private const double SPALTEN_ABSTAND = 32;

    private List<object> _eintraege = [];
    private int _seite;
    // Zuletzt aufgebaute Aufteilung — bei reiner Breitenänderung ohne neue Aufteilung nur Spaltenbreiten anpassen.
    private (int Spalten, int Zeilen, int Seite) _aufgebaut = (0, 0, -1);

    public PaarungsSpaltenControl()
    {
        InitializeComponent();
        SizeChanged += (_, e) =>
        {
#pragma warning disable S1244 // jede Breitenänderung zählt, auch eine sehr kleine
            if (e.PreviousSize.Width != e.NewSize.Width) Aufbauen(erzwingen: false);
#pragma warning restore S1244
        };
    }

    public IEnumerable? Paarungen
    {
        get => (IEnumerable?)GetValue(PaarungenProperty);
        set => SetValue(PaarungenProperty, value);
    }

    public DataTemplate? PaarungTemplate
    {
        get => (DataTemplate?)GetValue(PaarungTemplateProperty);
        set => SetValue(PaarungTemplateProperty, value);
    }

    public int ZeilenJeSpalte
    {
        get => (int)GetValue(ZeilenJeSpalteProperty);
        set => SetValue(ZeilenJeSpalteProperty, value);
    }

    public double MinSpaltenBreite
    {
        get => (double)GetValue(MinSpaltenBreiteProperty);
        set => SetValue(MinSpaltenBreiteProperty, value);
    }

    public double MaxSpaltenBreite
    {
        get => (double)GetValue(MaxSpaltenBreiteProperty);
        set => SetValue(MaxSpaltenBreiteProperty, value);
    }

    private void PaarungenGewechselt(object? alt, object? neu)
    {
        if (alt is INotifyCollectionChanged altIncc) altIncc.CollectionChanged -= Paarungen_CollectionChanged;
        if (neu is INotifyCollectionChanged neuIncc) neuIncc.CollectionChanged += Paarungen_CollectionChanged;
        _seite = 0;
        EintraegeUebernehmen();
    }

    private void Paarungen_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => EintraegeUebernehmen();

    private void EintraegeUebernehmen()
    {
        _eintraege = Paarungen?.Cast<object>().ToList() ?? [];
        Aufbauen(erzwingen: true);
    }

    private void ZurueckButton_Click(object sender, RoutedEventArgs e)
    {
        _seite--;
        Aufbauen(erzwingen: false);
    }

    private void WeiterButton_Click(object sender, RoutedEventArgs e)
    {
        _seite++;
        Aufbauen(erzwingen: false);
    }

    private void Aufbauen(bool erzwingen)
    {
        int anzahl = _eintraege.Count;
        if (anzahl == 0)
        {
            SpaltenGrid.Children.Clear();
            SpaltenGrid.ColumnDefinitions.Clear();
            BlaetterLeiste.Visibility = Visibility.Collapsed;
            _aufgebaut = (0, 0, -1);
            return;
        }

        // Vor dem ersten Layout ist die Breite 0 — dann mit zwei Spalten beginnen, SizeChanged korrigiert.
        double breite = ActualWidth;
        int zeilenMax = Math.Max(1, ZeilenJeSpalte);
        int spaltenGesamt = (int)Math.Ceiling(anzahl / (double)zeilenMax);
        int passend = breite <= 0
            ? 2
            : Math.Max(2, (int)Math.Floor((breite + SPALTEN_ABSTAND) / (MinSpaltenBreite + SPALTEN_ABSTAND)));

        // Seiten und Spalten ausgleichen: 32 Paarungen bei Platz für drei Spalten → 2 Seiten mit je zwei
        // Spalten (16 + 16) statt 24 + 8; 12 Paarungen → 6 + 6 statt 8 + 4.
        int seiten = (int)Math.Ceiling(spaltenGesamt / (double)passend);
        int spalten = (int)Math.Ceiling(spaltenGesamt / (double)seiten);
        int zeilen = (int)Math.Ceiling(anzahl / (double)(seiten * spalten));

        _seite = Math.Clamp(_seite, 0, seiten - 1);

        double spaltenBreite = breite <= 0
            ? MinSpaltenBreite
            : Math.Clamp((breite - SPALTEN_ABSTAND * (spalten - 1)) / spalten, 0, MaxSpaltenBreite);

        if (erzwingen || _aufgebaut != (spalten, zeilen, _seite))
        {
            SpaltenGrid.Children.Clear();
            SpaltenGrid.ColumnDefinitions.Clear();
            int erster = _seite * spalten * zeilen;
            for (int s = 0; s < spalten; s++)
            {
                var teil = _eintraege.Skip(erster + s * zeilen).Take(zeilen).ToList();
                if (teil.Count == 0) break;

                SpaltenGrid.ColumnDefinitions.Add(new ColumnDefinition());
                var liste = new ItemsControl
                {
                    ItemsSource = teil,
                    ItemTemplate = PaarungTemplate,
                    Margin = new Thickness(s == 0 ? 0 : SPALTEN_ABSTAND, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Top
                };
                Grid.SetColumn(liste, s);
                SpaltenGrid.Children.Add(liste);
            }
            _aufgebaut = (spalten, zeilen, _seite);

            int letzter = Math.Min(anzahl, erster + spalten * zeilen);
            BlaetterLeiste.Visibility = seiten > 1 ? Visibility.Visible : Visibility.Collapsed;
            SeitenText.Text = $"Paarungen {erster + 1}–{letzter} von {anzahl}";
            ZurueckButton.IsEnabled = _seite > 0;
            WeiterButton.IsEnabled = _seite < seiten - 1;
        }

        for (int s = 0; s < SpaltenGrid.ColumnDefinitions.Count; s++)
            SpaltenGrid.ColumnDefinitions[s].Width = new GridLength(spaltenBreite + (s == 0 ? 0 : SPALTEN_ABSTAND));
    }
}

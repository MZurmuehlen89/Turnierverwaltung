using Fussballturnierverwaltung.Application.Abstraktionen;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace Fussballturnierverwaltung.WinUi.Views;

/// <summary>Ein Fehler in der aufgeklappten <see cref="MeldungsLeiste"/>, mit der Kennung für GUI-Tests. Mit Feldbezug
/// (<see cref="Ziel"/>) ist er ein Knopf, der ins Feld springt (#595); Kreuzprüfungen bleiben reiner Text.</summary>
public sealed record MeldungsEintrag(string Text, string AutomationId, Feldfehler? Ziel = null)
{
    public Visibility AlsKnopf => Ziel?.Feld is null ? Visibility.Collapsed : Visibility.Visible;
    public Visibility AlsText => Ziel?.Feld is null ? Visibility.Visible : Visibility.Collapsed;
}

/// <summary>Meldungen eines Formulars als Andock-Leiste am rechten Rand (#594): Speicher-/Statusfehler
/// (<see cref="Fehlermeldung"/>), Fehler der Live-Prüfung (<see cref="Fehler"/>) und ein Hinweis, der das Speichern
/// nicht verhindert (<see cref="Hinweis"/>). Ohne Meldung ist die Leiste unsichtbar, sonst zeigt sie eine Kante mit
/// Zähler – rot, sobald ein Fehler dabei ist, sonst gelb. Ein Speicherfehler klappt die Liste selbst auf, Fehler der
/// Live-Prüfung nicht (sie entstehen beim Tippen). Ein Klick außerhalb der Leiste klappt sie wieder zu.
/// Liefert <see cref="Fehler"/> <see cref="Feldfehler"/>, springt ein Klick auf einen Fehler mit Feldbezug ins Feld
/// (<see cref="Feldmarkierung.Anspringen"/>, gesucht im Elternelement der Leiste – dem Formular).
/// Die Kennungen für GUI-Tests folgen <see cref="AutomationPraefix"/>: <c>.Meldungen</c> (Kante, Name = Zähler),
/// <c>.Fehlermeldung</c>, <c>.Fehler</c>, <c>.Hinweis</c>. Eingeklappt sind die Meldungen nicht im UIA-Baum.</summary>
public sealed partial class MeldungsLeiste : UserControl
{
    public static readonly DependencyProperty FehlermeldungProperty = DependencyProperty.Register(
        nameof(Fehlermeldung), typeof(string), typeof(MeldungsLeiste),
        new PropertyMetadata(null, (d, e) => ((MeldungsLeiste)d).FehlermeldungGeaendert(e.NewValue as string)));

    public string? Fehlermeldung
    {
        get => (string?)GetValue(FehlermeldungProperty);
        set => SetValue(FehlermeldungProperty, value);
    }

    // object statt einer Liste: die ViewModels liefern Texte (IEnumerable<string>) oder Feldfehler.
    public static readonly DependencyProperty FehlerProperty = DependencyProperty.Register(
        nameof(Fehler), typeof(object), typeof(MeldungsLeiste),
        new PropertyMetadata(null, (d, _) => ((MeldungsLeiste)d).Aktualisieren()));

    public object? Fehler
    {
        get => GetValue(FehlerProperty);
        set => SetValue(FehlerProperty, value);
    }

    public static readonly DependencyProperty HinweisProperty = DependencyProperty.Register(
        nameof(Hinweis), typeof(string), typeof(MeldungsLeiste),
        new PropertyMetadata(null, (d, _) => ((MeldungsLeiste)d).Aktualisieren()));

    public string? Hinweis
    {
        get => (string?)GetValue(HinweisProperty);
        set => SetValue(HinweisProperty, value);
    }

    public static readonly DependencyProperty AutomationPraefixProperty = DependencyProperty.Register(
        nameof(AutomationPraefix), typeof(string), typeof(MeldungsLeiste),
        new PropertyMetadata("", (d, _) => ((MeldungsLeiste)d).KennungenSetzen()));

    public string AutomationPraefix
    {
        get => (string)GetValue(AutomationPraefixProperty);
        set => SetValue(AutomationPraefixProperty, value);
    }

    private readonly PointerEventHandler _klickAusserhalb;
    private UIElement? _klickQuelle;

    public MeldungsLeiste()
    {
        InitializeComponent();
        _klickAusserhalb = KlickAusserhalb;
        // Vor dem Laden greifen weder der Farbzustand noch die Klickbeobachtung (kein XamlRoot) – darum hier nachholen.
        Loaded += (_, _) =>
        {
            Aktualisieren();
            if (Umschalter.IsChecked == true) KlickAusserhalbBeobachten();
        };
        Unloaded += (_, _) => Umschalter.IsChecked = false;
        Aktualisieren();
    }

    // ── Inhalt ────────────────────────────────────────────────────────────────

    private void FehlermeldungGeaendert(string? neu)
    {
        Aktualisieren();
        if (neu is not null) Umschalter.IsChecked = true;
    }

    private void Aktualisieren()
    {
        var fehler = Eintraege();
        TextZeigen(FehlermeldungRahmen, FehlermeldungText, Fehlermeldung);
        TextZeigen(HinweisRahmen, HinweisText, Hinweis);
        FehlerListe.ItemsSource = fehler;

        var anzahlFehler = fehler.Count + (Fehlermeldung is null ? 0 : 1);
        KanteSetzen(anzahlFehler, Hinweis is not null);
    }

    private List<MeldungsEintrag> Eintraege()
    {
        var kennung = $"{AutomationPraefix}.Fehler";
        return Fehler switch
        {
            IEnumerable<Feldfehler> meldungen => [.. meldungen.Select(m => new MeldungsEintrag(m.Text, kennung, m))],
            IEnumerable<string> texte => [.. texte.Select(t => new MeldungsEintrag(t, kennung))],
            _ => [],
        };
    }

    private void Eintrag_Klick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not MeldungsEintrag { Ziel: { } ziel }
            || VisualTreeHelper.GetParent(this) is not { } formular) return;
        Umschalter.IsChecked = false;
        Feldmarkierung.Anspringen(formular, ziel);
    }

    private static void TextZeigen(Border rahmen, TextBlock text, string? inhalt)
    {
        text.Text = inhalt ?? "";
        rahmen.Visibility = inhalt is null ? Visibility.Collapsed : Visibility.Visible;
    }

    private void KanteSetzen(int anzahlFehler, bool hatHinweis)
    {
        var hatMeldungen = anzahlFehler > 0 || hatHinweis;
        Visibility = hatMeldungen ? Visibility.Visible : Visibility.Collapsed;
        if (!hatMeldungen)
        {
            Umschalter.IsChecked = false;
            return;
        }

        var zaehler = ZaehlerText(anzahlFehler, hatHinweis);
        AutomationProperties.SetName(Umschalter, zaehler);
        KantenTextSetzen(zaehler);
        VisualStateManager.GoToState(this, anzahlFehler > 0 ? "MitFehlern" : "NurHinweise", false);
    }

    private static string ZaehlerText(int anzahlFehler, bool hatHinweis) => (anzahlFehler > 0, hatHinweis) switch
    {
        (true, true) => $"{anzahlFehler} Fehler · 1 Hinweis",
        (true, false) => $"{anzahlFehler} Fehler",
        _ => "1 Hinweis",
    };

    // Der Text steht um 90° gedreht im Canvas: Die Drehung um die linke obere Ecke legt ihn links neben die Fläche,
    // die Verschiebung um seine Höhe holt ihn zurück; die Fläche bekommt die vertauschten Maße.
    private void KantenTextSetzen(string text)
    {
        KantenText.Text = text;
        KantenText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var groesse = KantenText.DesiredSize;
        KantenDrehung.TranslateX = groesse.Height;
        KantenFlaeche.Width = groesse.Height;
        KantenFlaeche.Height = groesse.Width;
    }

    private void KennungenSetzen()
    {
        AutomationProperties.SetAutomationId(Umschalter, $"{AutomationPraefix}.Meldungen");
        AutomationProperties.SetAutomationId(FehlermeldungText, $"{AutomationPraefix}.Fehlermeldung");
        AutomationProperties.SetAutomationId(HinweisText, $"{AutomationPraefix}.Hinweis");
        Aktualisieren();
    }

    // ── Auf- und Zuklappen ────────────────────────────────────────────────────

    private void Umschalter_Geaendert(object sender, RoutedEventArgs e)
    {
        var offen = Umschalter.IsChecked == true;
        Liste.Visibility = offen ? Visibility.Visible : Visibility.Collapsed;
        if (offen) KlickAusserhalbBeobachten();
        else KlickAusserhalbVergessen();
    }

    // handledEventsToo: Textfelder und Knöpfe im Formular behandeln den Klick selbst; er soll sie trotzdem erreichen
    // und die Leiste nur nebenbei zuklappen.
    private void KlickAusserhalbBeobachten()
    {
        if (_klickQuelle is not null || XamlRoot?.Content is not UIElement wurzel) return;
        _klickQuelle = wurzel;
        _klickQuelle.AddHandler(PointerPressedEvent, _klickAusserhalb, true);
    }

    private void KlickAusserhalbVergessen()
    {
        _klickQuelle?.RemoveHandler(PointerPressedEvent, _klickAusserhalb);
        _klickQuelle = null;
    }

    private void KlickAusserhalb(object sender, PointerRoutedEventArgs e)
    {
        if (!IstInDerLeiste(e.OriginalSource as DependencyObject)) Umschalter.IsChecked = false;
    }

    private bool IstInDerLeiste(DependencyObject? element)
    {
        for (var aktuell = element; aktuell is not null; aktuell = VisualTreeHelper.GetParent(aktuell))
            if (ReferenceEquals(aktuell, this)) return true;
        return false;
    }
}

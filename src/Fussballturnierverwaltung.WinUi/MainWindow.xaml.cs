using Fussballturnierverwaltung.WinUi.Dialoge;
using Fussballturnierverwaltung.WinUi.Helpers;
using Fussballturnierverwaltung.WinUi.Ribbon;
using Fussballturnierverwaltung.WinUi.ViewModels;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using System.ComponentModel;

namespace Fussballturnierverwaltung.WinUi;

public sealed partial class MainWindow : Window
{
    // Titel/Neu/Speichern/Abbrechen/Separator/Löschen aus dem XAML in AktionenPanel (CommandBar.Content,
    // fensterseitig links verankert) — direkt danach werden die dynamischen Neu-Unterpunkte (Issue
    // #297-Nachzug, z.B. "Aus Vorlage erstellen") als eigene Buttons eingefügt, danach die
    // RibbonZusatzGruppen angehängt (CommandBar unterstützt kein ItemsSource-Binding für PrimaryCommands).
    private const int AnzahlFesterAktionenBasis = 6;

    // Wie viele Buttons NeuUnterpunkteAktualisieren aktuell direkt nach "Neu" eingefügt hat — bestimmt den
    // tatsächlichen Startindex der RibbonZusatzGruppen (Basis + diese Anzahl), da beide Bereiche sich
    // denselben AktionenPanel teilen und die Neu-Unterpunkte VOR den RibbonZusatzGruppen sitzen.
    private int _neuUnterpunkteAnzahl;

    private readonly ThemeService _themeService;
    private readonly IDialogService _dialogService;
    private readonly DispatcherQueueTimer _ribbonAktualisierenTimer;

    public MainViewModel ViewModel { get; }

    public LadeanzeigeViewModel Ladeanzeige { get; }

    public MainWindow(MainViewModel viewModel, ThemeService themeService, IDialogService dialogService)
    {
        ViewModel = viewModel;
        _themeService = themeService;
        _dialogService = dialogService;
        Ladeanzeige = new LadeanzeigeViewModel(DispatcherQueue);

        InitializeComponent();

        // Titelleiste (#398). Taskleiste, Startmenü und Alt+Tab nehmen bei der paketierten App die
        // Square44x44Logo-Dateien aus dem Manifest. Absoluter Pfad, weil das Arbeitsverzeichnis einer
        // paketierten App nicht der Programmordner ist.
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"));

        RootGrid.DataContext = ViewModel;
        RootGrid.Loaded += (_, _) => _dialogService.XamlRootSetzen(RootGrid.XamlRoot);
        _dialogService.FensterSetzen(this);

        _themeService.RootSetzen(RootGrid);
        _themeService.AnwendenAusEinstellungen();

        // Debounce für den CommandBar-Rebuild (Issue #338-Folgefund): RibbonZusatzGruppen hängt an
        // AusgewaehlteSpiel/AusgewaehlteRunde einiger Fach-ViewModels (z.B. SpielplanViewModel) — bei
        // schnellem Durchklicken per Pfeiltasten feuerte das PropertyChanged bei JEDER Zwischenauswahl
        // synchron auf dem UI-Thread, und RibbonZusatzGruppenAktualisieren wirft dabei jedes Mal alle
        // CommandBar-Buttons weg und erzeugt sie neu — spürbar ruckelnd. Ein kurzer Timer sammelt mehrere
        // schnell aufeinanderfolgende Änderungen und baut nur einmal für den jeweils letzten Zustand neu auf.
        _ribbonAktualisierenTimer = DispatcherQueue.CreateTimer();
        _ribbonAktualisierenTimer.Interval = TimeSpan.FromMilliseconds(120);
        _ribbonAktualisierenTimer.IsRepeating = false;
        _ribbonAktualisierenTimer.Tick += (_, _) =>
        {
            NeuUnterpunkteAktualisieren();
            RibbonZusatzGruppenAktualisieren();
        };

        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        NeuUnterpunkteAktualisieren();
        RibbonZusatzGruppenAktualisieren();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.RibbonZusatzGruppen) or nameof(MainViewModel.RibbonNeuUnterpunkte))
        {
            _ribbonAktualisierenTimer.Stop();
            _ribbonAktualisierenTimer.Start();
        }
    }

    /// <summary>Fügt <see cref="MainViewModel.RibbonNeuUnterpunkte"/> (Issue #297-Nachzug, z.B. "Aus Vorlage
    /// erstellen") als eigenständige Buttons direkt nach "Neu" ein (Index 2, nach Titel-TextBlock und
    /// NeuButton) — bewusst kein Flyout/Split-Button mehr (Nutzerwunsch aus #297-Test: "sieht nicht schön
    /// aus"), einfach ein weiterer sichtbarer Button. Entfernt zuerst die zuvor eingefügten Buttons, damit
    /// ein Tabwechsel nicht immer weiter anhäuft.</summary>
    private void NeuUnterpunkteAktualisieren()
    {
        for (var i = 0; i < _neuUnterpunkteAnzahl; i++)
            AktionenPanel.PrimaryCommands.RemoveAt(2);
        _neuUnterpunkteAnzahl = 0;

        var unterpunkte = ViewModel.RibbonNeuUnterpunkte;
        for (var i = 0; i < unterpunkte.Count; i++)
            AktionenPanel.PrimaryCommands.Insert(2 + i, RibbonButtonErstellen(unterpunkte[i]));
        _neuUnterpunkteAnzahl = unterpunkte.Count;
    }

    private void RibbonZusatzGruppenAktualisieren()
    {
        var start = AnzahlFesterAktionenBasis + _neuUnterpunkteAnzahl;
        while (AktionenPanel.PrimaryCommands.Count > start)
            AktionenPanel.PrimaryCommands.RemoveAt(start);

        var gruppen = ViewModel.RibbonZusatzGruppen;
        for (var g = 0; g < gruppen.Count; g++)
        {
            AktionenPanel.PrimaryCommands.Add(new AppBarSeparator());
            foreach (var aktion in gruppen[g].Aktionen)
                AktionenPanel.PrimaryCommands.Add(RibbonButtonErstellen(aktion));
        }
    }

    /// <summary>Erzeugt einen <see cref="AppBarButton"/> (oder, falls <see cref="RibbonAktion.IstAktiv"/>
    /// gesetzt ist, einen <see cref="AppBarToggleButton"/> mit sichtbar gedrücktem Zustand — Issue #325) aus
    /// einer <see cref="RibbonAktion"/> — entweder mit direktem <see cref="RibbonAktion.Befehl"/> oder, falls
    /// gesetzt, mit einem Flyout aus <see cref="RibbonAktion.ToggleUnterpunkte"/> (an-/abschaltbare Optionen
    /// mit Infotext, z.B. Auslosungs-Regeln). Explizite <see cref="AppBarButton.Width"/> statt der
    /// AppBarButton-Standardbreite: das Standard-Layout bricht/bschneidet bei mehrzeiligem Label nicht sauber
    /// um, längere deutsche Beschriftungen ("Lostöpfe automatisch befüllen") passten sonst nicht einzeilig in
    /// den Button (Nutzerfund nach #300-Test).</summary>
    private static ICommandBarElement RibbonButtonErstellen(RibbonAktion aktion)
    {
        if (aktion.IstAktiv is { } istAktiv)
        {
            var toggleButton = new AppBarToggleButton
            {
                Label = aktion.Header, Width = BreiteFuerLabel(aktion.Header),
                IsChecked = istAktiv, Command = aktion.Befehl
            };
            toggleButton.Click += (_, _) => Ablaufprotokoll.Bedienung(aktion.Header);
            KennungSetzen(toggleButton, aktion.AutomationId);
            if (aktion.IconGlyph is not null)
                toggleButton.Icon = new FontIcon { Glyph = aktion.IconGlyph };
            return toggleButton;
        }

        var button = new AppBarButton { Label = aktion.Header, Width = BreiteFuerLabel(aktion.Header) };
        KennungSetzen(button, aktion.AutomationId);
        if (aktion.IconGlyph is not null)
            button.Icon = new FontIcon { Glyph = aktion.IconGlyph };

        if (aktion.ToggleUnterpunkte is { Count: > 0 } toggles)
            button.Flyout = ToggleFlyoutErstellen(toggles);
        else if (aktion.Unteraktionen is { Count: > 0 } unteraktionen)
            button.Flyout = AktionsFlyoutErstellen(unteraktionen);
        else
        {
            button.Command = aktion.Befehl;
            // Ablaufprotokoll (#413): jede Ribbon-Aktion mit Zeitpunkt, damit ein späterer Fehler seinem Auslöser
            // zugeordnet werden kann.
            button.Click += (_, _) => Ablaufprotokoll.Bedienung(aktion.Header);
        }

        return button;
    }

    /// <summary>Flyout für <see cref="RibbonAktion.Unteraktionen"/> — jeder Menüpunkt führt sofort seinen
    /// Befehl aus und schließt das Flyout (Standardverhalten von <see cref="MenuFlyoutItem"/>), anders als
    /// <see cref="ToggleFlyoutErstellen"/>, das offen bleibt, solange mehrere Umschalter gesetzt werden.</summary>
    private static void KennungSetzen(DependencyObject element, string? automationId)
    {
        if (automationId is not null)
            AutomationProperties.SetAutomationId(element, automationId);
    }

    private static MenuFlyout AktionsFlyoutErstellen(IReadOnlyList<RibbonUnteraktion> unteraktionen)
    {
        var flyout = new MenuFlyout();
        foreach (var unteraktion in unteraktionen)
        {
            var item = unteraktion.SperrHinweis is { Length: > 0 } sperrHinweis
                ? new MenuFlyoutItem { Text = $"{unteraktion.Header} ({sperrHinweis})", IsEnabled = false }
                : new MenuFlyoutItem { Text = unteraktion.Header, Command = unteraktion.Befehl };
            item.Click += (_, _) => Ablaufprotokoll.Bedienung(unteraktion.Header);
            KennungSetzen(item, unteraktion.AutomationId);
            if (unteraktion.IconGlyph is not null)
                item.Icon = new FontIcon { Glyph = unteraktion.IconGlyph };
            flyout.Items.Add(item);
        }
        return flyout;
    }

    /// <summary>Baut den Flyout-Inhalt für <see cref="RibbonAktion.ToggleUnterpunkte"/> imperativ statt über
    /// <c>ToggleMenuFlyoutItem</c>/<c>MenuFlyout</c>: Letzteres kann nur eine einzeilige Beschriftung
    /// anzeigen, der Infotext (<see cref="RibbonToggleAktion.Beschreibung"/>) landete nur als Tooltip —
    /// unentdeckt, da ein Flyout typischerweise nicht lange genug gehovert wird (Nutzerfund nach #300-Test:
    /// "Auslosungsregeln haben keine Infotexte mehr"). <see cref="CheckBox"/> statt <see cref="ToggleSwitch"/>
    /// für den Umschalter — Letzterer reserviert intern immer Platz für seinen An/Aus-Beschriftungsbereich,
    /// selbst bei leerem <c>OnContent</c>/<c>OffContent</c>, was eine auffällige Lücke zwischen Schalter und
    /// Text erzeugte (Nutzerfund). Feste Breite + <c>HorizontalScrollBarVisibility="Disabled"</c>: bei vielen
    /// Regeln soll nur vertikal, nie horizontal gescrollt werden müssen — Text bricht stattdessen um.
    /// <c>MaxHeight</c> großzügig bemessen, damit die vertikale Scrollbar im Normalfall (die 7 festen
    /// <c>AuslosungsRegelArt</c>-Einträge) gar nicht erst zum Tragen kommt und nur als Fallback bei
    /// sehr kleinen Fenstern/vielen zukünftigen Einträgen greift (#310).
    /// Header+Beschreibung werden als <see cref="CheckBox.Content"/> gesetzt statt in einer separaten Zelle
    /// daneben: Ein eigenes Grid mit fester oder aus der Checkbox-Breite abgeleiteter Spaltenaufteilung
    /// scheiterte an WinUI3s <c>ButtonBase</c>-Erbe, das auch bei leerem Content ~120px Platz reserviert und so
    /// eine auffällige Lücke erzeugte; als Content sitzt der Text im Standard-Template direkt neben dem Glyph
    /// mit sinnvollem Default-Abstand, und der gesamte Text wird nebenbei mitklickbar (#310). Explizite
    /// <c>FlyoutPresenterStyle</c> mit explizit angehobener <c>MaxWidth</c>: WinUI3s Standard-<c>FlyoutPresenter</c>
    /// (der System-Container, den jeder <c>Flyout</c> automatisch um seinen <c>Content</c> legt) hat selbst eine
    /// kleinere Default-<c>MaxWidth</c> und bringt dafür einen eigenen, unsichtbaren ScrollViewer mit — der
    /// horizontale Scrollbalken kam von dort, nicht von unserem eigenen inneren <c>ScrollViewer</c>
    /// (<c>HorizontalScrollBarVisibility="Disabled"</c> darauf allein reichte deshalb nicht, Text-<c>MaxWidth</c>
    /// auf der Inhalts-Ebene blieb wirkungslos, siehe #310).</summary>
    private static Flyout ToggleFlyoutErstellen(IReadOnlyList<RibbonToggleAktion> toggles)
    {
        const double breite = 560;
        var liste = new StackPanel { Padding = new Thickness(4), Spacing = 14, Width = breite };
        var umschalterJeGruppe = new Dictionary<string, List<CheckBox>>();
        foreach (var toggle in toggles)
        {
            var umschalter = UmschalterErstellen(toggle);

            List<CheckBox>? gruppe = null;
            if (toggle.ExklusivGruppe is { } gruppenName)
            {
                if (!umschalterJeGruppe.TryGetValue(gruppenName, out gruppe))
                    umschalterJeGruppe[gruppenName] = gruppe = [];
                gruppe.Add(umschalter);
            }

            umschalter.Click += (_, _) =>
            {
                bool aktiv = umschalter.IsChecked == true;
                if (aktiv && gruppe is not null)
                    foreach (var andere in gruppe.Where(c => c != umschalter))
                        andere.IsChecked = false;
                toggle.Setzen(aktiv);
            };

            liste.Children.Add(umschalter);
        }

        var flyoutPresenterStyle = new Style(typeof(FlyoutPresenter));
        flyoutPresenterStyle.Setters.Add(new Setter(FrameworkElement.MaxWidthProperty, breite + 40));

        return new Flyout
        {
            FlyoutPresenterStyle = flyoutPresenterStyle,
            Content = new ScrollViewer
            {
                Content = liste, MaxHeight = 720, Width = breite,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            }
        };
    }

    /// <summary>Ein Umschalter des Toggle-Flyouts: Checkbox mit Überschrift, Infotext und ggf. Sperrhinweis als
    /// Content.</summary>
    private static CheckBox UmschalterErstellen(RibbonToggleAktion toggle)
    {
        var text = new StackPanel();
        text.Children.Add(new TextBlock { Text = toggle.Header, FontSize = 13, TextWrapping = TextWrapping.Wrap });
        if (toggle.Beschreibung is { Length: > 0 } beschreibung)
            text.Children.Add(new TextBlock
            {
                Text = beschreibung, FontSize = 11, Opacity = 0.6, TextWrapping = TextWrapping.Wrap
            });
        // Gesperrt: ausgegraut (IsEnabled = false dimmt den ganzen Content), der Hinweis erklärt warum (#331).
        if (toggle.SperrHinweis is { Length: > 0 } sperrHinweis)
            text.Children.Add(new TextBlock
            {
                Text = sperrHinweis, FontSize = 11, FontStyle = Windows.UI.Text.FontStyle.Italic,
                TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0)
            });

        var umschalter = new CheckBox
        {
            IsChecked = toggle.IstAktiv, Content = text,
            IsEnabled = toggle.SperrHinweis is null,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Top
        };
        KennungSetzen(umschalter, toggle.AutomationId);
        return umschalter;
    }

    // ── Button-Breite ────────────────────────
    private const double MINDESTBREITE = 90;
    private const double ICON_BREITE = 16;
    private const double RAND_UND_ABSTAENDE = 34;
    private const double LABEL_SCHRIFTGROESSE = 12;

    /// <summary>Misst die tatsächliche Label-Breite statt sie über die Zeichenzahl zu schätzen: die frühere
    /// Schätzung (26 + Zeichen × 7,5) unterschätzte kurze Labels mit breiten Großbuchstaben ("TV-Modus") —
    /// im LabelOnRight-Layout nimmt sich das Label dann seinen Platz und die Icon-Viewbox schrumpft auf 1×1 px,
    /// das Icon erscheint nur als Punkt (#337, per Visual-Tree-Diagnose belegt). Zuschlag = Icon-Viewbox
    /// (16 px) + Innenränder/Abstände des AppBarButton-Templates (gemessen ~33 px).</summary>
    private static double BreiteFuerLabel(string label)
    {
        var messText = new TextBlock { Text = label, FontSize = LABEL_SCHRIFTGROESSE };
        if (Microsoft.UI.Xaml.Application.Current.Resources.TryGetValue("ContentControlThemeFontFamily", out var schrift)
            && schrift is Microsoft.UI.Xaml.Media.FontFamily fontFamily)
            messText.FontFamily = fontFamily;
        messText.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
        return Math.Max(MINDESTBREITE, Math.Ceiling(messText.DesiredSize.Width) + ICON_BREITE + RAND_UND_ABSTAENDE);
    }
}

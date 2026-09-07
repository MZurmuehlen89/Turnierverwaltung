using Fussballturnierverwaltung.WinUi.Dialoge;
using Fussballturnierverwaltung.WinUi.Helpers;
using Fussballturnierverwaltung.WinUi.Ribbon;
using Fussballturnierverwaltung.WinUi.ViewModels;
using Microsoft.UI.Xaml;
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

    public MainViewModel ViewModel { get; }

    public MainWindow(MainViewModel viewModel, ThemeService themeService, IDialogService dialogService)
    {
        ViewModel = viewModel;
        _themeService = themeService;
        _dialogService = dialogService;

        InitializeComponent();

        RootGrid.DataContext = ViewModel;
        RootGrid.Loaded += (_, _) => _dialogService.XamlRootSetzen(RootGrid.XamlRoot);
        _dialogService.FensterSetzen(this);

        _themeService.RootSetzen(RootGrid);
        _themeService.AnwendenAusEinstellungen();

        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        NeuUnterpunkteAktualisieren();
        RibbonZusatzGruppenAktualisieren();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.RibbonZusatzGruppen) or nameof(MainViewModel.RibbonNeuUnterpunkte))
        {
            NeuUnterpunkteAktualisieren();
            RibbonZusatzGruppenAktualisieren();
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
            AktionenPanel.Children.RemoveAt(2);
        _neuUnterpunkteAnzahl = 0;

        var unterpunkte = ViewModel.RibbonNeuUnterpunkte;
        for (var i = 0; i < unterpunkte.Count; i++)
            AktionenPanel.Children.Insert(2 + i, RibbonButtonErstellen(unterpunkte[i]));
        _neuUnterpunkteAnzahl = unterpunkte.Count;
    }

    private void RibbonZusatzGruppenAktualisieren()
    {
        var start = AnzahlFesterAktionenBasis + _neuUnterpunkteAnzahl;
        while (AktionenPanel.Children.Count > start)
            AktionenPanel.Children.RemoveAt(start);

        var gruppen = ViewModel.RibbonZusatzGruppen;
        for (var g = 0; g < gruppen.Count; g++)
        {
            AktionenPanel.Children.Add(new AppBarSeparator());
            foreach (var aktion in gruppen[g].Aktionen)
                AktionenPanel.Children.Add(RibbonButtonErstellen(aktion));
        }
    }

    /// <summary>Erzeugt einen <see cref="AppBarButton"/> aus einer <see cref="RibbonAktion"/> — entweder mit
    /// direktem <see cref="RibbonAktion.Befehl"/> oder, falls gesetzt, mit einem Flyout aus <see
    /// cref="RibbonAktion.ToggleUnterpunkte"/> (an-/abschaltbare Menüpunkte, z.B. Auslosungs-Regeln).
    /// Explizite <see cref="AppBarButton.Width"/> statt der AppBarButton-Standardbreite: das Standard-Layout
    /// bricht/bschneidet bei mehrzeiligem Label nicht sauber um, längere deutsche Beschriftungen ("Lostöpfe
    /// automatisch befüllen") passten sonst nicht einzeilig in den Button (Nutzerfund nach #300-Test).</summary>
    private static AppBarButton RibbonButtonErstellen(RibbonAktion aktion)
    {
        var button = new AppBarButton { Label = aktion.Header, Width = BreiteFuerLabel(aktion.Header) };
        if (aktion.IconGlyph is not null)
            button.Icon = new FontIcon { Glyph = aktion.IconGlyph };

        if (aktion.ToggleUnterpunkte is { Count: > 0 } toggles)
        {
            var flyout = new MenuFlyout();
            foreach (var toggle in toggles)
            {
                var item = new ToggleMenuFlyoutItem { Text = toggle.Header, IsChecked = toggle.IstAktiv };
                if (toggle.Beschreibung is not null)
                    ToolTipService.SetToolTip(item, toggle.Beschreibung);
                item.Click += (_, _) => toggle.Setzen(item.IsChecked);
                flyout.Items.Add(item);
            }
            button.Flyout = flyout;
        }
        else
        {
            button.Command = aktion.Befehl;
        }

        return button;
    }

    private static double BreiteFuerLabel(string label) => Math.Max(90, 26 + label.Length * 7.5);
}

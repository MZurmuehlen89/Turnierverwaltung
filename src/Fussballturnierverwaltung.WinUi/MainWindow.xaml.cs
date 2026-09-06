using Fussballturnierverwaltung.WinUi.Dialoge;
using Fussballturnierverwaltung.WinUi.Helpers;
using Fussballturnierverwaltung.WinUi.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.ComponentModel;

namespace Fussballturnierverwaltung.WinUi;

public sealed partial class MainWindow : Window
{
    // Titel/Neu/Speichern/Abbrechen/Separator/Löschen aus dem XAML in AktionenPanel (CommandBar.Content,
    // fensterseitig links verankert) — dahinter werden die dynamischen RibbonZusatzGruppen angehängt
    // (CommandBar unterstützt kein ItemsSource-Binding für PrimaryCommands).
    private const int AnzahlFesterAktionen = 6;

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
        RibbonZusatzGruppenAktualisieren();
        NeuFlyoutAktualisieren();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.RibbonZusatzGruppen))
            RibbonZusatzGruppenAktualisieren();
        if (e.PropertyName is nameof(MainViewModel.RibbonNeuUnterpunkte) or nameof(MainViewModel.RibbonNeuBefehl))
            NeuFlyoutAktualisieren();
    }

    /// <summary>Hängt bei vorhandenen <see cref="MainViewModel.RibbonNeuUnterpunkte"/> (Issue #297-Nachzug,
    /// z.B. "Aus Vorlage erstellen") ein Flyout an den "Neu"-Button — WinUI löst bei gesetztem
    /// <c>AppBarButton.Flyout</c> beim Klick NICHT mehr das <c>Command</c> aus, daher wird "Neues Turnier"
    /// als erster Menüpunkt mitgeliefert, um den direkten Klick-Fall weiterhin verfügbar zu halten. Ohne
    /// Unterpunkte bleibt der Button unverändert direkt klickbar (Flyout = null).</summary>
    private void NeuFlyoutAktualisieren()
    {
        var unterpunkte = ViewModel.RibbonNeuUnterpunkte;
        if (unterpunkte.Count == 0)
        {
            NeuButton.Flyout = null;
            return;
        }

        var menu = new MenuFlyout();
        menu.Items.Add(new MenuFlyoutItem
        {
            Text = "Neues Turnier",
            Command = ViewModel.RibbonNeuBefehl,
            Icon = new SymbolIcon(Symbol.Add)
        });
        foreach (var aktion in unterpunkte)
        {
            var item = new MenuFlyoutItem { Text = aktion.Header, Command = aktion.Befehl };
            if (aktion.IconGlyph is not null)
                item.Icon = new FontIcon { Glyph = aktion.IconGlyph };
            menu.Items.Add(item);
        }
        NeuButton.Flyout = menu;
    }

    private void RibbonZusatzGruppenAktualisieren()
    {
        while (AktionenPanel.Children.Count > AnzahlFesterAktionen)
            AktionenPanel.Children.RemoveAt(AnzahlFesterAktionen);

        var gruppen = ViewModel.RibbonZusatzGruppen;
        for (var g = 0; g < gruppen.Count; g++)
        {
            AktionenPanel.Children.Add(new AppBarSeparator());
            foreach (var aktion in gruppen[g].Aktionen)
            {
                var button = new AppBarButton { Label = aktion.Header, Command = aktion.Befehl };
                if (aktion.IconGlyph is not null)
                    button.Icon = new FontIcon { Glyph = aktion.IconGlyph };
                AktionenPanel.Children.Add(button);
            }
        }
    }
}

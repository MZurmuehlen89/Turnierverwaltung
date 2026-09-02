using Fussballturnierverwaltung.WinUi.Dialoge;
using Fussballturnierverwaltung.WinUi.Helpers;
using Fussballturnierverwaltung.WinUi.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.ComponentModel;

namespace Fussballturnierverwaltung.WinUi;

public sealed partial class MainWindow : Window
{
    // Dark-Mode-Toggle/Separator/Neu/Speichern/Abbrechen/Separator/Löschen aus dem XAML — dahinter werden
    // die dynamischen RibbonZusatzGruppen eingefügt (CommandBar unterstützt kein ItemsSource-Binding für
    // PrimaryCommands).
    private const int AnzahlFesterPrimaryCommands = 7;

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

        _themeService.RootSetzen(RootGrid);
        _themeService.AnwendenAusEinstellungen();

        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        RibbonZusatzGruppenAktualisieren();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.RibbonZusatzGruppen))
            RibbonZusatzGruppenAktualisieren();
    }

    private void RibbonZusatzGruppenAktualisieren()
    {
        while (HauptCommandBar.PrimaryCommands.Count > AnzahlFesterPrimaryCommands)
            HauptCommandBar.PrimaryCommands.RemoveAt(AnzahlFesterPrimaryCommands);

        var gruppen = ViewModel.RibbonZusatzGruppen;
        for (var g = 0; g < gruppen.Count; g++)
        {
            HauptCommandBar.PrimaryCommands.Add(new AppBarSeparator());
            foreach (var aktion in gruppen[g].Aktionen)
            {
                var button = new AppBarButton { Label = aktion.Header, Command = aktion.Befehl };
                if (aktion.IconGlyph is not null)
                    button.Icon = new FontIcon { Glyph = aktion.IconGlyph };
                HauptCommandBar.PrimaryCommands.Add(button);
            }
        }
    }
}

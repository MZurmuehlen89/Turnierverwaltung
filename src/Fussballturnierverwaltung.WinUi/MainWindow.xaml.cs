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
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.RibbonZusatzGruppen))
            RibbonZusatzGruppenAktualisieren();
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

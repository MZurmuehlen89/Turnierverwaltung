using Fussballturnierverwaltung.WinUi.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Fussballturnierverwaltung.WinUi;

/// <summary>View-Locator für den Inhaltsbereich: x:Bind ist stark typisiert und kann
/// MainViewModel.AktuellesViewModel (ViewModelBase) nicht auf die konkrete View mappen. Anders als
/// WPFs implizite DataType-DataTemplates löst WinUI 3 das nicht zuverlässig automatisch auf — daher
/// expliziter Selector, der je Fach-View (#281–#290) um einen Case + eine Template-Property erweitert
/// wird, analog zum WPF-DataTemplate-Eintrag in MainWindow.xaml.</summary>
public class ViewModelTemplateSelector : DataTemplateSelector
{
    public DataTemplate? PlatzhalterTemplate { get; set; }
    public DataTemplate? MannschaftenTemplate { get; set; }
    public DataTemplate? LaenderUndVerbaendeTemplate { get; set; }
    public DataTemplate? RegelwerkeTemplate { get; set; }
    public DataTemplate? TurnierlisteTemplate { get; set; }
    public DataTemplate? TurnierdurchfuehrungTemplate { get; set; }
    public DataTemplate? RanglistenTemplate { get; set; }
    public DataTemplate? EinzelspielSimulationsTemplate { get; set; }

    protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container)
        => item switch
        {
            MannschaftenViewModel => MannschaftenTemplate,
            LaenderUndVerbaendeViewModel => LaenderUndVerbaendeTemplate,
            RegelwerkeViewModel => RegelwerkeTemplate,
            TurnierlisteViewModel => TurnierlisteTemplate,
            TurnierdurchfuehrungViewModel => TurnierdurchfuehrungTemplate,
            RanglistenViewModel => RanglistenTemplate,
            EinzelspielSimulationsViewModel => EinzelspielSimulationsTemplate,
            PlatzhalterViewModel => PlatzhalterTemplate,
            _ => null,
        };
}

using Fussballturnierverwaltung.WinUi.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Windows.ApplicationModel.DataTransfer;

namespace Fussballturnierverwaltung.WinUi.Views;

/// <summary>Tausch zwischen „Weitere verfügbar“ und dem Teilnehmerpool (#576) — dieselben drei Wege wie bei den Lostöpfen
/// (<see cref="AuslosungPage"/>): Doppelklick, Ziehen und Rechtsklick, jeweils auch für eine Mehrfachauswahl.</summary>
public sealed partial class TeilnehmerpoolAnsicht : UserControl
{
    public TeilnehmerpoolAnsicht()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => PoolGruppenBinden();
    }

    private TeilnehmerpoolViewModel? ViewModel => DataContext as TeilnehmerpoolViewModel;

    // Die gruppierte Quelle lebt in den Ressourcen, die keinen DataContext erben — darum hier verdrahtet.
    private void PoolGruppenBinden()
    {
        if (ViewModel is not { } viewModel) return;
        var quelle = (CollectionViewSource)Resources["PoolGruppenQuelle"];
        quelle.Source = viewModel.Gruppen;
        PoolListView.ItemsSource = quelle.View;
    }

    private bool IstPoolListe(object sender) => ReferenceEquals(sender, PoolListView);

    // Quelle des laufenden Drags: welche Teams aus welcher Liste (wie AuslosungPage._pendingDrag).
    private (IReadOnlyList<TeilnehmerAnzeige> Teilnehmer, bool AusPool)? _pendingDrag;

    private void Liste_DragItemsStarting(object sender, DragItemsStartingEventArgs e)
    {
        var teams = e.Items.OfType<TeilnehmerpoolZeile>().Select(z => z.Teilnehmer).ToList();
        if (teams.Count == 0) return;
        _pendingDrag = (teams, IstPoolListe(sender));
        e.Data.SetText(string.Join("\n", teams.Select(t => t.AnzeigeName)));
        e.Data.RequestedOperation = DataPackageOperation.Move;
    }

    private void Liste_DragOver(object sender, DragEventArgs e)
    {
        if (_pendingDrag is { } drag && drag.AusPool != IstPoolListe(sender))
            e.AcceptedOperation = DataPackageOperation.Move;
    }

    private async void WeitereListView_Drop(object sender, DragEventArgs e)
    {
        var drag = _pendingDrag;
        _pendingDrag = null;
        if (ViewModel is { } viewModel && drag is { AusPool: true })
            await viewModel.HerausnehmenAsync(drag.Value.Teilnehmer);
    }

    private async void PoolListView_Drop(object sender, DragEventArgs e)
    {
        var drag = _pendingDrag;
        _pendingDrag = null;
        if (ViewModel is { } viewModel && drag is { AusPool: false })
            await viewModel.AufnehmenAsync(drag.Value.Teilnehmer);
    }

    private async void Liste_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (ViewModel is not { } viewModel || sender is not ListView { SelectedItem: TeilnehmerpoolZeile zeile }) return;
        e.Handled = true;
        await TauschenAsync(viewModel, IstPoolListe(sender), [zeile.Teilnehmer]);
    }

    private void Liste_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if (ViewModel is not { } viewModel || sender is not ListView liste) return;
        if (e.OriginalSource is not FrameworkElement { DataContext: TeilnehmerpoolZeile zeile } ziel) return;

        var ausgewaehlt = ListViewAuswahl.Ziel(liste, zeile).Select(z => z.Teilnehmer).ToList();
        bool ausPool = IstPoolListe(sender);
        var eintrag = new MenuFlyoutItem
        {
            Text = ausPool ? "Aus dem Teilnehmerpool nehmen" : "In den Teilnehmerpool aufnehmen",
        };
        eintrag.Click += async (_, _) => await TauschenAsync(viewModel, ausPool, ausgewaehlt);
        var menue = new MenuFlyout();
        menue.Items.Add(eintrag);
        menue.ShowAt(ziel);
    }

    private static Task TauschenAsync(TeilnehmerpoolViewModel viewModel, bool ausPool, IReadOnlyList<TeilnehmerAnzeige> teilnehmer) =>
        ausPool ? viewModel.HerausnehmenAsync(teilnehmer) : viewModel.AufnehmenAsync(teilnehmer);
}

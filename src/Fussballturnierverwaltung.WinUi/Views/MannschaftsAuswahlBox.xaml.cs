using Fussballturnierverwaltung.WinUi.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace Fussballturnierverwaltung.WinUi.Views;

/// <summary>Auswahlbox für eine Mannschaft des Turniers mit Suche beim Tippen (#479). Der DataContext ist ein
/// <see cref="MannschaftsfilterViewModel"/>. Nach einer bestätigten Wahl meldet die Box <see cref="Bestaetigt"/> —
/// die Ansicht setzt dann den Fokus woandershin, sonst klappt die Vorschlagsliste beim nächsten Tastendruck wieder
/// auf.</summary>
public sealed partial class MannschaftsAuswahlBox : UserControl
{
    public static readonly DependencyProperty PlaceholderTextProperty = DependencyProperty.Register(
        nameof(PlaceholderText), typeof(string), typeof(MannschaftsAuswahlBox),
        new PropertyMetadata("", (d, e) => ((MannschaftsAuswahlBox)d).Box.PlaceholderText = (string)e.NewValue));

    public string PlaceholderText
    {
        get => (string)GetValue(PlaceholderTextProperty);
        set => SetValue(PlaceholderTextProperty, value);
    }

    // Ein UserControl erscheint nicht verlässlich im UIA-Baum – die AutomationId gehört an die Box selbst (#535).
    public static readonly DependencyProperty SuchfeldAutomationIdProperty = DependencyProperty.Register(
        nameof(SuchfeldAutomationId), typeof(string), typeof(MannschaftsAuswahlBox),
        new PropertyMetadata("", (d, e) => AutomationProperties.SetAutomationId(((MannschaftsAuswahlBox)d).Box, (string)e.NewValue)));

    public string SuchfeldAutomationId
    {
        get => (string)GetValue(SuchfeldAutomationIdProperty);
        set => SetValue(SuchfeldAutomationIdProperty, value);
    }

    public event EventHandler? Bestaetigt;

    public MannschaftsAuswahlBox()
    {
        InitializeComponent();

        // handledEventsToo: das Textfeld der Box behandelt den Mausklick selbst.
        Box.AddHandler(PointerPressedEvent, new PointerEventHandler(Box_PointerPressed), true);
        Box.GettingFocus += (_, args) =>
        {
            if (args.FocusState == FocusState.Keyboard) _nutzerGesteuert = true;
        };
        Box.LosingFocus += (_, args) => _fokusInVorschlaegen = IstInVorschlagsliste(args.NewFocusedElement);
        Box.RegisterPropertyChangedCallback(AutoSuggestBox.IsSuggestionListOpenProperty, (_, _) => SelbstAufklappenVerhindern());
    }

    // Die Liste gehört nur offen, wenn der Nutzer die Box selbst ansteuert (Klick hinein, Tab-Taste, Tippen). Die
    // AutoSuggestBox klappt sie aber schon von sich aus auf, sobald sie mit Vorschlägen den Fokus bekommt — und den
    // verteilt WinUI auch selbst: beim Klick in eine freie Fläche an das erste fokussierbare Element darunter, und
    // verschwindet das fokussierte Element (Reiterwechsel per Link), an das erste Element der neuen Ansicht
    // (Log-Diagnose #324).
    private bool _nutzerGesteuert;
    private bool _fokusInVorschlaegen;

    private void SelbstAufklappenVerhindern()
    {
        if (Box.IsSuggestionListOpen && !_nutzerGesteuert) Box.IsSuggestionListOpen = false;
    }

    private MannschaftsfilterViewModel? Filter => DataContext as MannschaftsfilterViewModel;

    private void Box_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        // Übernimmt der Spielplan eine Wahl, setzt er den Text der Box neu — dann liegt der Fokus schon woanders, die
        // Box meldet die Änderung aber trotzdem als Eingabe und klappt ihre Liste wieder auf. Eingabe ist nur, was in
        // der fokussierten Box geschieht.
        if (!BoxHatFokus())
        {
            sender.IsSuggestionListOpen = false;
            return;
        }
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;
        _nutzerGesteuert = true;
        Filter?.Suchen(sender.Text);
    }

    private bool BoxHatFokus()
    {
        if (XamlRoot is null) return false;
        for (var element = FocusManager.GetFocusedElement(XamlRoot) as DependencyObject; element is not null;
             element = VisualTreeHelper.GetParent(element))
        {
            if (ReferenceEquals(element, Box)) return true;
        }
        return false;
    }

    private void Box_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (Filter is not { } filter) return;
        _ = filter.BestaetigenAsync(args.ChosenSuggestion as MannschaftsOption);
        Bestaetigt?.Invoke(this, EventArgs.Empty);
    }

    // Wie eine Auswahlbox: beim Hineinklicken bzw. Hineintabben alle Mannschaften zeigen. Der Mausklick kommt erst
    // nach dem Fokuswechsel an — ein Selbst-Aufklappen davor ist schon wieder zu, hier geht die Liste gewollt auf.
    private void Box_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _nutzerGesteuert = true;
        AlleVorschlaegeAufklappen();
    }

    private void Box_GotFocus(object sender, RoutedEventArgs e)
    {
        if (_nutzerGesteuert) AlleVorschlaegeAufklappen();
    }

    private void AlleVorschlaegeAufklappen()
    {
        if (Filter is not { } filter) return;
        filter.AlleVorschlaegeZeigen();
        Box.IsSuggestionListOpen = true;
    }

    private void Box_LostFocus(object sender, RoutedEventArgs e)
    {
        // Ein Klick in die offene Vorschlagsliste nimmt der Box kurz den Fokus — das ist keine verlassene Eingabe.
        // Jeder andere Fokusverlust schließt die Liste (sie wurde von Hand geöffnet und schließt nicht von selbst).
        if (_fokusInVorschlaegen) return;
        _nutzerGesteuert = false;
        Box.IsSuggestionListOpen = false;
        _ = Filter?.FokusVerlorenAsync();
    }

    // Die Vorschlagsliste ist das Vorlagenteil „SuggestionsList“ der AutoSuggestBox; sie liegt in einem Popup, also
    // nicht unter der Box im Elementbaum.
    private static bool IstInVorschlagsliste(DependencyObject? element)
    {
        for (; element is not null; element = VisualTreeHelper.GetParent(element))
            if (element is FrameworkElement { Name: "SuggestionsList" }) return true;
        return false;
    }
}

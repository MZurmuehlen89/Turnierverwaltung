using Fussballturnierverwaltung.Application.Abstraktionen;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace Fussballturnierverwaltung.WinUi.Views;

/// <summary>Markiert ein Eingabefeld mit Prüffehlern (#595): roter Rahmen (2 px) und leicht roter Hintergrund, auch bei
/// Hover und Fokus, ein rötlicher Tooltip mit dem Fehlertext und <c>AutomationProperties.HelpText</c> (Screenreader,
/// GUI-Tests). <see cref="MeldungenProperty"/> bekommt alle Meldungen des Formulars bzw. der Zeile,
/// <see cref="FeldProperty"/> den Feldschlüssel aus dem Validator; das Feld zeigt nur die Meldungen mit diesem Schlüssel.
/// <see cref="Anspringen"/> sucht das Feld zu einer Meldung (Sprung aus der <see cref="MeldungsLeiste"/>).</summary>
public static class Feldmarkierung
{
    private const string RESSOURCEN_PFAD = "ms-appx:///Views/FeldmarkierungRessourcen.xaml";

    public static readonly DependencyProperty MeldungenProperty = DependencyProperty.RegisterAttached(
        "Meldungen", typeof(object), typeof(Feldmarkierung), new PropertyMetadata(null, Geaendert));

    public static readonly DependencyProperty FeldProperty = DependencyProperty.RegisterAttached(
        "Feld", typeof(string), typeof(Feldmarkierung), new PropertyMetadata(null, Geaendert));

    // Je Feld ein eigenes Wörterbuch: solange es in Resources.MergedDictionaries hängt, ist das Feld markiert.
    private static readonly DependencyProperty RessourcenProperty = DependencyProperty.RegisterAttached(
        "Ressourcen", typeof(ResourceDictionary), typeof(Feldmarkierung), new PropertyMetadata(null));

    private static readonly DependencyProperty EigenerToolTipProperty = DependencyProperty.RegisterAttached(
        "EigenerToolTip", typeof(object), typeof(Feldmarkierung), new PropertyMetadata(null));

    private static Style? _toolTipStil;

    // object statt IEnumerable<Feldfehler>: XAML-Bindungen liefern verschiedene Listentypen.
    public static object? GetMeldungen(DependencyObject element) => element.GetValue(MeldungenProperty);
    public static void SetMeldungen(DependencyObject element, object? wert) => element.SetValue(MeldungenProperty, wert);

    public static string? GetFeld(DependencyObject element) => (string?)element.GetValue(FeldProperty);
    public static void SetFeld(DependencyObject element, string? wert) => element.SetValue(FeldProperty, wert);

    // ── Anzeige ───────────────────────────────────────────────────────────────

    private static void Geaendert(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Control feld) return;
        var text = FehlerText(feld);
        RahmenSetzen(feld, text is not null);
        HinweisSetzen(feld, text);
    }

    private static IEnumerable<Feldfehler> EigeneMeldungen(DependencyObject feld)
    {
        var schluessel = GetFeld(feld);
        return schluessel is null || GetMeldungen(feld) is not IEnumerable<Feldfehler> meldungen
            ? []
            : meldungen.Where(m => m.Feld == schluessel);
    }

    private static string? FehlerText(DependencyObject feld)
    {
        var texte = EigeneMeldungen(feld).Select(m => m.Text).ToList();
        return texte.Count == 0 ? null : string.Join(Environment.NewLine, texte);
    }

    private static void RahmenSetzen(Control feld, bool markiert)
    {
        var ressourcen = (ResourceDictionary?)feld.GetValue(RessourcenProperty);
        if (markiert == ressourcen is not null) return;

        if (markiert)
        {
            ressourcen = new ResourceDictionary { Source = new Uri(RESSOURCEN_PFAD) };
            feld.Resources.MergedDictionaries.Add(ressourcen);
        }
        else
        {
            feld.Resources.MergedDictionaries.Remove(ressourcen);
            ressourcen = null;
        }
        feld.SetValue(RessourcenProperty, ressourcen);
        ThemeNeuAufloesen(feld);
    }

    /// <summary>Das Template löst {ThemeResource …} nur beim Anwenden und bei einem Themewechsel auf – neue Schlüssel in
    /// Resources greifen sonst erst später. Ein kurzer Wechsel hin und zurück löst die Schlüssel im selben Durchlauf neu
    /// auf, ohne das Template neu aufzubauen (Fokus und Cursor bleiben).</summary>
    private static void ThemeNeuAufloesen(FrameworkElement feld)
    {
        var bisher = feld.RequestedTheme;
        feld.RequestedTheme = feld.ActualTheme == ElementTheme.Dark ? ElementTheme.Light : ElementTheme.Dark;
        feld.RequestedTheme = bisher;
    }

    private static void HinweisSetzen(Control feld, string? text)
    {
        AutomationProperties.SetHelpText(feld, text ?? "");
        if (text is null)
        {
            EigenenToolTipWiederherstellen(feld);
            return;
        }
        if (ToolTipService.GetToolTip(feld) is ToolTip tip && tip.Style == ToolTipStil())
        {
            tip.Content = text;
            return;
        }
        EigenenToolTipMerken(feld);
        ToolTipService.SetToolTip(feld, new ToolTip { Content = text, Style = ToolTipStil() });
    }

    // Ein Feld kann einen eigenen Tooltip haben (z. B. „ab Platz“ der Landes-Quote, gebunden). Solange es markiert ist,
    // zeigt es den Fehler; danach kommt der eigene zurück – eine Bindung als Bindung, sonst der Wert.
    private static void EigenenToolTipMerken(Control feld)
    {
        object? eigener = feld.GetBindingExpression(ToolTipService.ToolTipProperty)?.ParentBinding
                          ?? ToolTipService.GetToolTip(feld);
        feld.SetValue(EigenerToolTipProperty, eigener);
    }

    private static void EigenenToolTipWiederherstellen(Control feld)
    {
        if (ToolTipService.GetToolTip(feld) is not ToolTip { } tip || tip.Style != ToolTipStil()) return;
        switch (feld.GetValue(EigenerToolTipProperty))
        {
            case Binding bindung: feld.SetBinding(ToolTipService.ToolTipProperty, bindung); break;
            case { } wert: ToolTipService.SetToolTip(feld, wert); break;
            default: feld.ClearValue(ToolTipService.ToolTipProperty); break;
        }
        feld.ClearValue(EigenerToolTipProperty);
    }

    private static Style ToolTipStil() =>
        _toolTipStil ??= (Style)new ResourceDictionary { Source = new Uri(RESSOURCEN_PFAD) }["FeldfehlerToolTipStil"];

    // ── Sprung aus der Meldungsleiste ─────────────────────────────────────────

    /// <summary>Fokussiert unterhalb von <paramref name="wurzel"/> das Feld, das <paramref name="meldung"/> anzeigt.
    /// Liegt es in einem zugeklappten Bereich, dessen Inhalt noch nicht aufgebaut ist, klappt es die Bereiche der Reihe
    /// nach auf, bis es das Feld findet (die übrigen bleiben zu).</summary>
    public static bool Anspringen(DependencyObject wurzel, Feldfehler meldung)
    {
        var feld = FeldSuchen(wurzel, meldung) ?? FeldInZugeklapptemBereichSuchen(wurzel, meldung);
        if (feld is null) return false;

        BereicheAufklappen(feld);
        feld.UpdateLayout();
        feld.Focus(FocusState.Programmatic);
        feld.StartBringIntoView();
        return true;
    }

    private static Control? FeldSuchen(DependencyObject wurzel, Feldfehler meldung) =>
        // Referenzgleichheit: gleich lautende Meldungen zweier Listen (z. B. Verband- und Landes-Quote) sind verschiedene Felder.
        Nachfahren(wurzel).OfType<Control>().FirstOrDefault(c => EigeneMeldungen(c).Any(m => ReferenceEquals(m, meldung)));

    private static Control? FeldInZugeklapptemBereichSuchen(DependencyObject wurzel, Feldfehler meldung)
    {
        foreach (var bereich in Nachfahren(wurzel).OfType<Expander>().Where(x => !x.IsExpanded).ToList())
        {
            bereich.IsExpanded = true;
            bereich.UpdateLayout();
            if (FeldSuchen(bereich, meldung) is { } feld) return feld;
            bereich.IsExpanded = false;
        }
        return null;
    }

    private static void BereicheAufklappen(DependencyObject feld)
    {
        for (var vorfahr = VisualTreeHelper.GetParent(feld); vorfahr is not null; vorfahr = VisualTreeHelper.GetParent(vorfahr))
            if (vorfahr is Expander { IsExpanded: false } bereich) bereich.IsExpanded = true;
    }

    private static IEnumerable<DependencyObject> Nachfahren(DependencyObject wurzel)
    {
        var offen = new Stack<DependencyObject>([wurzel]);
        while (offen.Count > 0)
        {
            var element = offen.Pop();
            yield return element;
            for (int i = VisualTreeHelper.GetChildrenCount(element) - 1; i >= 0; i--)
                offen.Push(VisualTreeHelper.GetChild(element, i));
        }
    }
}

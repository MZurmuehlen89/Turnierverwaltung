using Fussballturnierverwaltung.WinUi.Helpers;
using Fussballturnierverwaltung.WinUi.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Windows.Input;

namespace Fussballturnierverwaltung.WinUi.Views;

/// <summary>Kontextmenüs an Spielen (#62): an einer Zeile der Spielliste (<see cref="ZeilenMenue"/>, die Liste wählt die
/// Zeile vorher aus) und an einer Karte des Turnierbaums (angehängte Eigenschaft <c>Karte</c>). Nicht passende Einträge
/// stehen ausgegraut mit Begründung da, wie im Ribbon. Die Mannschaftseinträge kommen aus
/// <see cref="MannschaftsKontextmenue"/>.</summary>
public static class SpielKontextmenue
{
    private const string GRUND_GEGNER_OFFEN = "Gegner stehen noch nicht fest";
    private const string GRUND_KEIN_ERGEBNIS = "noch kein Ergebnis";

    // ── Spielliste ────────────────────────────────────────────────────────────

    /// <summary>Menü der Spielliste für <paramref name="auswahl"/> (die gewählten Zeilen, darunter die angeklickte
    /// <paramref name="zeile"/>). Die Einträge führen die Befehle des Spielplans aus, die auf der Auswahl arbeiten — sie
    /// werden aktiv, sobald das Formular des Spiels geladen ist.</summary>
    public static MenuFlyout ZeilenMenue(SpielplanViewModel vm, SpielZeile zeile, IReadOnlyList<SpielZeile> auswahl)
    {
        var menue = new MenuFlyout();
        if (auswahl.Count > 1)
        {
            MehrfachEintraege(menue, vm, auswahl);
            return menue;
        }

        string? grundOffen = zeile.IstPlatzhalterSpiel ? GRUND_GEGNER_OFFEN : null;
        string? grundOhneErgebnis = zeile.HatErgebnis ? null : GRUND_KEIN_ERGEBNIS;

        menue.Items.Add(Eintrag(zeile.HatErgebnis ? "Ergebnis ändern" : "Ergebnis eingeben", Symbol.Edit, grundOffen,
            () => _ = vm.SpielZeigenAsync(zeile.RundeId, zeile.Id)));
        menue.Items.Add(Eintrag("Simulieren", Symbol.Play,
            grundOffen ?? (zeile.HatErgebnis ? "hat schon ein Ergebnis" : null), vm.Mengen.SpielSimulierenBefehl));
        menue.Items.Add(Eintrag("Ausführlich simulieren …", Symbol.Video, grundOffen, vm.AusfuehrlichSimulierenBefehl));
        menue.Items.Add(Eintrag("Spielverlauf ansehen", Symbol.Clock, grundOhneErgebnis, vm.SpielverlaufAnsehenBefehl));
        menue.Items.Add(Eintrag("Ergebnis löschen", Symbol.Delete, grundOhneErgebnis, vm.LoeschenBefehl));

        // In der Mannschaftsansicht nur der Gegner — die Spiele der gewählten Mannschaft stehen schon da.
        int? gezeigt = vm.Mannschaftsfilter.Gewaehlt?.Id;
        MannschaftsEintraege(menue, vm.MannschaftsSpieleZeigenAsync,
            (zeile.HeimMannschaftId is { } heim && heim != gezeigt ? heim : null, zeile.HeimName),
            (zeile.AuswaertsMannschaftId is { } aus && aus != gezeigt ? aus : null, zeile.AuswaertsName));
        return menue;
    }

    private static void MehrfachEintraege(MenuFlyout menue, SpielplanViewModel vm, IReadOnlyList<SpielZeile> auswahl)
    {
        int mitErgebnis = auswahl.Count(s => s.HatErgebnis);
        menue.Items.Add(Eintrag(vm.AuswahlSimulierenMenuText, Symbol.Play,
            vm.AnzahlMehrfachSimulierbar == 0 ? "keine offenen Spiele" : null, vm.Mengen.AuswahlSimulierenBefehl));
        menue.Items.Add(Eintrag($"Ergebnisse löschen ({mitErgebnis})", Symbol.Delete,
            mitErgebnis == 0 ? "keine Ergebnisse" : null, vm.LoeschenBefehl));
    }

    // ── Turnierbaum ───────────────────────────────────────────────────────────

    /// <summary>Hängt an eine Turnierbaum-Karte (Wert: die <see cref="BracketKnotenAnzeige"/>) das Spielmenü. Ausgeführt
    /// wird es vom nächsten <see cref="ISpielAnzeige"/> im DataContext eines übergeordneten Elements. Ein Freilos hat
    /// kein Menü.</summary>
    public static readonly DependencyProperty KarteProperty = DependencyProperty.RegisterAttached(
        "Karte", typeof(object), typeof(SpielKontextmenue), new PropertyMetadata(null, KarteGeaendert));

    public static object? GetKarte(DependencyObject element) => element.GetValue(KarteProperty);

    public static void SetKarte(DependencyObject element, object? wert) => element.SetValue(KarteProperty, wert);

    private static void KarteGeaendert(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement element) return;
        element.ContextFlyout = e.NewValue is BracketKnotenAnzeige { SpielId: not null, TurnierRundeId: not null } karte
            ? KartenMenue(element, karte)
            : null;
    }

    private static MenuFlyout KartenMenue(UIElement anker, BracketKnotenAnzeige karte)
    {
        var menue = new MenuFlyout();
        int rundeId = karte.TurnierRundeId!.Value;
        if (karte.RueckspielId is { } rueckspielId)
        {
            menue.Items.Add(Untermenue("Hinspiel",
                KartenSpielEintraege(anker, rundeId, karte.SpielId!.Value, karte.SpielHatErgebnis, karte.IstPlatzhalterSpiel)));
            menue.Items.Add(Untermenue("Rückspiel",
                KartenSpielEintraege(anker, rundeId, rueckspielId, karte.RueckspielHatErgebnis, karte.IstPlatzhalterSpiel)));
        }
        else
        {
            foreach (var eintrag in KartenSpielEintraege(
                         anker, rundeId, karte.SpielId!.Value, karte.SpielHatErgebnis, karte.IstPlatzhalterSpiel))
                menue.Items.Add(eintrag);
        }

        MannschaftsEintraege(menue,
            id => MannschaftsKontextmenue.ZielFinden<IMannschaftsSpieleAnzeige>(anker)?.MannschaftsSpieleAnzeigenAsync(id)
                  ?? Task.CompletedTask,
            (karte.HeimMannschaftId, karte.HeimName), (karte.AuswaertsMannschaftId, karte.AuswaertsName));
        return menue;
    }

    private static IEnumerable<MenuFlyoutItem> KartenSpielEintraege(
        UIElement anker, int rundeId, int spielId, bool hatErgebnis, bool istPlatzhalterSpiel)
    {
        yield return Eintrag("Im Spielplan zeigen", Symbol.Edit, null,
            () => _ = Ziel(anker)?.SpielZeigenAsync(rundeId, spielId));
        yield return Eintrag("Ausführlich simulieren …", Symbol.Video, istPlatzhalterSpiel ? GRUND_GEGNER_OFFEN : null,
            () => Ziel(anker)?.SimulationOeffnen(rundeId, spielId, nurAnzeigen: false));
        yield return Eintrag("Spielverlauf ansehen", Symbol.Clock, hatErgebnis ? null : GRUND_KEIN_ERGEBNIS,
            () => Ziel(anker)?.SimulationOeffnen(rundeId, spielId, nurAnzeigen: true));
    }

    private static ISpielAnzeige? Ziel(UIElement anker) => MannschaftsKontextmenue.ZielFinden<ISpielAnzeige>(anker);

    private static MenuFlyoutSubItem Untermenue(string text, IEnumerable<MenuFlyoutItem> eintraege)
    {
        var untermenue = new MenuFlyoutSubItem { Text = text };
        foreach (var eintrag in eintraege) untermenue.Items.Add(eintrag);
        return untermenue;
    }

    // ── Gemeinsame Bausteine ──────────────────────────────────────────────────

    /// <summary>Trennstrich und „Alle Spiele von …“ für jede feststehende Mannschaft.</summary>
    private static void MannschaftsEintraege(
        MenuFlyout menue, Func<int, Task> alleSpieleZeigen, params (int? Id, string Name)[] mannschaften)
    {
        var vorhandene = mannschaften.Where(m => m.Id is not null).ToList();
        if (vorhandene.Count == 0) return;

        menue.Items.Add(new MenuFlyoutSeparator());
        foreach (var (id, name) in vorhandene)
            menue.Items.Add(Eintrag($"Alle Spiele von {name}", Symbol.List, null,
                () => _ = alleSpieleZeigen(id!.Value)));
    }

    /// <summary>Eintrag mit Befehl — die Freigabe folgt dem Befehl, solange kein <paramref name="sperrGrund"/>
    /// vorliegt.</summary>
    private static MenuFlyoutItem Eintrag(string text, Symbol symbol, string? sperrGrund, ICommand befehl)
    {
        var eintrag = GesperrtOder(text, symbol, sperrGrund);
        if (sperrGrund is null) eintrag.Command = befehl;
        return eintrag;
    }

    private static MenuFlyoutItem Eintrag(string text, Symbol symbol, string? sperrGrund, Action aktion)
    {
        var eintrag = GesperrtOder(text, symbol, sperrGrund);
        if (sperrGrund is null) eintrag.Click += (_, _) => aktion();
        return eintrag;
    }

    private static MenuFlyoutItem GesperrtOder(string text, Symbol symbol, string? sperrGrund)
    {
        var eintrag = sperrGrund is null
            ? new MenuFlyoutItem { Text = text }
            : new MenuFlyoutItem { Text = $"{text} ({sperrGrund})", IsEnabled = false };
        eintrag.Icon = new SymbolIcon(symbol);
        eintrag.Click += (_, _) => Ablaufprotokoll.Bedienung(text);
        return eintrag;
    }
}

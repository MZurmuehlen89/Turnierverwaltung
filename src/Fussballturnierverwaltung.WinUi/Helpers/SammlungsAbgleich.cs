using Microsoft.UI.Dispatching;
using System.Collections.ObjectModel;

namespace Fussballturnierverwaltung.WinUi.Helpers;

/// <summary>Gleicht eine gebundene <see cref="ObservableCollection{T}"/> per Schlüssel mit einer Zielliste ab,
/// statt sie neu anzulegen (Issue #388): Eine neue ItemsSource-Instanz lässt eine ListView an den Anfang
/// springen und verwirft Auswahl und Scrollposition. Vorhandene Elemente mit passendem Schlüssel bleiben als
/// Instanz erhalten (bzw. werden verschoben), fehlende werden eingefügt, überzählige entfernt.</summary>
internal static class SammlungsAbgleich
{
    /// <param name="istGleich">Optional: prüft, ob ein vorhandenes Element trotz gleichen Schlüssels inhaltlich
    /// veraltet ist — dann wird es durch das Zielelement ersetzt.</param>
    public static void Abgleichen<T, TSchluessel>(ObservableCollection<T> ist, IReadOnlyList<T> soll,
        Func<T, TSchluessel> schluessel, Func<T, T, bool>? istGleich = null)
    {
        var vergleicher = EqualityComparer<TSchluessel>.Default;

        // Erst Überzählige entfernen (Schlüssel kommen im Ziel nicht mehr vor) — hält die folgenden Suchen kurz.
        var sollSchluessel = soll.Select(schluessel).ToHashSet();
        for (var i = ist.Count - 1; i >= 0; i--)
            if (!sollSchluessel.Contains(schluessel(ist[i]))) ist.RemoveAt(i);

        for (var i = 0; i < soll.Count; i++)
        {
            var ziel = soll[i];
            var zielSchluessel = schluessel(ziel);

            var j = i;
            while (j < ist.Count && !vergleicher.Equals(schluessel(ist[j]), zielSchluessel)) j++;

            if (j == ist.Count)
            {
                ist.Insert(i, ziel);
                continue;
            }
            if (j != i) ist.Move(j, i);
            if (istGleich is not null && !istGleich(ist[i], ziel)) ist[i] = ziel;
        }

        // Rest (z. B. doppelte Schlüssel, die im Ziel seltener vorkommen)
        while (ist.Count > soll.Count) ist.RemoveAt(ist.Count - 1);
    }

    /// <summary>Bringt <paramref name="ziel"/> per reinem Merge auf denselben Inhalt wie <paramref name="soll"/>
    /// — nur <c>RemoveAt</c> für weggefallene und <c>Insert</c> für neu hinzugekommene Einträge, NIE
    /// Indexer-Zuweisung/<c>Replace</c>, NIE <c>Clear()</c>+Neubefüllen und NIE ein bereits vorhandenes
    /// Element entfernen+an anderer Stelle neu einfügen ("Move" per Remove+Insert). Für die Auswahllisten von
    /// ComboBoxen gedacht — <see cref="Abgleichen"/> nutzt <c>Move</c> und Indexer-Zuweisung und ist dort
    /// deshalb nicht verwendbar.
    ///
    /// Zwei WinUI3-Fallstricke wurden hier nacheinander gefunden (#335):
    /// 1. <c>Replace</c>-Notifications lösten eine <c>ArgumentException</c> ("cannot work with indices
    ///    larger than Int32.MaxValue - 1") in der WinRT-ABI-Marshalling-Schicht der gebundenen ComboBox aus
    ///    (<c>ABI.System.Collections.IList.ToAbiHelper.GetAt</c>) — vermutlich ein intern nicht gefundener
    ///    <c>IndexOf</c> (-1), der als riesiger <c>UInt32</c>-Index missinterpretiert wird.
    /// 2. Ein naiver Index-Diff (RemoveAt+Insert pro abweichendem Index) kann ein Element, das seine Position
    ///    wechseln muss, kurzzeitig DUPLIZIEREN (neu an der Zielposition eingefügt, während das alte
    ///    Vorkommen an seiner ursprünglichen — jetzt verschobenen — Position noch nicht bereinigt ist) — bei
    ///    mehreren solcher Operationen in schneller Folge verlor die ComboBox die Selektion komplett (beide
    ///    Felder zeigten nichts mehr an).
    ///
    /// Diese Methode vermeidet beides: Es werden zuerst nur Elemente ENTFERNT, die in <paramref name="soll"/>
    /// nicht mehr vorkommen (die verbleibenden bilden garantiert eine Teilfolge von <paramref name="soll"/>,
    /// da alle Aufrufe stets von derselben festen, sortierten Basisliste filtern), danach werden
    /// ausschließlich fehlende Elemente an ihrer Zielposition eingefügt — kein vorhandenes Element wird je
    /// entfernt und neu eingefügt. Ein stets an Index 0 stehender Sentinel-Eintrag ("— Kein Filter —") bleibt
    /// dadurch unangetastet.</summary>
    public static void TeilfolgeAbgleichen<T>(ObservableCollection<T> ziel, IReadOnlyList<T> soll)
    {
        var behalten = new HashSet<T>(soll);
        for (var i = ziel.Count - 1; i >= 0; i--)
            if (!behalten.Contains(ziel[i]))
                ziel.RemoveAt(i);

        var i2 = 0;
        foreach (var item in soll)
        {
            if (i2 < ziel.Count && EqualityComparer<T>.Default.Equals(ziel[i2], item)) { i2++; continue; }
            ziel.Insert(i2, item);
            i2++;
        }
    }

    /// <summary>Erster Teil eines <see cref="TeilfolgeAbgleichen"/> in zwei Schritten: fügt nur ein, was
    /// <paramref name="ziel"/> aus <paramref name="soll"/> noch fehlt, und entfernt nichts. So kann eine
    /// ComboBox zwischen den Schritten auf einen neuen Eintrag umgestellt werden, bevor ihr bisheriger
    /// verschwindet (#447). <paramref name="alle"/> ist die feste, sortierte Basisliste, aus der beide
    /// stammen — sie gibt die Reihenfolge vor.</summary>
    public static void TeilfolgeErgaenzen<T>(ObservableCollection<T> ziel, IReadOnlyList<T> alle, IReadOnlyList<T> soll)
    {
        var enthalten = new HashSet<T>(ziel);
        enthalten.UnionWith(soll);
        TeilfolgeAbgleichen(ziel, alle.Where(enthalten.Contains).ToList());
    }

    /// <summary>Verschiebt einen Listenabgleich auf den nächsten Dispatcher-Tick statt ihn synchron aus einem
    /// ComboBox-Change-Handler heraus auszuführen. Fund #335: Wird die ItemsSource der EINEN ComboBox verändert,
    /// während der native WinUI-XAML-Kern noch mitten in der Verarbeitung des SelectionChanged der ANDEREN
    /// ComboBox steckt, führte das reproduzierbar zu einer <c>COMException (0x8000FFFF, "Schwerwiegender
    /// Fehler")</c> ohne verwertbaren Managed-Stack. Über <see cref="DispatcherQueue.TryEnqueue(DispatcherQueueHandler)"/>
    /// läuft die Mutation erst, nachdem die aktuelle Bindungsoperation vollständig abgeschlossen ist.</summary>
    public static void Verzoegert(Action abgleichen) =>
        DispatcherQueue.GetForCurrentThread()?.TryEnqueue(() => abgleichen());
}

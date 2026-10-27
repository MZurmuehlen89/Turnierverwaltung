using System.Reflection;
using System.Text.RegularExpressions;
using FluentAssertions;
using Fussballturnierverwaltung.Application.Zufall;
using Fussballturnierverwaltung.Domain;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using NetArchTest.Rules;

namespace Fussballturnierverwaltung.Tests.Architektur;

/// <summary>
/// Die drei Schichten, die das Testprojekt erreicht (WinUi referenziert es bewusst nicht), und die gemeinsame Prüfung
/// der Architekturregeln (#540).
/// </summary>
internal static class Schichten
{
    public const string DOMAIN = "Fussballturnierverwaltung.Domain";
    public const string APPLICATION = "Fussballturnierverwaltung.Application";
    public const string INFRASTRUCTURE = "Fussballturnierverwaltung.Infrastructure";
    public const string WINUI = "Fussballturnierverwaltung.WinUi";

    public static readonly Assembly DomainAssembly = typeof(BasisEntitaet).Assembly;
    public static readonly Assembly ApplicationAssembly = typeof(Zufallsquelle).Assembly;
    public static readonly Assembly InfrastructureAssembly = typeof(TurnierverwaltungDbContext).Assembly;

    /// <summary>Regex für genau diesen Namespace (ResideInNamespace lässt offen, ob es Unter-Namespaces einschließt).</summary>
    public static string Genau(string ns) => $"^{Regex.Escape(ns)}$";

    /// <summary>Regex für diesen Namespace und alle darunter.</summary>
    public static string Darunter(string ns) => $"^{Regex.Escape(ns)}(\\.|$)";

    public static Types Alle() => Types.InAssemblies([DomainAssembly, ApplicationAssembly, InfrastructureAssembly]);

    /// <summary>
    /// Prüft die Regel und nennt bei einem Verstoß jeden betroffenen Typ. Eine Regel, deren Auswahl leer ist, gilt als
    /// Fehler – sonst bliebe sie nach einer Umbenennung unbemerkt grün, ohne noch etwas zu prüfen.
    /// </summary>
    public static void SollteErfuelltSein(this ConditionList regel)
    {
        var ergebnis = regel.GetResult();

        ergebnis.SelectedTypesForTesting.Should().NotBeEmpty("eine Regel ohne ausgewählte Typen prüft nichts");
        (ergebnis.FailingTypes ?? []).Select(t => t.FullName).Should().BeEmpty();
    }
}

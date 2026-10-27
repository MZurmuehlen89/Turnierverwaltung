using static Fussballturnierverwaltung.Tests.Architektur.Schichten;
using NetArchTest.Rules;

namespace Fussballturnierverwaltung.Tests.Architektur;

/// <summary>Abhängigkeitsrichtung Domain ← Application ← Infrastructure ← WinUi (Root-CLAUDE.md, #540).</summary>
public class SchichtabhaengigkeitenTests
{
    private const string EF_CORE = "Microsoft.EntityFrameworkCore";

    [Fact]
    public void Domain_HaengtVonKeinerAnderenSchichtUndKeinemFrameworkAb()
    {
        Types.InAssembly(DomainAssembly)
            .Should().NotHaveDependencyOnAny(APPLICATION, INFRASTRUCTURE, WINUI, EF_CORE, "Microsoft.Extensions")
            .SollteErfuelltSein();
    }

    [Fact]
    public void Application_HaengtNichtVonInfrastructureWinUiOderEfCoreAb()
    {
        // EF Core bleibt in Infrastructure: Application kennt nur die Repository-Interfaces (Bestandsaufnahme #540)
        Types.InAssembly(ApplicationAssembly)
            .Should().NotHaveDependencyOnAny(INFRASTRUCTURE, WINUI, EF_CORE)
            .SollteErfuelltSein();
    }

    [Fact]
    public void Infrastructure_HaengtNichtVonWinUiAb()
    {
        Types.InAssembly(InfrastructureAssembly)
            .Should().NotHaveDependencyOnAny(WINUI)
            .SollteErfuelltSein();
    }
}

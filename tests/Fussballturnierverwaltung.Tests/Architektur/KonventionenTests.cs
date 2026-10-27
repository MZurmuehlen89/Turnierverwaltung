using Fussballturnierverwaltung.Domain;
using Microsoft.EntityFrameworkCore;
using NetArchTest.Rules;
using static Fussballturnierverwaltung.Tests.Architektur.Schichten;

namespace Fussballturnierverwaltung.Tests.Architektur;

/// <summary>
/// Ablage- und Namensregeln, die die Bestandsaufnahme zu #540 als durchgehend eingehalten bestätigt hat.
/// Bewusst ohne Regel: „Services enden auf Service“ – es gibt kein Merkmal, das einen Dienst von anderen Klassen
/// unterscheidet (FinaleAutomatik, KoQuellverknuepfung u. a. arbeiten wie Dienste, heißen aber fachlich).
/// </summary>
public class KonventionenTests
{
    // ── Repositories ────────────────────────

    [Fact]
    public void RepositoryInterfaces_LiegenInApplication()
    {
        Alle().That().AreInterfaces().And().HaveNameEndingWith("Repository")
            .Should().ResideInNamespaceMatching(Darunter(APPLICATION))
            .SollteErfuelltSein();
    }

    [Fact]
    public void RepositoryKlassen_LiegenInInfrastructureRepositories()
    {
        Alle().That().AreClasses().And().HaveNameEndingWith("Repository")
            .Should().ResideInNamespaceMatching(Genau($"{INFRASTRUCTURE}.Repositories"))
            .SollteErfuelltSein();
    }

    // ── Persistenz ────────────────────────

    [Fact]
    public void EfKonfigurationen_EndenAufKonfigurationUndLiegenUnterPersistenzKonfigurationen()
    {
        Types.InAssembly(InfrastructureAssembly).That().ImplementInterface(typeof(IEntityTypeConfiguration<>))
            .Should().HaveNameEndingWith("Konfiguration")
            .And().ResideInNamespaceMatching(Darunter($"{INFRASTRUCTURE}.Persistenz.Konfigurationen"))
            .SollteErfuelltSein();
    }

    // ── Domain ────────────────────────

    [Fact]
    public void Entitaeten_LiegenUnterEntitaeten()
    {
        Types.InAssembly(DomainAssembly).That().Inherit<BasisEntitaet>()
            .Should().ResideInNamespaceMatching(Darunter($"{DOMAIN}.Entitaeten"))
            .SollteErfuelltSein();
    }

    [Fact]
    public void KlassenUnterEntitaeten_SindEntitaeten()
    {
        Types.InAssembly(DomainAssembly)
            .That().AreClasses().And().ResideInNamespaceMatching(Darunter($"{DOMAIN}.Entitaeten"))
            .Should().Inherit<BasisEntitaet>()
            .SollteErfuelltSein();
    }

    [Fact]
    public void DomainEnums_LiegenInEnums()
    {
        Types.InAssembly(DomainAssembly).That().AreEnums()
            .Should().ResideInNamespaceMatching(Genau($"{DOMAIN}.Enums"))
            .SollteErfuelltSein();
    }

    // ── Application ────────────────────────

    [Fact]
    public void Validatoren_LiegenInValidierung()
    {
        // Umkehrung gilt nicht: in Validierung liegen auch BildValidierung, UebergangsregelFeldValidierung und
        // ValidationException
        Alle().That().HaveNameEndingWith("Validator")
            .Should().ResideInNamespaceMatching(Genau($"{APPLICATION}.Validierung"))
            .SollteErfuelltSein();
    }

    // ── Namen ────────────────────────

    [Fact]
    public void Interfaces_BeginnenMitI()
    {
        Alle().That().AreInterfaces()
            .Should().HaveNameStartingWith("I")
            .SollteErfuelltSein();
    }
}

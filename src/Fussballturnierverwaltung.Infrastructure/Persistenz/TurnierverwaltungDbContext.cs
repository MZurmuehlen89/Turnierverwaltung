using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Ranglisten;
using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Fussballturnierverwaltung.Domain.Entitaeten.Simulation;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Turnierbaum;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Microsoft.EntityFrameworkCore;

namespace Fussballturnierverwaltung.Infrastructure.Persistenz;

public class TurnierverwaltungDbContext : DbContext
{
    public TurnierverwaltungDbContext(DbContextOptions<TurnierverwaltungDbContext> options)
        : base(options) { }

    // Stammdaten
    public DbSet<Verband> Verbaende => Set<Verband>();
    public DbSet<Land> Laender => Set<Land>();
    public DbSet<Mannschaft> Mannschaften => Set<Mannschaft>();

    // Regelwerke
    public DbSet<Regelwerk> Regelwerke => Set<Regelwerk>();
    public DbSet<PunkteRegel> PunkteRegeln => Set<PunkteRegel>();
    public DbSet<EntscheidungsVerfahren> Entscheidungsverfahren => Set<EntscheidungsVerfahren>();
    public DbSet<RankingRegel> RankingRegeln => Set<RankingRegel>();
    public DbSet<RankingRegelSet> RankingRegelSets => Set<RankingRegelSet>();
    public DbSet<RankingRegelSetEintrag> RankingRegelSetEintraege => Set<RankingRegelSetEintrag>();

    // Turniere
    public DbSet<TurnierVorlage> TurnierVorlagen => Set<TurnierVorlage>();
    public DbSet<VorlagenRunde> VorlagenRunden => Set<VorlagenRunde>();
    public DbSet<TurnierInstanz> TurnierInstanzen => Set<TurnierInstanz>();
    public DbSet<TurnierRunde> TurnierRunden => Set<TurnierRunde>();

    // Spiele
    public DbSet<Spiel> Spiele => Set<Spiel>();
    public DbSet<SpielErgebnis> SpielErgebnisse => Set<SpielErgebnis>();
    public DbSet<SpielEreignis> SpielEreignisse => Set<SpielEreignis>();

    // Auslosungen
    public DbSet<Auslosung> Auslosungen => Set<Auslosung>();
    public DbSet<Lostopf> Lostoepfe => Set<Lostopf>();
    public DbSet<LostopfEintrag> LostopfEintraege => Set<LostopfEintrag>();
    public DbSet<AuslosungsRegel> AuslosungsRegeln => Set<AuslosungsRegel>();
    public DbSet<AuslosungsErgebnis> AuslosungsErgebnisse => Set<AuslosungsErgebnis>();

    // Turnierbaum
    public DbSet<TurnierBaum> TurnierBaeume => Set<TurnierBaum>();
    public DbSet<TurnierBaumKnoten> TurnierBaumKnotenMenge => Set<TurnierBaumKnoten>();

    // Simulation
    public DbSet<SimulationsLauf> SimulationsLaeufe => Set<SimulationsLauf>();

    // Ranglisten
    public DbSet<Rangliste> Ranglisten => Set<Rangliste>();
    public DbSet<RanglistenEintrag> RanglistenEintraege => Set<RanglistenEintrag>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TurnierverwaltungDbContext).Assembly);
    }
}

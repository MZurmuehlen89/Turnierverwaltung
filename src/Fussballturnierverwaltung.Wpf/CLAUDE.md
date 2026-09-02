# CLAUDE.md — WPF

Gilt zusätzlich zur Root-`CLAUDE.md` bei Arbeit an diesem Projekt.

## WPF Patterns

**MVVM** uses CommunityToolkit.Mvvm. All ViewModels extend `ViewModelBase` (which extends `ObservableObject`). Source generators (`[ObservableProperty]`, `[RelayCommand]`) are used throughout.

**`ViewModelBase.AktivierenAsync()`** is called by `NavigationService` after each navigation. Override it to load data on arrival. Default implementation is a no-op.

**`NavigationService`** creates a fresh DI scope per navigation, resolving the requested ViewModel from it. This ensures each view gets its own `DbContext`. ViewModels are registered as `Transient`; `NavigationService` and `MainViewModel` are `Singleton`.

**`IRibbonAware`** interface: ViewModels that expose Fluent.Ribbon actions (Neu, Speichern, Abbrechen, Löschen) implement this interface. `MainViewModel` forwards the active ViewModel's ribbon commands to the ribbon bar. When no `IRibbonAware` ViewModel is active, the ribbon tab is hidden.

**`TurnierBereichViewModel`** composes `SpielplanViewModel` and `TurnierBaumViewModel` as tabs. It delegates `IRibbonAware` to whichever tab is currently active (`AktiverTabIndex`).

**Navigation groups** in `MainViewModel`:
- *Stammdaten*: Mannschaften, Länder & Verbände, Regelwerke, Turniervorlagen, Ranglisten
- *Turniere*: Turnierkopf, Turnierdefinition (Runden, Aufstiegsregeln, Verband-Quoten), Turnierdurchführung (Auslosung, Spielplan & Baum, Simulation), Einzelspiel-Simulation
- *Import / Export*: Import / Export

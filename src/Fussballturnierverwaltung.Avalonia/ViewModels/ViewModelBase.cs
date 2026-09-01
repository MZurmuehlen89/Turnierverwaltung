using CommunityToolkit.Mvvm.ComponentModel;

namespace Fussballturnierverwaltung.AvaloniaSpike.ViewModels;

public abstract class ViewModelBase : ObservableObject
{
    public virtual Task AktivierenAsync() => Task.CompletedTask;
}

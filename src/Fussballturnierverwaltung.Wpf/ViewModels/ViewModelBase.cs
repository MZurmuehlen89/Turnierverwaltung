using CommunityToolkit.Mvvm.ComponentModel;

namespace Fussballturnierverwaltung.Wpf.ViewModels;

public abstract class ViewModelBase : ObservableObject
{
    public virtual Task AktivierenAsync() => Task.CompletedTask;
}

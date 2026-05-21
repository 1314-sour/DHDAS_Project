using ReactiveUI;
using System.Reactive.Disposables;

namespace DHDAS.Application.Support;

public abstract class PluginViewModelBase : ReactiveObject
{
    // 用于统一管理订阅的释放
    protected CompositeDisposable Disposables { get; } = new();
    public virtual void OnActivated() { }
    public virtual void OnDeactivated() => Disposables.Clear();
}
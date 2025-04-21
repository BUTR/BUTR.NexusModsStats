using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace BUTR.NexusModsStats.Utils;

public sealed class ComposableDisposable : IDisposable
{
    private readonly ConcurrentQueue<IDisposable> _disposables = new();

    [return: NotNullIfNotNull("disposable")]
    public T? Add<T>(T? disposable) where T : IDisposable
    {
        if (disposable is null)
            return default;

        _disposables.Enqueue(disposable);
        return disposable;
    }

    public void Dispose()
    {
        foreach (var disposable in _disposables)
        {
            disposable.Dispose();
        }
    }
}
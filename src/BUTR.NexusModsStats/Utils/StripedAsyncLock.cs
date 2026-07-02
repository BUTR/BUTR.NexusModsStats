namespace BUTR.NexusModsStats.Utils;

/// <summary>
/// Key-based async lock with a fixed number of stripes, so memory usage is bounded
/// no matter how many distinct keys are locked over the process lifetime.
/// Register as a singleton - transient instances cannot coordinate anything.
/// </summary>
public sealed class StripedAsyncLock
{
    private readonly SemaphoreSlim[] _stripes;

    public StripedAsyncLock() : this(stripeCount: 32) { }

    public StripedAsyncLock(int stripeCount)
    {
        _stripes = new SemaphoreSlim[stripeCount];
        for (var i = 0; i < _stripes.Length; i++)
            _stripes[i] = new SemaphoreSlim(1, 1);
    }

    public async ValueTask<Releaser> AcquireAsync(string key, CancellationToken ct)
    {
        var stripe = _stripes[(key.GetHashCode() & int.MaxValue) % _stripes.Length];
        await stripe.WaitAsync(ct);
        return new Releaser(stripe);
    }

    public readonly struct Releaser : IDisposable
    {
        private readonly SemaphoreSlim _stripe;

        internal Releaser(SemaphoreSlim stripe) => _stripe = stripe;

        public void Dispose() => _stripe.Release();
    }
}
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Caching.Memory;

namespace ForgeTrust.Runnable.Caching;

/// <summary>
/// Memoizes async factory calls using <see cref="IMemoryCache"/> with automatic key generation
/// and per-key thundering-herd protection.
/// </summary>
public sealed class Memo : IMemo
{
    private readonly IMemoryCache _cache;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="Memo"/> class.
    /// </summary>
    /// <param name="cache">The underlying memory cache for storage.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="cache"/> is null.</exception>
    public Memo(IMemoryCache cache)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    /// <inheritdoc />
    public Task<TResult> GetAsync<TResult>(
        Func<CancellationToken, Task<TResult>> factory,
        CachePolicy policy,
        CancellationToken cancellationToken = default,
        [CallerMemberName] string callerMemberName = "")
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(policy);

        var key = callerMemberName;
        return GetOrCreateCoreAsync(key, ct => factory(ct), policy, cancellationToken);
    }

    /// <inheritdoc />
    public Task<TResult> GetAsync<TArg, TResult>(
        TArg arg,
        Func<TArg, CancellationToken, Task<TResult>> factory,
        CachePolicy policy,
        CancellationToken cancellationToken = default,
        [CallerMemberName] string callerMemberName = "")
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(policy);

        var key = BuildKey(callerMemberName, arg);
        return GetOrCreateCoreAsync(key, ct => factory(arg, ct), policy, cancellationToken);
    }

    /// <inheritdoc />
    public Task<TResult> GetAsync<TArg1, TArg2, TResult>(
        TArg1 arg1, TArg2 arg2,
        Func<TArg1, TArg2, CancellationToken, Task<TResult>> factory,
        CachePolicy policy,
        CancellationToken cancellationToken = default,
        [CallerMemberName] string callerMemberName = "")
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(policy);

        var key = BuildKey(callerMemberName, arg1, arg2);
        return GetOrCreateCoreAsync(key, ct => factory(arg1, arg2, ct), policy, cancellationToken);
    }

    /// <inheritdoc />
    public Task<TResult> GetAsync<TArg1, TArg2, TArg3, TResult>(
        TArg1 arg1, TArg2 arg2, TArg3 arg3,
        Func<TArg1, TArg2, TArg3, CancellationToken, Task<TResult>> factory,
        CachePolicy policy,
        CancellationToken cancellationToken = default,
        [CallerMemberName] string callerMemberName = "")
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(policy);

        var key = BuildKey(callerMemberName, arg1, arg2, arg3);
        return GetOrCreateCoreAsync(key, ct => factory(arg1, arg2, arg3, ct), policy, cancellationToken);
    }

    /// <inheritdoc />
    public Task<TResult> GetAsync<TArg1, TArg2, TArg3, TArg4, TResult>(
        TArg1 arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4,
        Func<TArg1, TArg2, TArg3, TArg4, CancellationToken, Task<TResult>> factory,
        CachePolicy policy,
        CancellationToken cancellationToken = default,
        [CallerMemberName] string callerMemberName = "")
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(policy);

        var key = BuildKey(callerMemberName, arg1, arg2, arg3, arg4);
        return GetOrCreateCoreAsync(key, ct => factory(arg1, arg2, arg3, arg4, ct), policy, cancellationToken);
    }

    /// <inheritdoc />
    public Task<TResult> GetAsync<TArg1, TArg2, TArg3, TArg4, TArg5, TResult>(
        TArg1 arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4, TArg5 arg5,
        Func<TArg1, TArg2, TArg3, TArg4, TArg5, CancellationToken, Task<TResult>> factory,
        CachePolicy policy,
        CancellationToken cancellationToken = default,
        [CallerMemberName] string callerMemberName = "")
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(policy);

        var key = BuildKey(callerMemberName, arg1, arg2, arg3, arg4, arg5);
        return GetOrCreateCoreAsync(key, ct => factory(arg1, arg2, arg3, arg4, arg5, ct), policy, cancellationToken);
    }

    /// <inheritdoc />
    public Task<TResult> GetAsync<TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TResult>(
        TArg1 arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4, TArg5 arg5, TArg6 arg6,
        Func<TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, CancellationToken, Task<TResult>> factory,
        CachePolicy policy,
        CancellationToken cancellationToken = default,
        [CallerMemberName] string callerMemberName = "")
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(policy);

        var key = BuildKey(callerMemberName, arg1, arg2, arg3, arg4, arg5, arg6);
        return GetOrCreateCoreAsync(
            key, ct => factory(arg1, arg2, arg3, arg4, arg5, arg6, ct), policy, cancellationToken);
    }

    /// <inheritdoc />
    public Task<TResult> GetAsync<TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TResult>(
        TArg1 arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4, TArg5 arg5, TArg6 arg6, TArg7 arg7,
        Func<TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, CancellationToken, Task<TResult>> factory,
        CachePolicy policy,
        CancellationToken cancellationToken = default,
        [CallerMemberName] string callerMemberName = "")
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(policy);

        var key = BuildKey(callerMemberName, arg1, arg2, arg3, arg4, arg5, arg6, arg7);
        return GetOrCreateCoreAsync(
            key, ct => factory(arg1, arg2, arg3, arg4, arg5, arg6, arg7, ct), policy, cancellationToken);
    }

    /// <inheritdoc />
    public Task<TResult> GetAsync<TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TArg8, TResult>(
        TArg1 arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4, TArg5 arg5, TArg6 arg6, TArg7 arg7, TArg8 arg8,
        Func<TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TArg8, CancellationToken, Task<TResult>> factory,
        CachePolicy policy,
        CancellationToken cancellationToken = default,
        [CallerMemberName] string callerMemberName = "")
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(policy);

        var key = BuildKey(callerMemberName, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8);
        return GetOrCreateCoreAsync(
            key, ct => factory(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, ct), policy, cancellationToken);
    }

    /// <summary>
    /// Core implementation that handles cache lookup, thundering-herd synchronization,
    /// and entry creation with the configured policy.
    /// </summary>
    private async Task<TResult> GetOrCreateCoreAsync<TResult>(
        string key,
        Func<CancellationToken, Task<TResult>> factory,
        CachePolicy policy,
        CancellationToken cancellationToken)
    {
        // Fast path: check cache without acquiring the lock
        if (_cache.TryGetValue(key, out TResult? cached))
        {
            return cached!;
        }

        // Slow path: acquire per-key lock to prevent thundering herd
        var keyLock = _locks.GetOrAdd(key, static _ => new SemaphoreSlim(1, 1));

        await keyLock.WaitAsync(cancellationToken);
        try
        {
            // Double-check after acquiring lock
            if (_cache.TryGetValue(key, out cached))
            {
                return cached!;
            }

            var result = await factory(cancellationToken);

            var options = new MemoryCacheEntryOptions();

            if (policy.AbsoluteExpiration.HasValue)
            {
                options.AbsoluteExpirationRelativeToNow = policy.AbsoluteExpiration.Value;
            }

            if (policy.SlidingExpiration.HasValue)
            {
                options.SlidingExpiration = policy.SlidingExpiration.Value;
            }

            _cache.Set(key, result, options);

            return result;
        }
        finally
        {
            keyLock.Release();
        }
    }

    // Key building uses HashCode.Combine for fast, allocation-light keys.
    // The hash is stringified (13 chars max for an int) and prefixed with the member name.

    internal static string BuildKey<TArg>(string prefix, TArg arg)
    {
        var hash = HashCode.Combine(arg);
        return string.Create(null, stackalloc char[128], $"{prefix}|{hash}");
    }

    internal static string BuildKey<TArg1, TArg2>(string prefix, TArg1 arg1, TArg2 arg2)
    {
        var hash = HashCode.Combine(arg1, arg2);
        return string.Create(null, stackalloc char[128], $"{prefix}|{hash}");
    }

    internal static string BuildKey<TArg1, TArg2, TArg3>(
        string prefix, TArg1 arg1, TArg2 arg2, TArg3 arg3)
    {
        var hash = HashCode.Combine(arg1, arg2, arg3);
        return string.Create(null, stackalloc char[128], $"{prefix}|{hash}");
    }

    internal static string BuildKey<TArg1, TArg2, TArg3, TArg4>(
        string prefix, TArg1 arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4)
    {
        var hash = HashCode.Combine(arg1, arg2, arg3, arg4);
        return string.Create(null, stackalloc char[128], $"{prefix}|{hash}");
    }

    internal static string BuildKey<TArg1, TArg2, TArg3, TArg4, TArg5>(
        string prefix, TArg1 arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4, TArg5 arg5)
    {
        var hash = HashCode.Combine(arg1, arg2, arg3, arg4, arg5);
        return string.Create(null, stackalloc char[128], $"{prefix}|{hash}");
    }

    internal static string BuildKey<TArg1, TArg2, TArg3, TArg4, TArg5, TArg6>(
        string prefix, TArg1 arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4, TArg5 arg5, TArg6 arg6)
    {
        var hash = HashCode.Combine(arg1, arg2, arg3, arg4, arg5, arg6);
        return string.Create(null, stackalloc char[128], $"{prefix}|{hash}");
    }

    internal static string BuildKey<TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7>(
        string prefix, TArg1 arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4, TArg5 arg5, TArg6 arg6,
        TArg7 arg7)
    {
        var hash = HashCode.Combine(arg1, arg2, arg3, arg4, arg5, arg6, arg7);
        return string.Create(null, stackalloc char[128], $"{prefix}|{hash}");
    }

    internal static string BuildKey<TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TArg8>(
        string prefix, TArg1 arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4, TArg5 arg5, TArg6 arg6,
        TArg7 arg7, TArg8 arg8)
    {
        var hash = HashCode.Combine(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8);
        return string.Create(null, stackalloc char[128], $"{prefix}|{hash}");
    }
}

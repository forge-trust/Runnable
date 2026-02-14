using System.Runtime.CompilerServices;

namespace ForgeTrust.Runnable.Caching;

/// <summary>
/// Provides memoized, cache-backed access to expensive computations with automatic
/// key generation and thundering-herd protection.
/// </summary>
public interface IMemo
{
    /// <summary>
    /// Returns a cached result or invokes <paramref name="factory"/> to compute and cache it.
    /// The cache key is automatically derived from the calling member name.
    /// </summary>
    /// <typeparam name="TResult">The type of the cached value.</typeparam>
    /// <param name="factory">The async factory to compute the value on a cache miss.</param>
    /// <param name="policy">The cache expiration policy.</param>
    /// <param name="cancellationToken">A token to cancel the factory invocation.</param>
    /// <param name="callerMemberName">Compiler-injected caller member name. Do not supply manually.</param>
    /// <returns>The cached or freshly computed value.</returns>
    Task<TResult> GetAsync<TResult>(
        Func<CancellationToken, Task<TResult>> factory,
        CachePolicy policy,
        CancellationToken cancellationToken = default,
        [CallerMemberName] string callerMemberName = "");

    /// <summary>
    /// Returns a cached result or invokes <paramref name="factory"/> to compute and cache it.
    /// The cache key is automatically derived from the calling member name and <paramref name="arg"/>.
    /// </summary>
    /// <typeparam name="TArg">The type of the argument.</typeparam>
    /// <typeparam name="TResult">The type of the cached value.</typeparam>
    /// <param name="arg">The argument passed to the factory and incorporated into the cache key.</param>
    /// <param name="factory">The async factory to compute the value on a cache miss.</param>
    /// <param name="policy">The cache expiration policy.</param>
    /// <param name="cancellationToken">A token to cancel the factory invocation.</param>
    /// <param name="callerMemberName">Compiler-injected caller member name. Do not supply manually.</param>
    /// <returns>The cached or freshly computed value.</returns>
    Task<TResult> GetAsync<TArg, TResult>(
        TArg arg,
        Func<TArg, CancellationToken, Task<TResult>> factory,
        CachePolicy policy,
        CancellationToken cancellationToken = default,
        [CallerMemberName] string callerMemberName = "");

    /// <summary>
    /// Returns a cached result or invokes <paramref name="factory"/> to compute and cache it.
    /// The cache key is automatically derived from the calling member name and the provided arguments.
    /// </summary>
    Task<TResult> GetAsync<TArg1, TArg2, TResult>(
        TArg1 arg1, TArg2 arg2,
        Func<TArg1, TArg2, CancellationToken, Task<TResult>> factory,
        CachePolicy policy,
        CancellationToken cancellationToken = default,
        [CallerMemberName] string callerMemberName = "");

    /// <summary>
    /// Returns a cached result or invokes <paramref name="factory"/> to compute and cache it.
    /// The cache key is automatically derived from the calling member name and the provided arguments.
    /// </summary>
    Task<TResult> GetAsync<TArg1, TArg2, TArg3, TResult>(
        TArg1 arg1, TArg2 arg2, TArg3 arg3,
        Func<TArg1, TArg2, TArg3, CancellationToken, Task<TResult>> factory,
        CachePolicy policy,
        CancellationToken cancellationToken = default,
        [CallerMemberName] string callerMemberName = "");

    /// <summary>
    /// Returns a cached result or invokes <paramref name="factory"/> to compute and cache it.
    /// The cache key is automatically derived from the calling member name and the provided arguments.
    /// </summary>
    Task<TResult> GetAsync<TArg1, TArg2, TArg3, TArg4, TResult>(
        TArg1 arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4,
        Func<TArg1, TArg2, TArg3, TArg4, CancellationToken, Task<TResult>> factory,
        CachePolicy policy,
        CancellationToken cancellationToken = default,
        [CallerMemberName] string callerMemberName = "");

    /// <summary>
    /// Returns a cached result or invokes <paramref name="factory"/> to compute and cache it.
    /// The cache key is automatically derived from the calling member name and the provided arguments.
    /// </summary>
    Task<TResult> GetAsync<TArg1, TArg2, TArg3, TArg4, TArg5, TResult>(
        TArg1 arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4, TArg5 arg5,
        Func<TArg1, TArg2, TArg3, TArg4, TArg5, CancellationToken, Task<TResult>> factory,
        CachePolicy policy,
        CancellationToken cancellationToken = default,
        [CallerMemberName] string callerMemberName = "");

    /// <summary>
    /// Returns a cached result or invokes <paramref name="factory"/> to compute and cache it.
    /// The cache key is automatically derived from the calling member name and the provided arguments.
    /// </summary>
    Task<TResult> GetAsync<TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TResult>(
        TArg1 arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4, TArg5 arg5, TArg6 arg6,
        Func<TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, CancellationToken, Task<TResult>> factory,
        CachePolicy policy,
        CancellationToken cancellationToken = default,
        [CallerMemberName] string callerMemberName = "");

    /// <summary>
    /// Returns a cached result or invokes <paramref name="factory"/> to compute and cache it.
    /// The cache key is automatically derived from the calling member name and the provided arguments.
    /// </summary>
    Task<TResult> GetAsync<TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TResult>(
        TArg1 arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4, TArg5 arg5, TArg6 arg6, TArg7 arg7,
        Func<TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, CancellationToken, Task<TResult>> factory,
        CachePolicy policy,
        CancellationToken cancellationToken = default,
        [CallerMemberName] string callerMemberName = "");

    /// <summary>
    /// Returns a cached result or invokes <paramref name="factory"/> to compute and cache it.
    /// The cache key is automatically derived from the calling member name and the provided arguments.
    /// </summary>
    Task<TResult> GetAsync<TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TArg8, TResult>(
        TArg1 arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4, TArg5 arg5, TArg6 arg6, TArg7 arg7, TArg8 arg8,
        Func<TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TArg8, CancellationToken, Task<TResult>> factory,
        CachePolicy policy,
        CancellationToken cancellationToken = default,
        [CallerMemberName] string callerMemberName = "");
}

using System.Collections.Concurrent;
using System.Threading.Channels;

namespace ForgeTrust.Runnable.Core.Extensions;

public static class EnumerableExtensions
{
    /// <summary>
    /// Projects each element of a sequence into a new form with bounded concurrency and preserves the input order of results.
    /// </summary>
    /// <param name="source">The sequence of input items.</param>
    /// <param name="body">An asynchronous transform that receives an input item and a cancellation token and produces a result.</param>
    /// <param name="maxDegreeOfParallelism">The maximum number of concurrent invocations of <paramref name="body"/>; must be greater than zero.</param>
    /// <param name="cancellationToken">A token to observe while waiting for tasks to complete.</param>
    /// <returns>An <see cref="IEnumerable{T}"/> of results in the same order as the input sequence.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> or <paramref name="body"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maxDegreeOfParallelism"/> is less than or equal to zero.</exception>
    /// <exception cref="OperationCanceledException">The operation was canceled via <paramref name="cancellationToken"/>.</exception>
    /// <remarks>Exceptions thrown by the <paramref name="body"/> function propagate to the returned task.</remarks>
    public static async Task<IEnumerable<TResult>> ParallelSelectAsync<TSource, TResult>(
        this IEnumerable<TSource> source,
        Func<TSource, CancellationToken, Task<TResult>> body,
        int maxDegreeOfParallelism,
        CancellationToken cancellationToken = default)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (body == null) throw new ArgumentNullException(nameof(body));
        if (maxDegreeOfParallelism <= 0) throw new ArgumentOutOfRangeException(nameof(maxDegreeOfParallelism));

        var results = new ConcurrentDictionary<long, TResult>();
        using var semaphore = new SemaphoreSlim(maxDegreeOfParallelism);
        var tasks = new List<Task>();
        long index = 0;

        try
        {
            foreach (var item in source)
            {
                var capturedIndex = index++;
                await semaphore.WaitAsync(cancellationToken);

                tasks.Add(
                    Task.Run(
                        async () =>
                        {
                            try
                            {
                                var result = await body(item, cancellationToken);
                                results.TryAdd(capturedIndex, result);
                            }
                            finally
                            {
                                try
                                {
                                    // ReSharper disable AccessToDisposedClosure
                                    semaphore.Release();
                                    // ReSharper restore AccessToDisposedClosure
                                }
                                catch (ObjectDisposedException)
                                {
                                    // Intentionally ignored: the operation was cancelled and the semaphore was disposed
                                }
                            }
                        },
                        cancellationToken));
            }

            await Task.WhenAll(tasks);
        }
        catch (Exception)
        {
            // JOIN ALL ACTIVE TASKS before disposing the semaphore
            // This ensures no background work is left unobserved and avoids NREs/ODEs
            try
            {
                await Task.WhenAll(tasks);
            }
            catch
            {
                // Exceptions in individual tasks are already captured by the main Task.WhenAll
            }

            throw;
        }

        return results.OrderBy(kvp => kvp.Key).Select(kvp => kvp.Value);
    }

    /// <summary>
    /// Produces an async sequence that yields each input element transformed by <paramref name="body"/> in the same order as the source, with bounded concurrency.
    /// </summary>
    /// <param name="source">The input sequence to transform.</param>
    /// <param name="body">A transform function that produces a result for an element.</param>
    /// <param name="maxDegreeOfParallelism">The maximum number of concurrent transform operations; must be greater than zero.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The sequence of transformed elements in the original input order; concurrency is limited to <paramref name="maxDegreeOfParallelism"/>.</returns>
    public static IAsyncEnumerable<TResult> ParallelSelectAsync<TSource, TResult>(
        this IEnumerable<TSource> source,
        Func<TSource, Task<TResult>> body,
        int maxDegreeOfParallelism,
        CancellationToken cancellationToken = default)
    {
        return source.ParallelSelectAsyncEnumerable(
            (item, _) => body(item),
            maxDegreeOfParallelism,
            bufferMultiplier: 4,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Asynchronously projects each element of the <paramref name="source"/> sequence with a bounded degree of concurrency using the specified <paramref name="body"/>, <paramref name="maxDegreeOfParallelism"/>, <paramref name="bufferMultiplier"/>, and <paramref name="cancellationToken"/>, yielding transformed results in the original source order.
    /// </summary>
    /// <param name="source">The input sequence to project.</param>
    /// <param name="body">An asynchronous transform that receives an element and a <see cref="CancellationToken"/> and produces a result.</param>
    /// <param name="maxDegreeOfParallelism">Maximum number of concurrent transform operations; must be greater than zero.</param>
    /// <param name="bufferMultiplier">Multiplier used to size the internal channel buffer; the channel capacity is <c>maxDegreeOfParallelism * bufferMultiplier</c>. Must be at least 1.</param>
    /// <param name="cancellationToken">Token to observe for cancellation of the overall enumeration.</param>
    /// <returns>An async enumerable that yields transformed results in the same order as the source.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="source"/> or <paramref name="body"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="maxDegreeOfParallelism"/> is less than or equal to zero or if <paramref name="bufferMultiplier"/> is less than 1.</exception>
    public static async IAsyncEnumerable<TResult> ParallelSelectAsyncEnumerable<TSource, TResult>(
        this IEnumerable<TSource> source,
        Func<TSource, CancellationToken, Task<TResult>> body,
        int maxDegreeOfParallelism,
        int bufferMultiplier = 4,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (body == null)
        {
            throw new ArgumentNullException(nameof(body));
        }

        if (maxDegreeOfParallelism <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDegreeOfParallelism));
        }

        if (bufferMultiplier < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(bufferMultiplier));
        }

        // Calculate capacity using long to prevent integer overflow
        long capacity = (long)maxDegreeOfParallelism * bufferMultiplier;
        if (capacity > int.MaxValue)
        {
            capacity = int.MaxValue;
        }

        var channel = Channel.CreateBounded<Task<TResult>>(
            new BoundedChannelOptions((int)capacity)
            {
                SingleWriter = true,
                SingleReader = true
            });

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var semaphore = new SemaphoreSlim(maxDegreeOfParallelism);
        var activeTasks = new ConcurrentBag<Task>();

        // Producer: Schedules tasks and writes them to the channel
        // We use Task.Run to offload the scheduling loop so it doesn't block the consumer
        var producerTask = Task.Run(
            async () =>
            {
                try
                {
                    foreach (var item in source)
                    {
                        // ReSharper disable AccessToDisposedClosure
                        if (cts.Token.IsCancellationRequested)
                        {
                            break;
                        }

                        await semaphore.WaitAsync(cts.Token);
                        // ReSharper restore AccessToDisposedClosure

                        // Start the task. 
                        // We wrap matching the semaphore release to the task completion.
                        var task = Task.Run(
                            async () =>
                            {
                                try
                                {
                                    // ReSharper disable AccessToDisposedClosure
                                    return await body(item, cts.Token);
                                    // ReSharper restore AccessToDisposedClosure
                                }
                                finally
                                {
                                    // Release concurrency limit slot when task completes
                                    try
                                    {
                                        // ReSharper disable AccessToDisposedClosure
                                        semaphore.Release();
                                        // ReSharper restore AccessToDisposedClosure
                                    }
                                    catch (ObjectDisposedException)
                                    {
                                        // Intentionally ignored: the operation was cancelled and the semaphore was disposed
                                    }
                                }
                            },
                            // ReSharper disable AccessToDisposedClosure
                            cts.Token);
                        // ReSharper restore AccessToDisposedClosure

                        activeTasks.Add(task);

                        // Write the task (future result) to the channel
                        // If the consumer is slow, this will block once the channel is full,
                        // providing backpressure but ensuring we have maxDegreeOfParallelism active tasks.
                        // ReSharper disable AccessToDisposedClosure
                        await channel.Writer.WriteAsync(task, cts.Token);
                        // ReSharper restore AccessToDisposedClosure
                    }

                    channel.Writer.Complete();
                }
                catch (Exception ex)
                {
                    channel.Writer.Complete(ex);
                }
            },
            cancellationToken);

        try
        {
            // Consumer: Yields results in order
            await foreach (var task in channel.Reader.ReadAllAsync(cancellationToken))
            {
                yield return await task;
            }
        }
        finally
        {
            // Signal producer to stop if it's still running (e.g. on break or exception)
            await cts.CancelAsync();

            try
            {
                // Ensure producer task is joined before semaphore is disposed
                await producerTask;
            }
            catch (OperationCanceledException)
            {
                // Expected on cancellation
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine(
                    $"Error during ParallelSelectAsyncEnumerable cleanup (producer): {ex}");
#endif
            }

            // JOIN ALL ACTIVE TASKS before disposing the semaphore and CTS
            // This is critical because they capture the semaphore and CTS in their closures.
            try
            {
                await Task.WhenAll(activeTasks);
            }
            catch (Exception)
            {
                // Exceptions in individual tasks are already handled/re-thrown via the channel/yield return
            }
        }
    }

    /// <summary>
    /// Produces an async sequence of transformed elements with bounded concurrency while preserving the input order.
    /// </summary>
    /// <param name="source">The input sequence to transform.</param>
    /// <param name="body">An asynchronous transform for each element. The transform's cancellation token is ignored by this overload.</param>
    /// <param name="maxDegreeOfParallelism">The maximum number of concurrent transform operations; must be greater than zero.</param>
    /// <param name="bufferMultiplier">Multiplier used to compute the internal bounded channel capacity as <c>maxDegreeOfParallelism * bufferMultiplier</c>; must be at least 1.</param>
    /// <param name="cancellationToken">Token to observe for cancellation of the overall operation.</param>
    /// <returns>An <see cref="IAsyncEnumerable{TResult}"/> that yields transformed elements in the same order as <paramref name="source"/>.</returns>
    public static IAsyncEnumerable<TResult> ParallelSelectAsyncEnumerable<TSource, TResult>(
        this IEnumerable<TSource> source,
        Func<TSource, Task<TResult>> body,
        int maxDegreeOfParallelism,
        int bufferMultiplier = 4,
        CancellationToken cancellationToken = default)
    {
        return source.ParallelSelectAsyncEnumerable(
            (item, _) => body(item),
            maxDegreeOfParallelism,
            bufferMultiplier,
            cancellationToken);
    }
}

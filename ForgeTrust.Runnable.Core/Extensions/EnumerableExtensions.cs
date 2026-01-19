using System.Collections.Concurrent;
using System.Threading.Channels;

namespace ForgeTrust.Runnable.Core.Extensions;

public static class EnumerableExtensions
{
    /// <summary>
    /// Projects each element of a sequence into a new form asynchronously, with a limit on the number of concurrent operations.
    /// </summary>
    /// <typeparam name="TSource">The type of the elements of source.</typeparam>
    /// <typeparam name="TResult">The type of the value returned by the selector.</typeparam>
    /// <param name="source">A sequence of values to invoke a transform function on.</param>
    /// <param name="body">A transform function to apply to each element.</param>
    /// <param name="maxDegreeOfParallelism">The maximum number of concurrent tasks.</param>
    /// <param name="cancellationToken">The CancellationToken to monitor for cancellation requests.</param>
    /// <returns>A task that represents the complete operation. The task result contains an IEnumerable of type TResult.</returns>
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
                                semaphore.Release();
                            }
                            catch (ObjectDisposedException)
                            {
                            }
                        }
                    },
                    cancellationToken));
        }

        await Task.WhenAll(tasks);

        return results.OrderBy(kvp => kvp.Key).Select(kvp => kvp.Value);
    }

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
    /// Projects each element of a sequence into a new form asynchronously, with a limit on the number of concurrent operations,
    /// yielding results in the original order as they become available.
    /// </summary>
    /// <typeparam name="TSource">The type of the elements of source.</typeparam>
    /// <typeparam name="TResult">The type of the value returned by the selector.</typeparam>
    /// <param name="source">A sequence of values to invoke a transform function on.</param>
    /// <param name="body">A transform function to apply to each element.</param>
    /// <param name="maxDegreeOfParallelism">The maximum number of concurrent tasks.</param>
    /// <param name="bufferMultiplier">Multiplier for the channel buffer size (default 4). Allows producer to run ahead of consumer.</param>
    /// <param name="cancellationToken">The CancellationToken to monitor for cancellation requests.</param>
    /// <returns>An async enumerable that yields results in order.</returns>
    public static async IAsyncEnumerable<TResult> ParallelSelectAsyncEnumerable<TSource, TResult>(
        this IEnumerable<TSource> source,
        Func<TSource, CancellationToken, Task<TResult>> body,
        int maxDegreeOfParallelism,
        int bufferMultiplier = 4,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (body == null) throw new ArgumentNullException(nameof(body));
        if (maxDegreeOfParallelism <= 0) throw new ArgumentOutOfRangeException(nameof(maxDegreeOfParallelism));
        if (bufferMultiplier < 1) throw new ArgumentOutOfRangeException(nameof(bufferMultiplier));

        var channel = Channel.CreateBounded<Task<TResult>>(
            new BoundedChannelOptions(maxDegreeOfParallelism * bufferMultiplier)
            {
                SingleWriter = true,
                SingleReader = true
            });

        using var semaphore = new SemaphoreSlim(maxDegreeOfParallelism);

        // Producer: Schedules tasks and writes them to the channel
        // We use Task.Run to offload the scheduling loop so it doesn't block the consumer
        _ = Task.Run(
            async () =>
            {
                try
                {
                    foreach (var item in source)
                    {
                        // ReSharper disable once AccessToDisposedClosure
                        await semaphore.WaitAsync(cancellationToken);

                        // Start the task. 
                        // We wrap matching the semaphore release to the task completion.
                        var task = Task.Run(
                            async () =>
                            {
                                try
                                {
                                    return await body(item, cancellationToken);
                                }
                                finally
                                {
                                    // Release concurrency limit slot when task completes
                                    // ReSharper disable once AccessToDisposedClosure
                                    semaphore.Release();
                                }
                            },
                            cancellationToken);

                        // Write the task (future result) to the channel
                        // If the consumer is slow, this will block once the channel is full,
                        // providing backpressure but ensuring we have maxDegreeOfParallelism active tasks.
                        await channel.Writer.WriteAsync(task, cancellationToken);
                    }

                    channel.Writer.Complete();
                }
                catch (Exception ex)
                {
                    channel.Writer.Complete(ex);
                }
            },
            cancellationToken);

        // Consumer: Yields results in order
        await foreach (var task in channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return await task;
        }
    }

    /// <summary>
    /// Projects each element of a sequence into a new form asynchronously, with a limit on the number of concurrent operations,
    /// yielding results in the original order as they become available.
    /// </summary>
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



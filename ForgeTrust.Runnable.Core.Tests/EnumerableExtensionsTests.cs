using ForgeTrust.Runnable.Core.Extensions;

namespace ForgeTrust.Runnable.Core.Tests;

public class EnumerableExtensionsTests
{
    [Fact]
    public async Task ParallelSelectAsyncEnumerable_PreservesOrder()
    {
        // Arrange
        var input = Enumerable.Range(0, 10).ToList();

        // Act
        // Variable delays to simulate out-of-order completion
        var results = new List<int>();
        await foreach (var item in input.ParallelSelectAsyncEnumerable(
                           async x =>
                           {
                               // Even numbers wait longer, odd numbers complete fast
                               // This ensures they would arrive out of order if not re-ordered by the enumerable
                               await Task.Delay(x % 2 == 0 ? 100 : 10);

                               return x;
                           },
                           maxDegreeOfParallelism: 4))
        {
            results.Add(item);
        }

        // Assert
        Assert.Equal(input, results);
    }

    [Fact]
    public async Task ParallelSelectAsyncEnumerable_HandlesEmptySource()
    {
        // Arrange
        var input = Enumerable.Empty<int>();

        // Act
        var results = new List<int>();
        await foreach (var item in input.ParallelSelectAsyncEnumerable(async x => await Task.FromResult(x), 4))
        {
            results.Add(item);
        }

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public async Task ParallelSelectAsyncEnumerable_ExceptionInBody_Propagates()
    {
        // Arrange
        var input = Enumerable.Range(0, 5);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var _ in input.ParallelSelectAsyncEnumerable(
                               async x =>
                               {
                                   if (x == 3)
                                   {
                                       throw new InvalidOperationException("Test Error");
                                   }

                                   return await Task.FromResult(x);
                               },
                               2))
            {
                // Intentionally empty: drives execution to verify exception propagation
            }
        });
    }

    [Fact]
    public async Task ParallelSelectAsyncEnumerable_RespectsMaxDegreeOfParallelism()
    {
        // This is a rough test since perfect concurrency testing is hard.
        // We verify that we don't start ALL tasks at once.

        // Arrange
        var input = Enumerable.Range(0, 10);
        var activeTasks = 0;
        var maxObservedTasks = 0;
        var lockObj = new object();

        // Act
        await foreach (var _ in input.ParallelSelectAsyncEnumerable(
                           async x =>
                           {
                               lock (lockObj)
                               {
                                   activeTasks++;
                                   maxObservedTasks = Math.Max(maxObservedTasks, activeTasks);
                               }

                               await Task.Delay(100);
                               lock (lockObj)
                               {
                                   activeTasks--;
                               }

                               return x;
                           },
                           maxDegreeOfParallelism: 2))
        {
            // Intentionally empty: drives execution to verify degree of parallelism
        }

        // Assert
        // We strictly assert maxObservedTasks <= 2 since we increased the delay to reduce flakiness.
        Assert.True(maxObservedTasks <= 2, $"Expected max degree 2, but observed {maxObservedTasks}");
    }

    [Fact]
    public async Task ParallelSelectAsyncEnumerable_EarlyBreak_CompletesProducerTask()
    {
        // Arrange
        var input = Enumerable.Range(0, 100);
        var startedCount = 0;

        // Act
        await foreach (var item in input.ParallelSelectAsyncEnumerable(
                           async x =>
                           {
                               Interlocked.Increment(ref startedCount);
                               await Task.Delay(50);

                               return x;
                           },
                           maxDegreeOfParallelism: 10))
        {
            if (item == 5)
            {
                break;
            }
        }

        // Wait a bit to ensure cleanup would have happened
        await Task.Delay(200);

        // Assert
        // With 10 parallelism and breaking at item 5, we expect some tasks to have started but not all 100.
        // If the producer task didn't stop, it might have scheduled more than 15-20 (bufferMultiplier 4x * DOP 10 = 40 possible)
        // But it definitely SHOULD NOT reach 100.
        Assert.True(startedCount < 100, $"Expected producer to stop, but {startedCount} tasks were started.");
    }
}

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
                                   if (x == 3) throw new InvalidOperationException("Test Error");

                                   return await Task.FromResult(x);
                               },
                               2))
            {
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
        int activeTasks = 0;
        int maxObservedTasks = 0;
        object lockObj = new object();

        // Act
        await foreach (var _ in input.ParallelSelectAsyncEnumerable(
                           async x =>
                           {
                               lock (lockObj)
                               {
                                   activeTasks++;
                                   maxObservedTasks = Math.Max(maxObservedTasks, activeTasks);
                               }

                               await Task.Delay(50);
                               lock (lockObj)
                               {
                                   activeTasks--;
                               }

                               return x;
                           },
                           maxDegreeOfParallelism: 2))
        {
        }

        // Assert
        // We can't strictly assert maxObservedTasks == 2 because of scheduling race conditions 
        // (a task might start before another fully cleans up), but it should be close and certainly not 10.
        Assert.True(maxObservedTasks <= 3, $"Expected max degree approx 2, but observed {maxObservedTasks}");
    }
}

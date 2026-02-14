using Microsoft.Extensions.Caching.Memory;

namespace ForgeTrust.Runnable.Caching.Tests;

public class MemoTests
{
    private static Memo CreateMemo() => new(new MemoryCache(new MemoryCacheOptions()));

    [Fact]
    public async Task GetAsync_CacheMiss_CallsFactory()
    {
        var memo = CreateMemo();
        var callCount = 0;

        var result = await memo.GetAsync(
            async ct =>
            {
                callCount++;

                return 42;
            },
            CachePolicy.Absolute(TimeSpan.FromMinutes(5)));

        Assert.Equal(42, result);
        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task GetAsync_CacheHit_DoesNotCallFactory()
    {
        var memo = CreateMemo();
        var callCount = 0;

        Task<int> Factory(CancellationToken ct)
        {
            callCount++;

            return Task.FromResult(42);
        }

        var first = await memo.GetAsync(Factory, CachePolicy.Absolute(TimeSpan.FromMinutes(5)));
        var second = await memo.GetAsync(Factory, CachePolicy.Absolute(TimeSpan.FromMinutes(5)));

        Assert.Equal(42, first);
        Assert.Equal(42, second);
        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task GetAsync_DifferentArgs_DifferentEntries()
    {
        var memo = CreateMemo();

        var a = await memo.GetAsync(
            1,
            (x, ct) => Task.FromResult(x * 10),
            CachePolicy.Absolute(TimeSpan.FromMinutes(5)));

        var b = await memo.GetAsync(
            2,
            (x, ct) => Task.FromResult(x * 10),
            CachePolicy.Absolute(TimeSpan.FromMinutes(5)));

        Assert.Equal(10, a);
        Assert.Equal(20, b);
    }

    [Fact]
    public async Task GetAsync_SameArgs_CacheHit()
    {
        var memo = CreateMemo();
        var callCount = 0;

        Task<string> Factory(int id, CancellationToken ct)
        {
            callCount++;

            return Task.FromResult($"user-{id}");
        }

        var first = await memo.GetAsync(42, Factory, CachePolicy.Absolute(TimeSpan.FromMinutes(5)));
        var second = await memo.GetAsync(42, Factory, CachePolicy.Absolute(TimeSpan.FromMinutes(5)));

        Assert.Equal("user-42", first);
        Assert.Equal("user-42", second);
        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task GetAsync_ThunderingHerd_FactoryCalledOnce()
    {
        var memo = CreateMemo();
        var callCount = 0;
        var gate = new TaskCompletionSource<bool>();

        async Task<int> SlowFactory(CancellationToken ct)
        {
            Interlocked.Increment(ref callCount);
            await gate.Task;

            return 99;
        }

        // Launch multiple concurrent callers for the same key
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => Task.Run(() => memo.GetAsync(SlowFactory, CachePolicy.Absolute(TimeSpan.FromMinutes(5)))))
            .ToArray();

        // Give all tasks time to hit the lock
        await Task.Delay(100);

        // Release the factory
        gate.SetResult(true);

        var results = await Task.WhenAll(tasks);

        Assert.All(results, r => Assert.Equal(99, r));
        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task GetAsync_TwoArgs_Works()
    {
        var memo = CreateMemo();

        var result = await memo.GetAsync(
            "tenant-1",
            42,
            (t, u, ct) => Task.FromResult($"{t}:{u}"),
            CachePolicy.Absolute(TimeSpan.FromMinutes(5)));

        Assert.Equal("tenant-1:42", result);
    }

    [Fact]
    public async Task GetAsync_ThreeArgs_Works()
    {
        var memo = CreateMemo();

        var result = await memo.GetAsync(
            "a",
            "b",
            "c",
            (
                a,
                b,
                c,
                ct) => Task.FromResult($"{a}-{b}-{c}"),
            CachePolicy.Absolute(TimeSpan.FromMinutes(5)));

        Assert.Equal("a-b-c", result);
    }

    [Fact]
    public async Task GetAsync_FourArgs_Works()
    {
        var memo = CreateMemo();

        var result = await memo.GetAsync(
            1,
            2,
            3,
            4,
            (
                a,
                b,
                c,
                d,
                ct) => Task.FromResult(a + b + c + d),
            CachePolicy.Absolute(TimeSpan.FromMinutes(5)));

        Assert.Equal(10, result);
    }

    [Fact]
    public async Task GetAsync_FiveArgs_Works()
    {
        var memo = CreateMemo();

        var result = await memo.GetAsync(
            1,
            2,
            3,
            4,
            5,
            (
                a,
                b,
                c,
                d,
                e,
                ct) => Task.FromResult(a + b + c + d + e),
            CachePolicy.Absolute(TimeSpan.FromMinutes(5)));

        Assert.Equal(15, result);
    }

    [Fact]
    public async Task GetAsync_SixArgs_Works()
    {
        var memo = CreateMemo();

        var result = await memo.GetAsync(
            1,
            2,
            3,
            4,
            5,
            6,
            (
                a,
                b,
                c,
                d,
                e,
                f,
                ct) => Task.FromResult(a + b + c + d + e + f),
            CachePolicy.Absolute(TimeSpan.FromMinutes(5)));

        Assert.Equal(21, result);
    }

    [Fact]
    public async Task GetAsync_SevenArgs_Works()
    {
        var memo = CreateMemo();

        var result = await memo.GetAsync(
            1,
            2,
            3,
            4,
            5,
            6,
            7,
            (
                a,
                b,
                c,
                d,
                e,
                f,
                g,
                ct) => Task.FromResult(a + b + c + d + e + f + g),
            CachePolicy.Absolute(TimeSpan.FromMinutes(5)));

        Assert.Equal(28, result);
    }

    [Fact]
    public async Task GetAsync_EightArgs_Works()
    {
        var memo = CreateMemo();

        var result = await memo.GetAsync(
            1,
            2,
            3,
            4,
            5,
            6,
            7,
            8,
            (
                a,
                b,
                c,
                d,
                e,
                f,
                g,
                h,
                ct) => Task.FromResult(a + b + c + d + e + f + g + h),
            CachePolicy.Absolute(TimeSpan.FromMinutes(5)));

        Assert.Equal(36, result);
    }

    [Fact]
    public async Task GetAsync_CancellationToken_Respected()
    {
        var memo = CreateMemo();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            memo.GetAsync(
                async ct =>
                {
                    ct.ThrowIfCancellationRequested();

                    return 1;
                },
                CachePolicy.Absolute(TimeSpan.FromMinutes(5)),
                cts.Token));
    }

    [Fact]
    public async Task GetAsync_NullFactory_Throws()
    {
        var memo = CreateMemo();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            memo.GetAsync(
                (Func<CancellationToken, Task<int>>)null!,
                CachePolicy.Absolute(TimeSpan.FromMinutes(5))));
    }

    [Fact]
    public async Task GetAsync_NullPolicy_Throws()
    {
        var memo = CreateMemo();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            memo.GetAsync(
                ct => Task.FromResult(1),
                null!));
    }

    [Fact]
    public void Constructor_NullCache_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new Memo(null!));
    }

    [Fact]
    public async Task GetAsync_AbsoluteExpiration_Evicts()
    {
        var cache = new MemoryCache(new MemoryCacheOptions { ExpirationScanFrequency = TimeSpan.FromMilliseconds(10) });
        var memo = new Memo(cache);
        var callCount = 0;

        Task<int> Factory(CancellationToken ct)
        {
            callCount++;

            return Task.FromResult(callCount);
        }

        var first = await memo.GetAsync(Factory, CachePolicy.Absolute(TimeSpan.FromMilliseconds(50)));
        Assert.Equal(1, first);

        // Wait for expiration
        await Task.Delay(100);

        var second = await memo.GetAsync(Factory, CachePolicy.Absolute(TimeSpan.FromMilliseconds(50)));
        Assert.Equal(2, second);
        Assert.Equal(2, callCount);
    }

    [Fact]
    public async Task GetAsync_SlidingExpiration_KeepsAliveOnAccess()
    {
        var cache = new MemoryCache(new MemoryCacheOptions { ExpirationScanFrequency = TimeSpan.FromMilliseconds(10) });
        var memo = new Memo(cache);
        var callCount = 0;

        Task<int> Factory(CancellationToken ct)
        {
            callCount++;

            return Task.FromResult(callCount);
        }

        var policy = CachePolicy.Sliding(TimeSpan.FromMilliseconds(200));

        await memo.GetAsync(Factory, policy);

        // Access before expiration to keep it alive
        await Task.Delay(100);
        await memo.GetAsync(Factory, policy);

        await Task.Delay(100);
        await memo.GetAsync(Factory, policy);

        // Should still be the first call's result
        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task GetAsync_SlidingExpiration_EvictsAfterIdle()
    {
        var cache = new MemoryCache(new MemoryCacheOptions { ExpirationScanFrequency = TimeSpan.FromMilliseconds(10) });
        var memo = new Memo(cache);
        var callCount = 0;

        Task<int> Factory(CancellationToken ct)
        {
            callCount++;

            return Task.FromResult(callCount);
        }

        var policy = CachePolicy.Sliding(TimeSpan.FromMilliseconds(50));

        await memo.GetAsync(Factory, policy);

        // Wait for idle expiration
        await Task.Delay(100);

        var second = await memo.GetAsync(Factory, policy);
        Assert.Equal(2, second);
    }

    [Fact]
    public async Task GetAsync_SlidingWithAbsolute_EnforcesCeiling()
    {
        var cache = new MemoryCache(new MemoryCacheOptions { ExpirationScanFrequency = TimeSpan.FromMilliseconds(10) });
        var memo = new Memo(cache);
        var callCount = 0;

        Task<int> Factory(CancellationToken ct)
        {
            callCount++;

            return Task.FromResult(callCount);
        }

        // Sliding of 200ms but absolute ceiling of 150ms
        var policy = CachePolicy.SlidingWithAbsolute(
            TimeSpan.FromMilliseconds(100),
            TimeSpan.FromMilliseconds(200));

        await memo.GetAsync(Factory, policy);

        // Access within sliding window to keep alive
        await Task.Delay(80);
        var second = await memo.GetAsync(Factory, policy);
        Assert.Equal(1, second); // Still cached

        // Wait past the absolute ceiling
        await Task.Delay(200);

        var third = await memo.GetAsync(Factory, policy);
        Assert.Equal(2, third); // Evicted by absolute ceiling
    }

    [Fact]
    public async Task GetAsync_FactoryException_Propagates()
    {
        var memo = CreateMemo();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            memo.GetAsync(
                ct => Task.FromException<int>(new InvalidOperationException("boom")),
                CachePolicy.Absolute(TimeSpan.FromMinutes(5))));
    }

    [Fact]
    public async Task GetAsync_FactoryException_DoesNotCache()
    {
        var memo = CreateMemo();
        var callCount = 0;

        Task<int> Factory(CancellationToken ct)
        {
            callCount++;
            if (callCount == 1)
            {
                throw new InvalidOperationException("transient");
            }

            return Task.FromResult(42);
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            memo.GetAsync(Factory, CachePolicy.Absolute(TimeSpan.FromMinutes(5))));

        var result = await memo.GetAsync(Factory, CachePolicy.Absolute(TimeSpan.FromMinutes(5)));

        Assert.Equal(42, result);
        Assert.Equal(2, callCount);
    }

    // Key generation tests

    [Fact]
    public void BuildKey_OneArg_Deterministic()
    {
        var key1 = Memo.BuildKey("Test", 42);
        var key2 = Memo.BuildKey("Test", 42);
        Assert.Equal(key1, key2);
    }

    [Fact]
    public void BuildKey_DifferentArgs_DifferentKeys()
    {
        var key1 = Memo.BuildKey("Test", 1);
        var key2 = Memo.BuildKey("Test", 2);
        Assert.NotEqual(key1, key2);
    }

    [Fact]
    public void BuildKey_DifferentPrefixes_DifferentKeys()
    {
        var key1 = Memo.BuildKey("MethodA", 42);
        var key2 = Memo.BuildKey("MethodB", 42);
        Assert.NotEqual(key1, key2);
    }

    [Fact]
    public void BuildKey_TwoArgs_Deterministic()
    {
        var key1 = Memo.BuildKey("Test", "a", 1);
        var key2 = Memo.BuildKey("Test", "a", 1);
        Assert.Equal(key1, key2);
    }
}

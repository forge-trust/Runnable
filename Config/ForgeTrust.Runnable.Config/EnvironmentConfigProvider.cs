using System.Globalization;
using System.Text.Json;
using ForgeTrust.Runnable.Core;

namespace ForgeTrust.Runnable.Config;

/// <summary>
/// A configuration provider that retrieves values from environment variables.
/// </summary>
internal class EnvironmentConfigProvider : IEnvironmentConfigProvider
{
    // Safety limit for indexed env-var collections (KEY__0, KEY__1, ...).
    // Prevents unbounded probing while still supporting large lists.
    private const int MaxIndexedCollectionEntries = 1024;

    private readonly IEnvironmentProvider _environmentProvider;

    // Priority is technically ignored because DefaultConfigManager special-cases this provider
    // to always check it first, effectively giving it overrides-all priority.
    /// <inheritdoc />
    public int Priority { get; } = -1;

    /// <inheritdoc />
    public string Name { get; } = nameof(EnvironmentConfigProvider);

    /// <summary>
    /// Initializes a new instance of the <see cref="EnvironmentConfigProvider"/> class.
    /// </summary>
    /// <param name="environmentProvider">The environment provider.</param>
    public EnvironmentConfigProvider(IEnvironmentProvider environmentProvider)
    {
        _environmentProvider = environmentProvider;
    }

    /// <inheritdoc />
    public T? GetValue<T>(string environment, string key)
    {
        var envPrefix = NormalizeSegment(environment);
        var legacyKey = NormalizeSegment(key);
        var hierarchicalKey = NormalizeHierarchicalKey(key);

        var directCandidates = BuildDirectCandidates(envPrefix, legacyKey, hierarchicalKey);

        foreach (var candidate in directCandidates)
        {
            var value = _environmentProvider.GetEnvironmentVariable(candidate);
            if (value == null)
            {
                continue;
            }

            if (TryConvertStringValue<T>(value, out var parsed))
            {
                return parsed;
            }

            // Prefer the first parseable candidate while still allowing
            // lower-priority key formats as fallback when parsing fails.
        }

        if (TryReadIndexedCollection<T>($"{envPrefix}__{hierarchicalKey}", out var envScopedCollection))
        {
            return envScopedCollection;
        }

        if (TryReadIndexedCollection<T>(hierarchicalKey, out var collection))
        {
            return collection;
        }

        return default;
    }

    /// <inheritdoc />
    public string Environment => _environmentProvider.Environment;

    /// <inheritdoc />
    public bool IsDevelopment => _environmentProvider.IsDevelopment;

    /// <inheritdoc />
    public string? GetEnvironmentVariable(string name, string? defaultValue = null) =>
        _environmentProvider.GetEnvironmentVariable(name, defaultValue);

    /// <summary>
    /// Converts a key/environment segment to uppercase (via <see cref="string.ToUpperInvariant"/>)
    /// and flattens separators by replacing '.' and '-' with a single '_'.
    /// Used for legacy flat environment-variable lookup.
    /// </summary>
    private static string NormalizeSegment(string value) =>
        value.ToUpperInvariant()
            .Replace('.', '_')
            .Replace('-', '_');

    /// <summary>
    /// Converts a key to uppercase (via <see cref="string.ToUpperInvariant"/>), splits on '.' and '-'
    /// as hierarchical delimiters, removes empty segments, and joins segments using "__".
    /// Used for hierarchical environment-variable lookup while preserving path boundaries.
    /// </summary>
    private static string NormalizeHierarchicalKey(string value)
    {
        var segments = value.Split(['.', '-'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Join("__", segments.Select(s => s.ToUpperInvariant()));
    }

    private static IReadOnlyList<string> BuildDirectCandidates(string envPrefix, string legacyKey, string hierarchicalKey)
    {
        var ordered = new[]
        {
            $"{envPrefix}_{legacyKey}",
            legacyKey,
            $"{envPrefix}__{hierarchicalKey}",
            hierarchicalKey
        };

        var distinct = new List<string>(ordered.Length);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var candidate in ordered)
        {
            if (seen.Add(candidate))
            {
                distinct.Add(candidate);
            }
        }

        return distinct;
    }

    private static bool TryConvertStringValue<T>(string value, out T? parsed)
    {
        if (!TryConvertStringToType(value, typeof(T), out var obj))
        {
            parsed = default;
            return false;
        }

        parsed = (T?)obj;
        return true;
    }

    private bool TryReadIndexedCollection<T>(string keyPrefix, out T? parsed)
    {
        parsed = default;
        var targetType = typeof(T);
        var elementType = GetCollectionElementType(targetType);
        if (elementType == null)
        {
            return false;
        }

        var values = new List<string>();
        for (var index = 0; index < MaxIndexedCollectionEntries; index++)
        {
            var value = _environmentProvider.GetEnvironmentVariable($"{keyPrefix}__{index}");
            if (value == null)
            {
                if (index == 0)
                {
                    return false;
                }

                break;
            }

            values.Add(value);
        }

        var typedList = (System.Collections.IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType))!;
        foreach (var value in values)
        {
            if (!TryConvertStringToType(value, elementType, out var element))
            {
                return false;
            }

            typedList.Add(element);
        }

        if (targetType.IsArray)
        {
            var array = Array.CreateInstance(elementType, typedList.Count);
            typedList.CopyTo(array, 0);
            parsed = (T)(object)array;
            return true;
        }

        parsed = (T)typedList;
        return true;
    }

    private static Type? GetCollectionElementType(Type targetType)
    {
        if (targetType.IsArray)
        {
            return targetType.GetElementType();
        }

        if (targetType.IsGenericType)
        {
            var genericDef = targetType.GetGenericTypeDefinition();
            if (genericDef == typeof(List<>)
                || genericDef == typeof(IList<>)
                || genericDef == typeof(IEnumerable<>)
                || genericDef == typeof(IReadOnlyList<>)
                || genericDef == typeof(ICollection<>))
            {
                return targetType.GetGenericArguments()[0];
            }
        }

        return null;
    }

    private static bool TryConvertStringToType(string value, Type targetType, out object? parsed)
    {
        var nullableUnderlying = Nullable.GetUnderlyingType(targetType);
        if (nullableUnderlying != null)
        {
            if (string.IsNullOrEmpty(value))
            {
                parsed = null;
                return true;
            }

            targetType = nullableUnderlying;
        }

        if (targetType == typeof(string))
        {
            parsed = value;
            return true;
        }

        if (targetType.IsEnum)
        {
            if (Enum.TryParse(targetType, value, true, out var enumValue))
            {
                parsed = enumValue;
                return true;
            }

            parsed = null;
            return false;
        }

        if (targetType == typeof(Guid))
        {
            if (Guid.TryParse(value, out var guid))
            {
                parsed = guid;
                return true;
            }

            parsed = null;
            return false;
        }

        if (targetType == typeof(DateTimeOffset))
        {
            if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dto))
            {
                parsed = dto;
                return true;
            }

            parsed = null;
            return false;
        }

        if (targetType == typeof(TimeSpan))
        {
            if (TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out var ts))
            {
                parsed = ts;
                return true;
            }

            parsed = null;
            return false;
        }

        try
        {
            if (IsSimpleType(targetType))
            {
                parsed = Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
                return true;
            }

            parsed = JsonSerializer.Deserialize(value, targetType);
            return parsed != null;
        }
        catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException
                                       or ArgumentException or JsonException or NotSupportedException)
        {
            parsed = null;
            return false;
        }
    }

    private static bool IsSimpleType(Type targetType) =>
        targetType.IsPrimitive
        || targetType == typeof(decimal)
        || targetType == typeof(DateTime);
}

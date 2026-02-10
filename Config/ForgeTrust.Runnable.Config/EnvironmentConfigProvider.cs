using System.Globalization;
using System.Text.Json;
using ForgeTrust.Runnable.Core;

namespace ForgeTrust.Runnable.Config;

/// <summary>
/// A configuration provider that retrieves values from environment variables.
/// </summary>
internal class EnvironmentConfigProvider : IEnvironmentConfigProvider
{
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

        string[] directCandidates =
        [
            $"{envPrefix}_{legacyKey}",
            legacyKey,
            $"{envPrefix}__{hierarchicalKey}",
            hierarchicalKey
        ];

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

            return default;
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

    private static string NormalizeSegment(string value) =>
        value.ToUpperInvariant()
            .Replace('.', '_')
            .Replace('-', '_');

    private static string NormalizeHierarchicalKey(string value)
    {
        var segments = value.Split(['.', '-'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Join("__", segments.Select(s => s.ToUpperInvariant()));
    }

    private static bool TryConvertStringValue<T>(string value, out T? parsed)
    {
        var targetType = typeof(T);
        var nullableUnderlying = Nullable.GetUnderlyingType(targetType);
        if (nullableUnderlying != null)
        {
            if (string.IsNullOrEmpty(value))
            {
                parsed = default;
                return true;
            }

            targetType = nullableUnderlying;
        }

        if (targetType == typeof(string))
        {
            parsed = (T)(object)value;
            return true;
        }

        if (targetType.IsEnum)
        {
            if (Enum.TryParse(targetType, value, true, out var enumValue))
            {
                parsed = (T)enumValue;
                return true;
            }

            parsed = default;
            return false;
        }

        try
        {
            if (IsSimpleType(targetType))
            {
                parsed = (T?)Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
                return true;
            }

            parsed = JsonSerializer.Deserialize<T>(value);
            return parsed != null;
        }
        catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException
                                       or ArgumentException or JsonException)
        {
            parsed = default;
            return false;
        }
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
        for (var index = 0; index < 1024; index++)
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

        if (values.Count == 0)
        {
            return false;
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

        if (targetType.IsAssignableFrom(typedList.GetType()))
        {
            parsed = (T)typedList;
            return true;
        }

        try
        {
            parsed = (T?)Activator.CreateInstance(targetType, typedList);
            return parsed != null;
        }
        catch
        {
            return false;
        }
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
        if (targetType == typeof(string))
        {
            parsed = value;
            return true;
        }

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
                                       or ArgumentException or JsonException)
        {
            parsed = null;
            return false;
        }
    }

    private static bool IsSimpleType(Type targetType) =>
        targetType.IsPrimitive
        || targetType == typeof(decimal)
        || targetType == typeof(Guid)
        || targetType == typeof(DateTime)
        || targetType == typeof(DateTimeOffset)
        || targetType == typeof(TimeSpan);
}

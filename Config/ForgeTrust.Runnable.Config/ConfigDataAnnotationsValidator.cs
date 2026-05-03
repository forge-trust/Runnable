using System.Collections;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Options;

namespace ForgeTrust.Runnable.Config;

internal static class ConfigDataAnnotationsValidator
{
    public static void Validate(
        string key,
        Type configType,
        Type valueType,
        object? value)
    {
        if (value == null || IsScalar(value.GetType()))
        {
            return;
        }

        var failures = new List<ConfigurationValidationFailure>();
        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);

        ValidateNode(
            key,
            configType,
            valueType,
            value,
            path: null,
            visited,
            failures);

        if (failures.Count > 0)
        {
            throw new ConfigurationValidationException(key, configType, valueType, failures);
        }
    }

    private static void ValidateNode(
        string key,
        Type configType,
        Type valueType,
        object value,
        string? path,
        HashSet<object> visited,
        List<ConfigurationValidationFailure> failures)
    {
        if (!TrackVisit(value, visited))
        {
            return;
        }

        var results = new List<ValidationResult>();
        var context = new ValidationContext(value);

        Validator.TryValidateObject(
            value,
            context,
            results,
            validateAllProperties: true);

        foreach (var result in results)
        {
            failures.Add(ToFailure(key, configType, valueType, path, result));
        }

        foreach (var property in GetValidatableProperties(value.GetType()))
        {
            ValidateProperty(key, configType, valueType, value, property, path, visited, failures);
        }

        foreach (var field in GetValidatableFields(value.GetType()))
        {
            ValidateField(key, configType, valueType, value, field, path, visited, failures);
        }
    }

    private static void ValidateProperty(
        string key,
        Type configType,
        Type valueType,
        object parent,
        PropertyInfo property,
        string? parentPath,
        HashSet<object> visited,
        List<ConfigurationValidationFailure> failures)
    {
        var objectMembersAttribute = property.GetCustomAttribute<ValidateObjectMembersAttribute>();
        var enumeratedItemsAttribute = property.GetCustomAttribute<ValidateEnumeratedItemsAttribute>();
        if (objectMembersAttribute == null && enumeratedItemsAttribute == null)
        {
            return;
        }

        var memberPath = CombinePath(parentPath, property.Name);
        var memberValue = property.GetValue(parent);

        ValidateOptInMember(
            key,
            configType,
            valueType,
            memberValue,
            memberPath,
            objectMembersAttribute,
            enumeratedItemsAttribute,
            visited,
            failures);
    }

    private static void ValidateField(
        string key,
        Type configType,
        Type valueType,
        object parent,
        FieldInfo field,
        string? parentPath,
        HashSet<object> visited,
        List<ConfigurationValidationFailure> failures)
    {
        var objectMembersAttribute = field.GetCustomAttribute<ValidateObjectMembersAttribute>();
        var enumeratedItemsAttribute = field.GetCustomAttribute<ValidateEnumeratedItemsAttribute>();
        if (objectMembersAttribute == null && enumeratedItemsAttribute == null)
        {
            return;
        }

        var memberPath = CombinePath(parentPath, field.Name);
        var memberValue = field.GetValue(parent);

        ValidateOptInMember(
            key,
            configType,
            valueType,
            memberValue,
            memberPath,
            objectMembersAttribute,
            enumeratedItemsAttribute,
            visited,
            failures);
    }

    private static void ValidateOptInMember(
        string key,
        Type configType,
        Type valueType,
        object? memberValue,
        string memberPath,
        ValidateObjectMembersAttribute? objectMembersAttribute,
        ValidateEnumeratedItemsAttribute? enumeratedItemsAttribute,
        HashSet<object> visited,
        List<ConfigurationValidationFailure> failures)
    {
        if (objectMembersAttribute?.Validator != null)
        {
            failures.Add(CreateUnsupportedValidatorFailure(
                key,
                configType,
                valueType,
                memberPath,
                nameof(ValidateObjectMembersAttribute)));
        }

        if (enumeratedItemsAttribute?.Validator != null)
        {
            failures.Add(CreateUnsupportedValidatorFailure(
                key,
                configType,
                valueType,
                memberPath,
                nameof(ValidateEnumeratedItemsAttribute)));
        }

        if (memberValue == null)
        {
            return;
        }

        if (objectMembersAttribute != null && !IsScalar(memberValue.GetType()))
        {
            ValidateNode(
                key,
                configType,
                valueType,
                memberValue,
                memberPath,
                visited,
                failures);
        }

        if (enumeratedItemsAttribute != null && memberValue is IEnumerable enumerable && memberValue is not string)
        {
            var index = 0;
            foreach (var item in enumerable)
            {
                if (item != null && !IsScalar(item.GetType()))
                {
                    ValidateNode(
                        key,
                        configType,
                        valueType,
                        item,
                        $"{memberPath}[{index.ToString(System.Globalization.CultureInfo.InvariantCulture)}]",
                        visited,
                        failures);
                }

                index++;
            }
        }
    }

    private static ConfigurationValidationFailure ToFailure(
        string key,
        Type configType,
        Type valueType,
        string? path,
        ValidationResult result)
    {
        var memberNames = result.MemberNames
            .Select(memberName => PrefixMemberName(path, memberName))
            .Where(memberName => !string.IsNullOrWhiteSpace(memberName))
            .ToList();

        if (memberNames.Count == 0 && path != null)
        {
            memberNames.Add(path);
        }

        return new ConfigurationValidationFailure(
            key,
            configType,
            valueType,
            memberNames,
            result.ErrorMessage ?? "The configuration value is invalid.");
    }

    private static ConfigurationValidationFailure CreateUnsupportedValidatorFailure(
        string key,
        Type configType,
        Type valueType,
        string memberPath,
        string attributeName) =>
        new(
            key,
            configType,
            valueType,
            [memberPath],
            $"{attributeName} custom validator types are not supported by Runnable Config validation.");

    private static IEnumerable<PropertyInfo> GetValidatableProperties(Type type) =>
        type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => property.GetIndexParameters().Length == 0)
            .Where(property => property.GetMethod is { IsPublic: true });

    private static IEnumerable<FieldInfo> GetValidatableFields(Type type) =>
        type.GetFields(BindingFlags.Instance | BindingFlags.Public);

    private static bool TrackVisit(object value, HashSet<object> visited)
    {
        if (value.GetType().IsValueType)
        {
            return true;
        }

        return visited.Add(value);
    }

    private static string CombinePath(string? prefix, string memberName) =>
        string.IsNullOrEmpty(prefix)
            ? memberName
            : $"{prefix}.{memberName}";

    private static string PrefixMemberName(string? path, string memberName)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return memberName;
        }

        return string.IsNullOrWhiteSpace(memberName)
            ? path
            : $"{path}.{memberName}";
    }

    private static bool IsScalar(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;

        return type.IsPrimitive
               || type.IsEnum
               || type == typeof(string)
               || type == typeof(decimal)
               || type == typeof(DateTime)
               || type == typeof(DateTimeOffset)
               || type == typeof(TimeSpan)
               || type == typeof(Guid)
               || type == typeof(Uri);
    }

    private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
    {
        public static ReferenceEqualityComparer Instance { get; } = new();

        public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);

        public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
    }
}

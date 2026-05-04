using System.ComponentModel.DataAnnotations;

namespace ForgeTrust.Runnable.Config;

/// <summary>
/// Base class for Runnable scalar configuration value validation attributes.
/// Apply derived attributes to concrete <see cref="Config{T}"/> or <see cref="ConfigStruct{T}"/> wrapper
/// types to validate resolved scalar values during configuration initialization.
/// </summary>
/// <remarks>
/// <para>
/// Scalar validation runs after provider/default resolution and only when the resolved value is non-null.
/// These attributes validate the value itself; they do not make a missing configuration key required.
/// Use a default value or an application startup presence check when absence should fail.
/// </para>
/// <para>
/// Validation is intentionally strict about value types. Built-in scalar attributes return validation
/// failures for unsupported runtime types instead of converting values. Use an options object with
/// DataAnnotations when validation spans multiple members, needs nested object traversal, or should model
/// required presence separately from value shape.
/// </para>
/// <para>
/// The validation context object is the concrete config wrapper and does not provide application dependency
/// injection services. Override <see cref="Config{T}.ValidateValue"/> or <see cref="ConfigStruct{T}.ValidateValue"/>
/// for scalar rules that cannot be expressed as reusable attributes.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
public abstract class ConfigValueValidationAttribute : ValidationAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigValueValidationAttribute"/> class.
    /// </summary>
    protected ConfigValueValidationAttribute()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigValueValidationAttribute"/> class with an error message.
    /// </summary>
    /// <param name="errorMessage">The validation error message.</param>
    protected ConfigValueValidationAttribute(string errorMessage)
        : base(errorMessage)
    {
    }
}

/// <summary>
/// Validates that a resolved scalar configuration value is not empty.
/// Supported value types are <see cref="string"/> and <see cref="Guid"/>.
/// </summary>
/// <remarks>
/// <para>
/// A null value is treated as successful validation so optional or missing scalar values remain optional.
/// Non-null strings must contain non-whitespace characters, and non-null <see cref="Guid"/> values must not
/// be <see cref="Guid.Empty"/>.
/// </para>
/// <para>
/// Runtime type matching is strict: only <see cref="string"/> and <see cref="Guid"/> are supported. Applying
/// this attribute to a different scalar value type returns a validation failure rather than attempting a
/// conversion. Use an options object with member-level DataAnnotations when required presence, cross-field
/// rules, or richer object validation is part of the contract.
/// </para>
/// </remarks>
public sealed class ConfigValueNotEmptyAttribute : ConfigValueValidationAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigValueNotEmptyAttribute"/> class.
    /// </summary>
    public ConfigValueNotEmptyAttribute()
        : base("The configuration value must not be empty.")
    {
    }

    /// <inheritdoc />
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is null)
        {
            return ValidationResult.Success;
        }

        return value switch
        {
            string text when !string.IsNullOrWhiteSpace(text) => ValidationResult.Success,
            string => new ValidationResult(FormatErrorMessage(validationContext.DisplayName)),
            Guid guid when guid != Guid.Empty => ValidationResult.Success,
            Guid => new ValidationResult(FormatErrorMessage(validationContext.DisplayName)),
            _ => new ValidationResult(CreateUnsupportedTypeMessage(value.GetType()))
        };
    }

    private static string CreateUnsupportedTypeMessage(Type type) =>
        $"{nameof(ConfigValueNotEmptyAttribute)} supports String and Guid values. "
        + $"Config value type '{type.Name}' is not supported.";
}

/// <summary>
/// Validates that a resolved scalar numeric configuration value is inside an inclusive range.
/// Supported value types are <see cref="int"/> and <see cref="double"/>.
/// </summary>
/// <remarks>
/// <para>
/// A null value is treated as successful validation so optional or missing scalar values remain optional.
/// Non-null values are compared against the inclusive minimum and maximum supplied to the constructor.
/// </para>
/// <para>
/// Runtime type matching is strict and follows the constructor overload. The <see cref="int"/> constructor
/// validates only <see cref="int"/> values, and the <see cref="double"/> constructor validates only
/// <see cref="double"/> values. Unsupported runtime types return validation failures instead of being
/// converted. Use an options object when validation needs multiple numeric fields, required presence, or
/// object-level DataAnnotations.
/// </para>
/// </remarks>
public sealed class ConfigValueRangeAttribute : ConfigValueValidationAttribute
{
    private readonly RangeKind _kind;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigValueRangeAttribute"/> class for <see cref="int"/> values.
    /// </summary>
    /// <param name="minimum">The inclusive minimum allowed value.</param>
    /// <param name="maximum">The inclusive maximum allowed value.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="minimum"/> is greater than <paramref name="maximum"/>.
    /// </exception>
    public ConfigValueRangeAttribute(int minimum, int maximum)
        : base("The configuration value must be between {0} and {1}.")
    {
        if (minimum > maximum)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minimum),
                "Minimum must be less than or equal to maximum.");
        }

        _kind = RangeKind.Int32;
        Minimum = minimum;
        Maximum = maximum;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigValueRangeAttribute"/> class for <see cref="double"/> values.
    /// </summary>
    /// <param name="minimum">The inclusive minimum allowed value.</param>
    /// <param name="maximum">The inclusive maximum allowed value.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when either bound is <see cref="double.NaN"/> or <paramref name="minimum"/>
    /// is greater than <paramref name="maximum"/>.
    /// </exception>
    public ConfigValueRangeAttribute(double minimum, double maximum)
        : base("The configuration value must be between {0} and {1}.")
    {
        if (double.IsNaN(minimum))
        {
            throw new ArgumentOutOfRangeException(nameof(minimum), "Minimum must be a valid number.");
        }

        if (double.IsNaN(maximum))
        {
            throw new ArgumentOutOfRangeException(nameof(maximum), "Maximum must be a valid number.");
        }

        if (minimum > maximum)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minimum),
                "Minimum must be less than or equal to maximum.");
        }

        _kind = RangeKind.Double;
        Minimum = minimum;
        Maximum = maximum;
    }

    /// <summary>
    /// Gets the inclusive minimum allowed value.
    /// </summary>
    public object Minimum { get; }

    /// <summary>
    /// Gets the inclusive maximum allowed value.
    /// </summary>
    public object Maximum { get; }

    /// <inheritdoc />
    public override string FormatErrorMessage(string name) =>
        string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            ErrorMessageString,
            Minimum,
            Maximum);

    /// <inheritdoc />
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is null)
        {
            return ValidationResult.Success;
        }

        return _kind switch
        {
            RangeKind.Int32 => ValidateInt32(value),
            RangeKind.Double => ValidateDouble(value),
            _ => ValidationResult.Success
        };
    }

    private ValidationResult? ValidateInt32(object value)
    {
        if (value is not int typedValue)
        {
            return new ValidationResult(CreateUnsupportedTypeMessage(value.GetType(), "Int32"));
        }

        var minimum = (int)Minimum;
        var maximum = (int)Maximum;

        return typedValue >= minimum && typedValue <= maximum
            ? ValidationResult.Success
            : new ValidationResult(FormatErrorMessage(string.Empty));
    }

    private ValidationResult? ValidateDouble(object value)
    {
        if (value is not double typedValue)
        {
            return new ValidationResult(CreateUnsupportedTypeMessage(value.GetType(), "Double"));
        }

        var minimum = (double)Minimum;
        var maximum = (double)Maximum;

        return typedValue >= minimum && typedValue <= maximum
            ? ValidationResult.Success
            : new ValidationResult(FormatErrorMessage(string.Empty));
    }

    private static string CreateUnsupportedTypeMessage(Type type, string supportedType) =>
        $"{nameof(ConfigValueRangeAttribute)} configured for {supportedType} values cannot validate "
        + $"config value type '{type.Name}'.";

    private enum RangeKind
    {
        Int32,
        Double
    }
}

/// <summary>
/// Validates that a resolved scalar string configuration value has at least the configured length.
/// </summary>
/// <remarks>
/// <para>
/// A null value is treated as successful validation so optional or missing scalar values remain optional.
/// Non-null strings must have a length greater than or equal to <see cref="Length"/>.
/// </para>
/// <para>
/// Runtime type matching is strict: only <see cref="string"/> values are supported. Applying this attribute
/// to another scalar value type returns a validation failure rather than converting the value. Use an options
/// object with DataAnnotations when string length is only one part of a larger model contract or when
/// required presence should be represented separately.
/// </para>
/// </remarks>
public sealed class ConfigValueMinLengthAttribute : ConfigValueValidationAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigValueMinLengthAttribute"/> class.
    /// </summary>
    /// <param name="length">The minimum allowed string length.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="length"/> is less than zero.
    /// </exception>
    public ConfigValueMinLengthAttribute(int length)
        : base("The configuration value must be at least {0} character(s) long.")
    {
        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "Length must be greater than or equal to zero.");
        }

        Length = length;
    }

    /// <summary>
    /// Gets the minimum allowed string length.
    /// </summary>
    public int Length { get; }

    /// <inheritdoc />
    public override string FormatErrorMessage(string name) =>
        string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            ErrorMessageString,
            Length);

    /// <inheritdoc />
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is null)
        {
            return ValidationResult.Success;
        }

        if (value is not string text)
        {
            return new ValidationResult(CreateUnsupportedTypeMessage(value.GetType()));
        }

        return text.Length >= Length
            ? ValidationResult.Success
            : new ValidationResult(FormatErrorMessage(validationContext.DisplayName));
    }

    private static string CreateUnsupportedTypeMessage(Type type) =>
        $"{nameof(ConfigValueMinLengthAttribute)} supports String values. "
        + $"Config value type '{type.Name}' is not supported.";
}

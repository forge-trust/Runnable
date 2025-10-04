using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace FakeItEasy;

public static class A
{
    public static T Fake<T>() where T : class
    {
        if (!typeof(T).IsInterface)
        {
            throw new InvalidOperationException("This simplified FakeItEasy stub only supports interface fakes.");
        }

        return FakeManager.CreateFake<T>();
    }

    public static CallConfiguration CallTo(Expression<Action> callExpression)
    {
        if (callExpression is null)
        {
            throw new ArgumentNullException(nameof(callExpression));
        }

        if (callExpression.Body is not MethodCallExpression methodCall)
        {
            throw new ArgumentException("The expression must represent a method call.", nameof(callExpression));
        }

        var target = ExpressionEvaluator.Evaluate(methodCall.Object);
        var state = FakeManager.GetState(target ?? throw new InvalidOperationException("Call target cannot be null."));
        var configuration = state.GetConfiguration(methodCall.Method);

        return new CallConfiguration(configuration);
    }

    public static CallConfiguration<T> CallTo<T>(Expression<Func<T>> callExpression)
    {
        if (callExpression is null)
        {
            throw new ArgumentNullException(nameof(callExpression));
        }

        if (callExpression.Body is not MethodCallExpression methodCall)
        {
            throw new ArgumentException("The expression must represent a method call.", nameof(callExpression));
        }

        var target = ExpressionEvaluator.Evaluate(methodCall.Object);
        var state = FakeManager.GetState(target ?? throw new InvalidOperationException("Call target cannot be null."));
        var configuration = state.GetConfiguration(methodCall.Method);

        return new CallConfiguration<T>(configuration);
    }
}

public static class A<T>
{
    public static T _ => default!;
}

public sealed class CallConfiguration
{
    private readonly FakeCallConfiguration _configuration;

    internal CallConfiguration(FakeCallConfiguration configuration)
    {
        _configuration = configuration;
    }

    public CallConfiguration Invokes(Delegate action)
    {
        _configuration.InvokeAction = args =>
        {
            try
            {
                action.DynamicInvoke(args);
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                throw ex.InnerException;
            }
        };

        return this;
    }

    public CallConfiguration Returns(object? value)
    {
        _configuration.ReturnFunction = _ => value;
        return this;
    }

    public CallConfiguration ReturnsLazily(Delegate factory)
    {
        _configuration.ReturnFunction = args =>
        {
            try
            {
                return factory.DynamicInvoke(args);
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                throw ex.InnerException;
            }
        };
        return this;
    }

    public void MustHaveHappenedOnceExactly()
    {
        _configuration.AssertCallCount(1);
    }
}

public sealed class CallConfiguration<T>
{
    private readonly FakeCallConfiguration _configuration;

    internal CallConfiguration(FakeCallConfiguration configuration)
    {
        _configuration = configuration;
    }

    public CallConfiguration<T> Invokes(Delegate action)
    {
        _configuration.InvokeAction = args =>
        {
            try
            {
                action.DynamicInvoke(args);
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                throw ex.InnerException;
            }
        };

        return this;
    }

    public CallConfiguration<T> Returns(object? value)
    {
        _configuration.ReturnFunction = _ => value;
        return this;
    }

    public CallConfiguration<T> ReturnsLazily(Delegate factory)
    {
        _configuration.ReturnFunction = args =>
        {
            try
            {
                return factory.DynamicInvoke(args);
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                throw ex.InnerException;
            }
        };
        return this;
    }

    public void MustHaveHappenedOnceExactly()
    {
        _configuration.AssertCallCount(1);
    }
}

internal static class ExpressionEvaluator
{
    public static object? Evaluate(Expression? expression)
    {
        if (expression is null)
        {
            return null;
        }

        return Expression.Lambda(expression).Compile().DynamicInvoke();
    }
}

internal static class FakeManager
{
    private static readonly ConditionalWeakTable<object, FakeState> States = new();

    public static T CreateFake<T>() where T : class
    {
        var state = new FakeState();
        var proxy = FakeProxy<T>.Create(state);
        States.Add(proxy!, state);
        return proxy!;
    }

    public static FakeState GetState(object fake)
    {
        if (!States.TryGetValue(fake, out var state))
        {
            throw new InvalidOperationException("The provided object is not a FakeItEasy fake.");
        }

        return state;
    }
}

internal sealed class FakeState
{
    private readonly Dictionary<MethodInfo, FakeCallConfiguration> _configurations = new();

    public FakeCallConfiguration GetConfiguration(MethodInfo method)
    {
        if (!_configurations.TryGetValue(method, out var configuration))
        {
            configuration = new FakeCallConfiguration(method);
            _configurations[method] = configuration;
        }

        return configuration;
    }
}

internal sealed class FakeCallConfiguration
{
    private readonly MethodInfo _method;

    public FakeCallConfiguration(MethodInfo method)
    {
        _method = method;
    }

    public Action<object?[]>? InvokeAction { get; set; }

    public Func<object?[], object?>? ReturnFunction { get; set; }

    public int CallCount { get; private set; }

    public object? Invoke(Type returnType, object?[]? args)
    {
        args ??= Array.Empty<object?>();
        CallCount++;

        InvokeAction?.Invoke(args);

        if (ReturnFunction is not null)
        {
            return ReturnFunction(args);
        }

        if (returnType == typeof(void))
        {
            return null;
        }

        return returnType.IsValueType
            ? Activator.CreateInstance(returnType)
            : null;
    }

    public void AssertCallCount(int expected)
    {
        if (CallCount != expected)
        {
            throw new InvalidOperationException($"Expected method '{_method.Name}' to be called {expected} time(s) but was called {CallCount} time(s).");
        }
    }
}

internal class FakeProxy<T> : DispatchProxy where T : class
{
    private FakeState _state = null!;

    public static T Create(FakeState state)
    {
        var proxy = DispatchProxy.Create<T, FakeProxy<T>>();
        var fakeProxy = (FakeProxy<T>)(object)proxy!;
        fakeProxy._state = state;
        return proxy!;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod is null)
        {
            throw new ArgumentNullException(nameof(targetMethod));
        }

        var configuration = _state.GetConfiguration(targetMethod);
        return configuration.Invoke(targetMethod.ReturnType, args);
    }
}

using System.Reflection;
using Spectre.Console.Cli;

namespace SysDiag.Cli.Infrastructure;

/// <summary>
/// Resolves types for Spectre.Console.Cli: registered services come from the
/// dictionary, everything else is constructed with its constructor parameters
/// taken from the same dictionary.
/// </summary>
internal sealed class TypeResolver : ITypeResolver
{
    private readonly Dictionary<Type, Func<TypeResolver, object>> _factories;

    public TypeResolver(Dictionary<Type, Func<TypeResolver, object>> factories)
    {
        _factories = factories;
    }

    public object? Resolve(Type? type)
    {
        if (type is null)
        {
            return null;
        }

        if (_factories.TryGetValue(type, out Func<TypeResolver, object>? factory))
        {
            return factory(this);
        }

        // Spectre asks for collections of optional extensions, for example
        // IEnumerable<IHelpProvider>. "None registered" has to be an empty array;
        // null would be reported as an unresolvable type.
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
        {
            return Array.CreateInstance(type.GetGenericArguments()[0], 0);
        }

        // Other optional services simply do not exist here. Returning null tells
        // Spectre to fall back to its defaults instead of failing.
        if (type.IsInterface || type.IsAbstract)
        {
            return null;
        }

        return Construct(type);
    }

    /// <summary>
    /// Creates an instance and fills its constructor parameters recursively.
    /// </summary>
    public object Construct(Type type)
    {
        ConstructorInfo? constructor = type
            .GetConstructors()
            .OrderByDescending(candidate => candidate.GetParameters().Length)
            .FirstOrDefault();

        if (constructor is null)
        {
            return Activator.CreateInstance(type)
                ?? throw new InvalidOperationException($"Cannot create an instance of {type}.");
        }

        object?[] arguments = constructor
            .GetParameters()
            .Select(parameter => Resolve(parameter.ParameterType))
            .ToArray();

        return constructor.Invoke(arguments);
    }
}

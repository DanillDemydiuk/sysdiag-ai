using Spectre.Console.Cli;

namespace SysDiag.Cli.Infrastructure;

/// <summary>
/// Minimal dependency registry for Spectre.Console.Cli. The framework registers
/// every command type here and later asks for instances; this implementation
/// hands out the already-built services instead of pulling in a DI container for
/// four objects.
/// </summary>
internal sealed class TypeRegistrar : ITypeRegistrar
{
    /// <summary>
    /// Factories receive the resolver, because a registered implementation may
    /// itself need constructor arguments - which is exactly the case for the
    /// commands, and the reason this cannot be a plain Activator call.
    /// </summary>
    private readonly Dictionary<Type, Func<TypeResolver, object>> _factories = [];

    public void Register(Type service, Type implementation) =>
        _factories[service] = resolver => resolver.Construct(implementation);

    public void RegisterInstance(Type service, object implementation) =>
        _factories[service] = _ => implementation;

    public void RegisterLazy(Type service, Func<object> factory) =>
        _factories[service] = _ => factory();

    public ITypeResolver Build() => new TypeResolver(_factories);
}

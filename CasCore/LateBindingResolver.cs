using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace DouglasDwyer.CasCore;

/// <summary>
/// A helper class for determining which method will <b>actually</b> be
/// invoked as the result of a virtual call.
/// </summary>
/// <remarks>
/// The CLR answers this through two internal QCalls,
/// <c>RuntimeTypeHandle.GetInterfaceMethodImplementation</c> and
/// <c>RuntimeMethodHandle.GetMethodFromCanonical</c>, which this type bound through
/// <c>GetFunctionPointer</c>. Mono provides neither, so the static initializer threw on the
/// first virtual call in the browser and took every plugin load with it. Reflection answers
/// the same question and works everywhere; the answers are memoised per runtime type,
/// because dispatch can never change for a fixed (runtime type, declared method) pair.
/// </remarks>
internal static class LateBindingResolver
{
    /// <summary>
    /// Dispatch answers, partitioned by the runtime type they were asked about.
    /// Weakly keyed so a collectible plugin load context still unloads: a strong key
    /// would pin the type, and with it the assembly that owns it.
    /// </summary>
    private static readonly ConditionalWeakTable<Type, DispatchMap> Dispatched = new();

    /// <summary>
    /// Gets the method that will actually be invoked for a virtual call.
    /// </summary>
    /// <param name="obj">The object for the virtual call.</param>
    /// <param name="method">The method being invoked.</param>
    /// <returns>The actual method that would be called.</returns>
    public static MethodBase GetTargetMethod(object? obj, MethodBase method)
    {
        if (obj is null)
        {
            if (!method.IsStatic && !method.IsConstructor)
            {
                throw new NullReferenceException();
            }

            return method;
        }

        if (method is not MethodInfo info || !info.IsVirtual || info.IsFinal)
        {
            return method;
        }

        var objType = obj.GetType();
        var map = Dispatched.GetOrCreateValue(objType);

        // Arrays implement their interfaces on a runtime helper type that reflection does
        // not surface, so let the runtime build the delegate and report the target it bound.
        if (objType.IsSZArray)
        {
            return map.GetOrAddArray(info, obj);
        }

        return map.GetOrAddVirtual(info, objType);
    }

    /// <summary>
    /// Determines the implementation a virtual call on <paramref name="objType"/> dispatches to.
    /// </summary>
    /// <param name="declared">The method named by the call site, as an open definition.</param>
    /// <param name="objType">The runtime type of the receiver.</param>
    /// <returns>The dispatched method, or the declared one when nothing overrides it.</returns>
    private static MethodBase ResolveVirtual(MethodInfo declared, Type objType)
    {
        if (declared.DeclaringType is { IsInterface: true } iface)
        {
            return ResolveInterface(declared, iface, objType) ?? declared;
        }

        return ResolveOverride(declared, objType) ?? declared;
    }

    /// <summary>
    /// Finds the method implementing an interface member on the given runtime type.
    /// </summary>
    private static MethodBase? ResolveInterface(MethodInfo declared, Type iface, Type objType)
    {
        InterfaceMapping mapping;
        try
        {
            mapping = objType.GetInterfaceMap(iface);
        }
        catch (ArgumentException)
        {
            // The receiver does not implement the interface, so the call itself is
            // invalid; the declared member is all that is left to ask the policy about.
            return null;
        }

        for (var i = 0; i < mapping.InterfaceMethods.Length; i++)
        {
            if (IsSameDeclaration(mapping.InterfaceMethods[i], declared))
            {
                return mapping.TargetMethods[i];
            }
        }

        return null;
    }

    /// <summary>
    /// Finds the most-derived override of a class virtual method at or above the runtime type.
    /// </summary>
    private static MethodBase? ResolveOverride(MethodInfo declared, Type objType)
    {
        var root = declared.GetBaseDefinition();

        for (var type = objType; type is not null; type = type.BaseType)
        {
            foreach (var candidate in type.GetMethods(
                         BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                // A `new` declaration starts a new slot, so its base definition is itself
                // and it cannot be mistaken for an override of this root.
                if (candidate.IsVirtual && candidate.GetBaseDefinition().MethodHandle == root.MethodHandle)
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Determines whether two method references name the same declaration.
    /// </summary>
    private static bool IsSameDeclaration(MethodInfo left, MethodInfo right)
    {
        return left.MethodHandle == right.MethodHandle
            || (left.MetadataToken == right.MetadataToken && left.DeclaringType == right.DeclaringType);
    }

    /// <summary>
    /// Obtains a target method handle by creating a delegate which will automatically perform method resolution.
    /// This method may only be called if there are less than 15 parameters, none of which may be by-refs.
    /// </summary>
    /// <param name="obj">The target object to call.</param>
    /// <param name="info">The method that will be called on the object.</param>
    /// <returns>The method that will actually be called on the object.</returns>
    private static MethodBase GetTargetMethodViaDelegate(object obj, MethodInfo info)
    {
        var parameters = info.GetParameters().Select(x => x.ParameterType);

        Type delegateType;
        if (info.ReturnType.Equals(typeof(void)))
        {
            delegateType = Expression.GetActionType(parameters.ToArray());
        }
        else
        {
            delegateType = Expression.GetFuncType(parameters.Append(info.ReturnType).ToArray());
        }

        return Delegate.CreateDelegate(delegateType, obj, info).Method;
    }

    /// <summary>
    /// Memoised dispatch answers for one runtime type.
    /// </summary>
    private sealed class DispatchMap
    {
        private readonly ConcurrentDictionary<RuntimeMethodHandle, MethodBase> _virtual = new();
        private readonly ConcurrentDictionary<RuntimeMethodHandle, MethodBase> _arrays = new();

        /// <summary>
        /// Gets the target of a class or interface virtual call, resolving it once.
        /// </summary>
        public MethodBase GetOrAddVirtual(MethodInfo declared, Type objType)
        {
            // Dispatch is a property of the open definition: every instantiation of a
            // generic method lands on the same override.
            if (declared.IsGenericMethod && !declared.IsGenericMethodDefinition)
            {
                declared = declared.GetGenericMethodDefinition();
            }

            if (_virtual.TryGetValue(declared.MethodHandle, out var cached))
            {
                return cached;
            }

            return _virtual.GetOrAdd(declared.MethodHandle, ResolveVirtual(declared, objType));
        }

        /// <summary>
        /// Gets the target of a virtual call on an array, resolving it once.
        /// </summary>
        public MethodBase GetOrAddArray(MethodInfo declared, object obj)
        {
            if (_arrays.TryGetValue(declared.MethodHandle, out var cached))
            {
                return cached;
            }

            return _arrays.GetOrAdd(declared.MethodHandle, GetTargetMethodViaDelegate(obj, declared));
        }
    }
}

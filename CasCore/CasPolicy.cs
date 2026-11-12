using System.Collections.Immutable;
using System.Reflection;

namespace DouglasDwyer.CasCore;

/// <summary>
/// Defines a set of whitelisted fields and methods that sandboxed assemblies may access.
/// </summary>
public sealed class CasPolicy
{
    /// <summary>
    /// The set of members that may be accessed under this policy.
    /// </summary>
    private readonly ImmutableHashSet<MemberId> _accessibleMembers;

    /// <summary>
    /// Whether the intrinsic members of array types (construction and element access) may be called.
    /// </summary>
    private readonly bool _allowArrayMembers;

    /// <summary>
    /// The names of the intrinsic members the runtime exposes on every array type.
    /// </summary>
    private static readonly ImmutableHashSet<string> ArrayMemberNames =
        ImmutableHashSet.Create(".ctor", "Get", "Set", "Address");

    /// <summary>
    /// Creates a new CAS policy that allows the provided set of members to be accessed.
    /// </summary>
    /// <param name="members">The set of allowed members.</param>
    internal CasPolicy(ImmutableHashSet<MemberId> members)
        : this(members, false)
    {
    }

    /// <summary>
    /// Creates a new CAS policy that allows the provided set of members to be accessed.
    /// </summary>
    /// <param name="members">The set of allowed members.</param>
    /// <param name="allowArrayMembers">Whether array construction and element access are allowed.</param>
    internal CasPolicy(ImmutableHashSet<MemberId> members, bool allowArrayMembers)
    {
        _accessibleMembers = members;
        _allowArrayMembers = allowArrayMembers;
    }

    /// <summary>
    /// Determines whether this policy allows the specified field to be accessed.
    /// </summary>
    /// <param name="field">The field to be read/written.</param>
    /// <returns>Whether the field is accessible.</returns>
    public bool CanAccess(FieldInfo field)
    {
        var memberId = new MemberId(field);
        return _accessibleMembers.Contains(memberId);
    }

    /// <summary>
    /// Determines whether this policy allows the specified method to be called.
    /// </summary>
    /// <param name="method">The method to be invoked.</param>
    /// <returns>Whether the method is accessible.</returns>
    public bool CanAccess(MethodBase method)
    {
        // Array types expose intrinsic members that all share one metadata token, so they cannot be
        // identified by MemberId. Authorise them from the dedicated flag instead.
        if (IsArrayMember(method))
        {
            return _allowArrayMembers;
        }

        var memberId = new MemberId(method);
        return _accessibleMembers.Contains(memberId);
    }

    /// <summary>
    /// Determines whether the given method is an intrinsic member of an array type.
    /// </summary>
    /// <param name="method">The method in question.</param>
    /// <returns>Whether the method belongs to an array type.</returns>
    private static bool IsArrayMember(MethodBase method)
    {
        return method.DeclaringType is { IsArray: true } && ArrayMemberNames.Contains(method.Name);
    }
}
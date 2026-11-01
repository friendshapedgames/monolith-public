namespace SecretPlanGodot.Core;

/// <summary>
///     Implements <typeparamref name="TProvider" /> on a partial class by forwarding each of its get-only properties
///     to the nearest ancestor that also implements it. Properties the class declares itself are not relayed.
///     The implementation comes from RelayGenerator (FriendShapedGenerators); without it this attribute does nothing.
/// </summary>
/// <typeparam name="TProvider">The provider interface to relay, e.g. IWorldStateProvider.</typeparam>
/// <example>
///     <code>
///     [Relay&lt;IWorldStateProvider&gt;]
///     public partial class Player : CharacterBody3D { }
///     </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class RelayAttribute<TProvider> : Attribute where TProvider : class
{
    public Type ProviderInterface => typeof(TProvider);
}

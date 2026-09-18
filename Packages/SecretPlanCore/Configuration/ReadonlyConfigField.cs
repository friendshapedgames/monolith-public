namespace SecretPlanCore.Configuration;

/// <summary>
///     If present, this field will report itself as "readonly" it is up to the editor to decide what that means.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class ReadonlyConfigField : Attribute;
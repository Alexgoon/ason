
namespace Ason;

public abstract class ProxyAttributeBase : Attribute
{
    public string? Description { get; }
    protected ProxyAttributeBase(string? description = null)
    {
        Description = description;
    }
}

[AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
public sealed class AsonOperatorAttribute : ProxyAttributeBase
{
    // Optional override for the target name used over the wire (defaults to type name)
    public string? TargetName { get; }
    public AsonOperatorAttribute(string? targetName = null, string? description = null) : base(description)
    {
        TargetName = targetName;
    }
}

[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class AsonMethodAttribute : ProxyAttributeBase
{
    public AsonMethodAttribute(string? description = null) : base(description)
    {
    }
}

[AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
public sealed class AsonModelAttribute : ProxyAttributeBase
{
    public AsonModelAttribute(string? description = null) : base(description)
    {
    }
}

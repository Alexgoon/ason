
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
public sealed class ProxyClassAttribute : ProxyAttributeBase
{
    // Optional override for the target name used over the wire (defaults to type name)
    public string? TargetName { get; }
    public ProxyClassAttribute(string? targetName = null, string? description = null) : base(description)
    {
        TargetName = targetName;
    }
}

[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class ProxyMethodAttribute : ProxyAttributeBase
{
    public ProxyMethodAttribute(string? description = null) : base(description)
    {
    }
}

[AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
public sealed class ProxyModelAttribute : ProxyAttributeBase
{
    public ProxyModelAttribute(string? description = null) : base(description)
    {
    }
}

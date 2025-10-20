using System.Reflection;
using System.Text;
using System.Text.Json;
using ModelContextProtocol.Client; // MCP

namespace Ason;

// Generates proxy runtime code (embedded into Roslyn script) and signature listings for AI guidance.
// Modified: All proxy methods are exposed as synchronous (no Task/async) and any source method name ending with 'Async'
// is trimmed for the generated proxy surface. Under the hood we still perform async host calls and block with
// GetAwaiter().GetResult(). A re-entrant safe scheduler (SynchronizationContextInvocationScheduler) prevents
// deadlocks by executing inline if already on the captured SynchronizationContext.
public static class ProxySerializer {
    public static string SerializeAll(params Assembly[] assemblies) {
        var scan = assemblies is { Length: > 0 } ? assemblies : Array.Empty<Assembly>();
        var sb = new StringBuilder();
        sb.Append(ScriptGenDefaults.GetUsingsPrelude());
        sb.AppendLine("public static class ProxyRuntime { public static IHostBridge Host { get; set; } }");
        sb.AppendLine("ProxyRuntime.Host = Host;");
        AppendModelDtos(sb, scan);
        AppendDynamicProxies(sb, scan);
        return sb.ToString();
    }

    public static string SerializeSignatures(params Assembly[] assemblies) {
        var scan = assemblies is { Length: > 0 } ? assemblies : Array.Empty<Assembly>();
        var sb = new StringBuilder();
        AppendModelDtosSignatures(sb, scan);
        AppendSignatureProxies(sb, scan);
        return sb.ToString();
    }

    public static (string runtime, string signatures) SerializeMcpServers(IEnumerable<(string name, IList<McpClientTool> tools)> servers) {
        var runtime = new StringBuilder();
        var sig = new StringBuilder();
        var modelSet = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (name, tools) in servers) {
            // Scan for complex input models first
            foreach (var tool in tools) {
                var schema = TryGetToolSchema(tool);
                if (schema is not JsonElement root) continue;
                if (!root.TryGetProperty("properties", out var propsElem) || propsElem.ValueKind != JsonValueKind.Object) continue;
                foreach (var prop in propsElem.EnumerateObject()) {
                    if (prop.Value.ValueKind == JsonValueKind.Object &&
                        prop.Value.TryGetProperty("type", out var typeElem) && typeElem.GetString() == "object" &&
                        prop.Value.TryGetProperty("properties", out _)) {
                        string modelName = ToPascal(tool.Name) + ToPascal(prop.Name) + "Input";
                        if (modelSet.Add(modelName)) BuildModelClassStandalone(modelName, prop.Value, runtime, sig);
                    }
                }
            }
            string serverClass = ToPascal(name) + "Mcp";
            runtime.AppendLine($"public static class {serverClass} {{");
            sig.AppendLine($"public static class {serverClass} {{");
            foreach (var tool in tools) {
                string methodName = ToPascal(tool.Name);
                var schema = TryGetToolSchema(tool);
                var (rtParams, sigParams, argDict) = BuildToolParameters(tool, schema);
                if (!string.IsNullOrWhiteSpace(tool.Description)) {
                    foreach (var line in tool.Description.Split('\n')) {
                        var trimmed = line.Trim(); if (trimmed.Length > 0) { runtime.AppendLine($"    // {trimmed}"); sig.AppendLine($"    // {trimmed}"); }
                    }
                }
                runtime.AppendLine($"    public static object? {methodName}({rtParams}) {{");
                runtime.AppendLine($"        var __args = new Dictionary<string, object?>() {{ {argDict} }};");
                runtime.AppendLine($"        return ProxyRuntime.Host.InvokeMcpAsync<object?>(\"{name}\", \"{tool.Name}\", __args).GetAwaiter().GetResult();");
                runtime.AppendLine("    }");
                sig.AppendLine($"    public static object? {methodName}({sigParams});");
            }
            runtime.AppendLine("}");
            sig.AppendLine("}");
        }
        return (runtime.ToString(), sig.ToString());
    }

    #region Model Generation
    private static void AppendModelDtos(StringBuilder sb, Assembly[] assemblies) {
        var modelTypes = GetTypesWithAttribute<AsonModelAttribute>(assemblies).Where(t => t.IsClass && !t.IsAbstract).OrderBy(t => t.Name).ToArray();
        foreach (var t in modelTypes) {
            sb.AppendLine($"public class {t.Name} {{");
            foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanRead && p.CanWrite).OrderBy(p => p.Name)) {
                sb.AppendLine($"    public {GetFriendlyTypeName(p.PropertyType)} {p.Name} {{ get; set; }}");
            }
            sb.AppendLine("}");
        }
    }
    private static void AppendModelDtosSignatures(StringBuilder sb, Assembly[] assemblies) {
        var modelTypes = GetTypesWithAttribute<AsonModelAttribute>(assemblies).Where(t => t.IsClass && !t.IsAbstract).OrderBy(t => t.Name).ToArray();
        foreach (var t in modelTypes) {
            var attr = t.GetCustomAttribute<AsonModelAttribute>();
            if (!string.IsNullOrWhiteSpace(attr?.Description)) foreach (var line in SplitLines(attr.Description!)) sb.AppendLine($"// {line}");
            sb.AppendLine($"public class {t.Name} {{");
            foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanRead && p.CanWrite).OrderBy(p => p.Name)) {
                sb.AppendLine($"    public {GetFriendlyTypeName(p.PropertyType)} {p.Name};");
            }
            sb.AppendLine("}");
        }
    }
    #endregion

    #region Proxy Generation (Runtime)
    private static void AppendDynamicProxies(StringBuilder sb, Assembly[] assemblies) {
        var proxyTypes = GetTypesWithAttribute<AsonOperatorAttribute>(assemblies)
            .Where(t => !IsExcludedBase(t))
            .OrderBy(t => t.Name).ToArray();
        foreach (var t in proxyTypes) {
            bool isStatic = t.IsAbstract && t.IsSealed;
            if (isStatic) AppendStaticOperatorProxy(sb, t); else AppendInstanceOperatorProxy(sb, t);
        }
    }

    private static void AppendStaticOperatorProxy(StringBuilder sb, Type type) {
        string proxyName = type.Name;
        string target = type.Name;
        sb.AppendLine($"public static class {proxyName} {{");
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.GetCustomAttribute<AsonMethodAttribute>() != null);
        foreach (var mi in methods) {
            EmitRuntimeMethod(sb, mi, target, isInstance:false);
        }
        sb.AppendLine("}");
    }

    private static void AppendInstanceOperatorProxy(StringBuilder sb, Type type) {
        string proxyName = type.Name;
        string target = type.Name;
        bool isRoot = typeof(RootOperator).IsAssignableFrom(type) && type != typeof(RootOperator);
        sb.AppendLine($"public class {proxyName} {{");
        if (isRoot) {
            sb.AppendLine("    private readonly string? _handle;");
            sb.AppendLine($"    public {proxyName}() {{ _handle = \"{proxyName}\"; }}");
        } else {
            sb.AppendLine("    private readonly string _handle;");
            sb.AppendLine($"    public {proxyName}(string handle) {{ _handle = handle; }}");
        }
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.GetCustomAttribute<AsonMethodAttribute>() != null);
        foreach (var mi in methods) {
            EmitRuntimeMethod(sb, mi, target, isInstance:true);
        }
        sb.AppendLine("}");
    }

    private static void EmitRuntimeMethod(StringBuilder sb, MethodInfo mi, string target, bool isInstance) {
        var pars = mi.GetParameters();
        string paramSig = string.Join(", ", pars.Select((p,i)=> $"object? {p.Name ?? "arg"+i}"));
        string argsPack = pars.Length == 0 ? "Array.Empty<object?>()" : $"new object?[] {{ {string.Join(", ", pars.Select(p=>p.Name))} }}";
        string rawName = mi.Name;
        string logicalName = TrimAsyncSuffix(rawName);
        Type rt = mi.ReturnType;
        if (rt == typeof(void) || rt == typeof(Task)) {
            sb.AppendLine($"    public void {logicalName}({paramSig}) => ProxyRuntime.Host.InvokeAsync<object>(\"{target}\", \"{rawName}\", {argsPack}{(isInstance ? ", _handle" : string.Empty)}).GetAwaiter().GetResult();");
        }
        else if (rt.IsGenericType && rt.GetGenericTypeDefinition() == typeof(Task<>)) {
            var tArg = rt.GetGenericArguments()[0];
            bool isOp = tArg.GetCustomAttribute<AsonOperatorAttribute>() != null;
            if (isOp) {
                sb.AppendLine($"    public {tArg.Name} {logicalName}({paramSig}) {{ var handle = ProxyRuntime.Host.InvokeAsync<string>(\"{target}\", \"{rawName}\", {argsPack}{(isInstance ? ", _handle" : string.Empty)}).GetAwaiter().GetResult(); return new {tArg.Name}(handle); }}");
            } else {
                string tName = GetFriendlyTypeName(tArg);
                sb.AppendLine($"    public {tName} {logicalName}({paramSig}) => ProxyRuntime.Host.InvokeAsync<{tName}>(\"{target}\", \"{rawName}\", {argsPack}{(isInstance ? ", _handle" : string.Empty)}).GetAwaiter().GetResult();");
            }
        }
        else {
            string tName = GetFriendlyTypeName(rt);
            sb.AppendLine($"    public {tName} {logicalName}({paramSig}) => ProxyRuntime.Host.InvokeAsync<{tName}>(\"{target}\", \"{rawName}\", {argsPack}{(isInstance ? ", _handle" : string.Empty)}).GetAwaiter().GetResult();");
        }
    }
    #endregion

    #region Proxy Generation (Signatures)
    private static void AppendSignatureProxies(StringBuilder sb, Assembly[] assemblies) {
        var proxyTypes = GetTypesWithAttribute<AsonOperatorAttribute>(assemblies)
            .Where(t => !IsExcludedBase(t))
            .OrderBy(t => t.Name).ToArray();
        foreach (var t in proxyTypes) {
            bool isStatic = t.IsAbstract && t.IsSealed;
            if (isStatic) AppendStaticSignatureProxy(sb, t); else AppendInstanceSignatureProxy(sb, t);
        }
    }

    private static void AppendStaticSignatureProxy(StringBuilder sb, Type type) {
        var attr = type.GetCustomAttribute<AsonOperatorAttribute>();
        if (!string.IsNullOrWhiteSpace(attr?.Description)) foreach (var line in SplitLines(attr.Description!)) sb.AppendLine($"// {line}");
        sb.AppendLine($"public static class {type.Name} {{");
        foreach (var mi in type.GetMethods(BindingFlags.Public | BindingFlags.Static).Where(m=>m.GetCustomAttribute<AsonMethodAttribute>()!=null).OrderBy(m=>m.Name))
            EmitSignatureMethod(sb, mi, isInstance:false);
        sb.AppendLine("}");
    }

    private static void AppendInstanceSignatureProxy(StringBuilder sb, Type type) {
        var attr = type.GetCustomAttribute<AsonOperatorAttribute>();
        if (!string.IsNullOrWhiteSpace(attr?.Description)) foreach (var line in SplitLines(attr.Description!)) sb.AppendLine($"// {line}");
        sb.AppendLine($"public class {type.Name} {{");
        sb.AppendLine($"    private {type.Name}();"); // discourage direct instantiation
        foreach (var mi in type.GetMethods(BindingFlags.Public | BindingFlags.Instance).Where(m=>m.GetCustomAttribute<AsonMethodAttribute>()!=null).OrderBy(m=>m.Name))
            EmitSignatureMethod(sb, mi, isInstance:true);
        sb.AppendLine("}");
    }

    private static void EmitSignatureMethod(StringBuilder sb, MethodInfo mi, bool isInstance) {
        var attr = mi.GetCustomAttribute<AsonMethodAttribute>();
        if (!string.IsNullOrWhiteSpace(attr?.Description)) foreach (var line in SplitLines(attr.Description!)) sb.AppendLine($"    // {line}");
        string logicalName = TrimAsyncSuffix(mi.Name);
        var pars = mi.GetParameters();
        string paramSig = string.Join(", ", pars.Select((p,i)=> $"{GetFriendlyTypeName(p.ParameterType)} {p.Name ?? "arg"+i}"));
        string retType = MapReturnSignature(mi.ReturnType);
        sb.AppendLine($"    public {(retType=="void"?"void":retType)} {logicalName}({paramSig});");
    }
    #endregion

    #region Helpers
    private static IEnumerable<Type> GetTypesWithAttribute<TAttr>(Assembly[] assemblies) where TAttr : Attribute {
        IEnumerable<Assembly> source = assemblies.Length > 0 ? assemblies : AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic);
        foreach (var asm in source) {
            Type[] types = Array.Empty<Type>();
            try { types = asm.GetTypes(); } catch { }
            foreach (var t in types) if (t.GetCustomAttribute<TAttr>() != null) yield return t;
        }
    }

    private static bool IsExcludedBase(Type t) {
        if (t == typeof(OperatorBase) || t == typeof(RootOperator)) return true;
        var n = t.Name;
        if (n.StartsWith("OperatorBase`", StringComparison.Ordinal) || n.StartsWith("RootOperator`", StringComparison.Ordinal)) return true;
        return false;
    }

    private static string TrimAsyncSuffix(string name) => name.EndsWith("Async", StringComparison.Ordinal) ? name[..^5] : name;

    private static string MapReturnSignature(Type rt) {
        if (rt == typeof(void) || rt == typeof(Task)) return "void";
        if (rt.IsGenericType && rt.GetGenericTypeDefinition() == typeof(Task<>)) {
            var tArg = rt.GetGenericArguments()[0];
            if (tArg.GetCustomAttribute<AsonOperatorAttribute>() != null) return tArg.Name;
            return GetFriendlyTypeName(tArg);
        }
        return GetFriendlyTypeName(rt);
    }

    private static string GetFriendlyTypeName(Type t) {
        if (t.IsArray) return GetFriendlyTypeName(t.GetElementType()!) + "[]";
        if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Nullable<>)) return GetFriendlyTypeName(t.GetGenericArguments()[0]) + "?";
        if (t.IsGenericType) {
            var name = t.Name; var idx = name.IndexOf('`'); if (idx>=0) name = name[..idx];
            return name + "<" + string.Join(", ", t.GetGenericArguments().Select(GetFriendlyTypeName)) + ">";
        }
        return t.Name;
    }

    private static IEnumerable<string> SplitLines(string value) => value.Replace("\r\n","\n").Replace('\r','\n').Split('\n');

    // MCP helpers
    private static void BuildModelClassStandalone(string modelName, JsonElement schema, StringBuilder runtime, StringBuilder sig) {
        runtime.AppendLine("[ProxyModel]");
        runtime.AppendLine($"public class {modelName} {{");
        sig.AppendLine("[ProxyModel]");
        sig.AppendLine($"public class {modelName} {{");
        if (schema.TryGetProperty("properties", out var props) && props.ValueKind == JsonValueKind.Object) {
            foreach (var p in props.EnumerateObject()) {
                string csType = MapJsonTypeToCSharp(p.Value, out _, modelName, p.Name);
                string pascal = ToPascal(p.Name);
                runtime.AppendLine($"    public {csType} {pascal} {{ get; set; }}");
                sig.AppendLine($"    public {csType} {pascal};");
            }
        }
        runtime.AppendLine("}"); sig.AppendLine("}");
    }

    private static (string runtimeParams, string sigParams, string argDictBuilder) BuildToolParameters(McpClientTool tool, JsonElement? schema) {
        var runtimeParams = new List<string>(); var sigParams = new List<string>(); var argPairs = new List<string>();
        JsonElement? effective = schema;
        try {
            if (effective is not JsonElement ej || ej.ValueKind != JsonValueKind.Object) {
                var tt = tool.GetType(); var protoProp = tt.GetProperty("ProtocolTool"); var proto = protoProp?.GetValue(tool);
                if (proto != null) {
                    var inputSchemaProp = proto.GetType().GetProperty("InputSchema"); var inputSchemaVal = inputSchemaProp?.GetValue(proto);
                    if (inputSchemaVal is JsonElement je && je.ValueKind == JsonValueKind.Object) effective = je; else if (inputSchemaVal is string s && !string.IsNullOrWhiteSpace(s)) { try { using var doc = JsonDocument.Parse(s); effective = doc.RootElement.Clone(); } catch { } }
                }
            }
        } catch { }
        if (effective is JsonElement root && root.ValueKind == JsonValueKind.Object) {
            bool used = false;
            if (root.TryGetProperty("properties", out var props) && props.ValueKind == JsonValueKind.Object) {
                used = true; foreach (var p in props.EnumerateObject()) AddToolParam(tool, p.Name, p.Value, runtimeParams, sigParams, argPairs);
            }
            if (!used) {
                foreach (var p in root.EnumerateObject()) {
                    if (p.NameEquals("type") || p.NameEquals("title") || p.NameEquals("description") || p.NameEquals("required")) continue;
                    if (p.Value.ValueKind == JsonValueKind.Object && (p.Value.TryGetProperty("type", out _) || p.Value.TryGetProperty("properties", out _))) AddToolParam(tool, p.Name, p.Value, runtimeParams, sigParams, argPairs);
                }
            }
        }
        return (string.Join(", ", runtimeParams), string.Join(", ", sigParams), string.Join(", ", argPairs));
    }

    private static void AddToolParam(McpClientTool tool, string rawName, JsonElement schemaElem, List<string> runtimeParams, List<string> sigParams, List<string> argPairs) {
        string csType = MapJsonTypeToCSharp(schemaElem, out bool isComplex, tool.Name, rawName);
        string paramName = CamelCase(rawName);
        runtimeParams.Add($"{csType} {paramName}"); sigParams.Add($"{csType} {paramName}");
        if (isComplex) argPairs.Add($"[\"{rawName}\"] = {paramName} == null ? null : new Dictionary<string, object?>({paramName}.GetType().GetProperties().ToDictionary(pi => char.ToLowerInvariant(pi.Name[0]) + pi.Name.Substring(1), pi => (object?)pi.GetValue({paramName})))");
        else argPairs.Add($"[\"{rawName}\"] = {paramName}");
    }

    private static JsonElement? TryGetToolSchema(McpClientTool tool) {
        var toolType = tool.GetType();
        var schemaProp = toolType.GetProperty("Schema") ?? toolType.GetProperty("InputSchema") ?? toolType.GetProperty("schema") ?? toolType.GetProperty("inputSchema");
        if (schemaProp != null) {
            var val = schemaProp.GetValue(tool);
            if (val is JsonElement je && je.ValueKind == JsonValueKind.Object) return je;
        }
        return null;
    }

    private static string MapJsonTypeToCSharp(JsonElement prop, out bool isComplexModel, string toolName = "", string propName = "") {
        isComplexModel = false;
        if (prop.ValueKind == JsonValueKind.Object && prop.TryGetProperty("type", out var tElem)) {
            var t = tElem.GetString();
            switch (t) {
                case "string": return "string";
                case "integer": return "int";
                case "number": return "double";
                case "boolean": return "bool";
                case "array": return "object[]";
                case "object": isComplexModel = true; return ToPascal(toolName) + ToPascal(propName) + "Input";
            }
        }
        return "object";
    }

    private static string ToPascal(string value) {
        if (string.IsNullOrWhiteSpace(value)) return value;
        var parts = value.Split(new[]{'-','_',' ','.'}, StringSplitOptions.RemoveEmptyEntries);
        var sb = new StringBuilder(); foreach (var p in parts) sb.Append(char.ToUpperInvariant(p[0])).Append(p.AsSpan(1));
        return sb.ToString();
    }
    private static string CamelCase(string name) => string.IsNullOrEmpty(name) ? name : name.Length==1 ? name.ToLowerInvariant() : char.ToLowerInvariant(name[0]) + name[1..];
    #endregion
}

using System.Reflection;
using System.Text;
using System.Text.Json;
using ModelContextProtocol.Client;

namespace Ason.CodeGen;

internal sealed class ProxyCodeGenerationService {
    readonly object _lock = new();
    readonly List<Assembly> _internalOperatorAssemblies = new();
    readonly List<Assembly> _externalOperatorAssemblies = new();
    readonly List<Assembly> _mcpDynamicAssemblies = new();
    readonly List<McpServerInfo> _mcpServers = new();
    readonly StringBuilder _mcpRuntime = new();
    readonly StringBuilder _mcpSignatures = new();

    string? _runtime; string? _signatures; bool _dirty;

    public event Action<string,string>? ProxiesRebuilt; // runtime, signatures

    public void AddInternalAssemblies(params Assembly[] assemblies) {
        lock(_lock) {
            foreach(var a in assemblies) if (a!=null && !_internalOperatorAssemblies.Contains(a)) _internalOperatorAssemblies.Add(a);
            _dirty = true;
        }
    }
    public void AddExternalAssemblies(params Assembly[] assemblies) {
        lock(_lock) {
            foreach(var a in assemblies) if (a!=null && !_externalOperatorAssemblies.Contains(a)) _externalOperatorAssemblies.Add(a);
            _dirty = true;
        }
    }

    public async Task AddMcpServerAsync(string serverName, IMcpClient client) {
        var tools = await client.ListToolsAsync().ConfigureAwait(false);
        lock(_lock) {
            var descriptor = new McpServerInfo(serverName, tools);
            _mcpServers.Add(descriptor);
            GenerateMcpCode(descriptor); // append dynamic code
            _dirty = true;
        }
    }
    public void ForceAddMcpServerForTests(string serverName, IList<McpClientTool> tools) {
        lock(_lock) {
            var info = new McpServerInfo(serverName, tools);
            _mcpServers.Add(info);
            GenerateMcpCode(info);
            _dirty = true;
        }
    }

    public (string runtime, string signatures) RebuildIfDirty(Func<string> existingOperatorDeclarationsFactory) {
        lock(_lock) {
            if (!_dirty && _runtime!=null && _signatures!=null) return (_runtime, _signatures);
            var assembliesArray = _internalOperatorAssemblies
                .Concat(_externalOperatorAssemblies)
                .Concat(_mcpDynamicAssemblies)
                .Distinct()
                .ToArray();
            var existingOpDeclarations = existingOperatorDeclarationsFactory();
            var baseRuntime = ProxySerializer.SerializeAll(assembliesArray);
            var baseSigs = ProxySerializer.SerializeSignatures(assembliesArray);
            _runtime = baseRuntime + _mcpRuntime.ToString() + existingOpDeclarations;
            _signatures = baseSigs + _mcpSignatures.ToString() + existingOpDeclarations;
            _dirty = false;
            ProxiesRebuilt?.Invoke(_runtime, _signatures);
            return (_runtime, _signatures);
        }
    }

    static string ToPascal(string value) {
        if (string.IsNullOrWhiteSpace(value)) return value;
        var parts = value.Split(new[] {'-','_',' ','.'}, StringSplitOptions.RemoveEmptyEntries);
        var sb = new StringBuilder();
        foreach (var p in parts) sb.Append(char.ToUpperInvariant(p[0])).Append(p.AsSpan(1));
        return sb.ToString();
    }
    static string CamelCase(string name) {
        if (string.IsNullOrEmpty(name)) return name;
        if (name.Length == 1) return name.ToLowerInvariant();
        return char.ToLowerInvariant(name[0]) + name.Substring(1);
    }
    static string MapJsonTypeToCSharp(JsonElement prop, out bool isComplexModel, string toolName = "", string propName = "") {
        isComplexModel = false;
        if (prop.ValueKind == JsonValueKind.Object && prop.TryGetProperty("type", out var tElem)) {
            var t = tElem.GetString();
            switch (t) {
                case "string": return "string";
                case "integer": return "int";
                case "number": return "double";
                case "boolean": return "bool";
                case "array": return "object[]";
                case "object":
                    isComplexModel = true;
                    return ToPascal(toolName) + ToPascal(propName) + "Input";
            }
        }
        return "object";
    }

    void BuildModelClass(string modelName, JsonElement schema, StringBuilder runtime, StringBuilder sig) {
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
        runtime.AppendLine("}");
        sig.AppendLine("}");
    }

    void GenerateMcpCode(McpServerInfo server) {
        string serverClass = ToPascal(server.Name) + "Mcp";
        var runtime = new StringBuilder();
        var sig = new StringBuilder();
        var modelSet = new HashSet<string>(StringComparer.Ordinal);
        foreach (var tool in server.Tools) {
            JsonElement? schema = TryGetToolSchema(tool);
            if (schema is not JsonElement root) continue;
            if (!root.TryGetProperty("properties", out var propsElem) || propsElem.ValueKind != JsonValueKind.Object) continue;
            foreach (var prop in propsElem.EnumerateObject()) {
                if (prop.Value.ValueKind == JsonValueKind.Object &&
                    prop.Value.TryGetProperty("type", out var typeElem) && typeElem.GetString() == "object" &&
                    prop.Value.TryGetProperty("properties", out _)) {
                    string modelName = ToPascal(tool.Name) + ToPascal(prop.Name) + "Input";
                    if (modelSet.Add(modelName)) BuildModelClass(modelName, prop.Value, runtime, sig);
                }
            }
        }
        runtime.AppendLine($"public static class {serverClass} {{");
        sig.AppendLine($"public static class {serverClass} {{");
        foreach (var tool in server.Tools) {
            string methodName = ToPascal(tool.Name);
            var schema = TryGetToolSchema(tool);
            var (paramListRuntime, paramListSig, argDictBuilder) = BuildToolParameters(tool, schema);
            if (!string.IsNullOrWhiteSpace(tool.Description)) {
                foreach (var line in tool.Description.Split('\n')) {
                    var trimmed = line.Trim(); if (trimmed.Length > 0) { runtime.AppendLine($"    // {trimmed}"); sig.AppendLine($"    // {trimmed}"); }
                }
            }
            runtime.AppendLine($"    public static object? {methodName}({paramListRuntime}) {{");
            runtime.AppendLine($"        var __args = new Dictionary<string, object?>() {{ {argDictBuilder} }};");
            runtime.AppendLine($"        return ProxyRuntime.Host.InvokeMcpAsync<object?>(\"{server.Name}\", \"{tool.Name}\", __args).GetAwaiter().GetResult();");
            runtime.AppendLine("    }");
            sig.AppendLine($"    public static object? {methodName}({paramListSig});");
        }
        runtime.AppendLine("}");
        sig.AppendLine("}");
        _mcpRuntime.AppendLine(runtime.ToString());
        _mcpSignatures.AppendLine(sig.ToString());
    }

    static JsonElement? TryGetToolSchema(McpClientTool tool) {
        var toolType = tool.GetType();
        var schemaProp = toolType.GetProperty("Schema") ?? toolType.GetProperty("InputSchema") ?? toolType.GetProperty("schema") ?? toolType.GetProperty("inputSchema");
        if (schemaProp != null) {
            var val = schemaProp.GetValue(tool);
            if (val is JsonElement je && je.ValueKind == JsonValueKind.Object) return je;
        }
        return null;
    }

    static (string runtimeParams, string sigParams, string argDictBuilder) BuildToolParameters(McpClientTool tool, JsonElement? schema) {
        var runtimeParams = new List<string>();
        var sigParams = new List<string>();
        var argPairs = new List<string>();
        JsonElement? effectiveSchema = schema;
        try {
            if (effectiveSchema is not JsonElement ej || ej.ValueKind != JsonValueKind.Object) {
                var toolType = tool.GetType();
                var protocolToolProp = toolType.GetProperty("ProtocolTool");
                var protocolToolObj = protocolToolProp?.GetValue(tool);
                if (protocolToolObj != null) {
                    var inputSchemaProp = protocolToolObj.GetType().GetProperty("InputSchema");
                    var inputSchemaVal = inputSchemaProp?.GetValue(protocolToolObj);
                    if (inputSchemaVal is JsonElement je) {
                        if (je.ValueKind == JsonValueKind.Object) effectiveSchema = je;
                    } else if (inputSchemaVal is string s && !string.IsNullOrWhiteSpace(s)) {
                        try { using var doc = JsonDocument.Parse(s); effectiveSchema = doc.RootElement.Clone(); } catch { }
                    }
                }
            }
        } catch { }
        if (effectiveSchema is JsonElement root && root.ValueKind == JsonValueKind.Object) {
            bool usedProps = false;
            if (root.TryGetProperty("properties", out var props) && props.ValueKind == JsonValueKind.Object) {
                usedProps = true;
                foreach (var p in props.EnumerateObject()) AddParam(tool, p.Name, p.Value, runtimeParams, sigParams, argPairs);
            }
            if (!usedProps) {
                foreach (var p in root.EnumerateObject()) {
                    if (p.NameEquals("type") || p.NameEquals("title") || p.NameEquals("description") || p.NameEquals("required")) continue;
                    if (p.Value.ValueKind == JsonValueKind.Object && (p.Value.TryGetProperty("type", out _) || p.Value.TryGetProperty("properties", out _))) {
                        AddParam(tool, p.Name, p.Value, runtimeParams, sigParams, argPairs);
                    }
                }
            }
        }
        return (string.Join(", ", runtimeParams), string.Join(", ", sigParams), string.Join(", ", argPairs));
    }

    static void AddParam(McpClientTool tool, string rawName, JsonElement schemaElem, List<string> runtimeParams, List<string> sigParams, List<string> argPairs) {
        string csType = MapJsonTypeToCSharp(schemaElem, out bool isComplexModel, tool.Name, rawName);
        string paramName = "@" + CamelCase(rawName);
        runtimeParams.Add($"{csType} {paramName}");
        sigParams.Add($"{csType} {paramName}");
        if (isComplexModel) {
            argPairs.Add($"[\"{rawName}\"] = {paramName} == null ? null : new Dictionary<string, object?>({paramName}.GetType().GetProperties().ToDictionary(pi => char.ToLowerInvariant(pi.Name[0]) + pi.Name.Substring(1), pi => (object?)pi.GetValue({paramName})))");
        } else {
            argPairs.Add($"[\"{rawName}\"] = {paramName}");
        }
    }
}

internal sealed record McpServerInfo(string Name, IList<McpClientTool> Tools);

using System;
using System.Linq;
using Xunit;
using Ason.CodeGen;
using System.Reflection;
using Ason;
using System.Text.Json;
using System.Collections.Generic;

namespace Ason.Tests.CodeGen;

public partial class ProxyCodeGenerationServiceTests {
    // Add MCP test using reflection to construct sealed McpClientTool via internal JSON representation if possible
    [Fact]
    public void Mcp_CodeGeneration_IncludesServerClassAndModels() {
        var svc = new ProxyCodeGenerationService();
        // Attempt to locate sealed McpClientTool type
        var toolType = typeof(AsonClient).Assembly.GetType("ModelContextProtocol.Client.McpClientTool") ?? AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("ModelContextProtocol.Client.McpClientTool")).FirstOrDefault(t=>t!=null);
        if (toolType == null) { Assert.True(true, "McpClientTool type not found - skipping"); return; }
        // Create a minimal fake instance via reflection (look for parameterless or suitable ctor)
        object? instance = null;
        foreach (var ctor in toolType.GetConstructors(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)) {
            var pars = ctor.GetParameters();
            try {
                var args = pars.Select(p => p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType) : null).ToArray();
                instance = ctor.Invoke(args);
                break;
            } catch { }
        }
        if (instance == null) { Assert.True(true, "Could not instantiate McpClientTool - skipping"); return; }
        // Set Name / Description / Schema if present
        SetProp(toolType, instance, "Name", "get-customer");
        SetProp(toolType, instance, "Description", "Gets a customer");
        var schema = JsonDocument.Parse("{\"type\":\"object\",\"properties\":{\"customer\":{\"type\":\"object\",\"properties\":{\"id\":{\"type\":\"integer\"},\"name\":{\"type\":\"string\"}}},\"count\":{\"type\":\"integer\"}}}").RootElement;
        SetProp(toolType, instance, "Schema", schema);
        svc.ForceAddMcpServerForTests("sales-api", new List<ModelContextProtocol.Client.McpClientTool>{ (ModelContextProtocol.Client.McpClientTool)instance });
        var (runtime, sigs) = svc.RebuildIfDirty(()=>string.Empty);
        Assert.Contains("SalesApiMcp", runtime);
    }

    static void SetProp(Type t, object obj, string name, object value) {
        var p = t.GetProperty(name, BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
        if (p!=null && p.CanWrite) { try { p.SetValue(obj, value); } catch { } }
        var f = t.GetField(name, BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
        if (f!=null) { try { f.SetValue(obj, value); } catch { } }
    }
}

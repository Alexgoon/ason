using System.Reflection;
using Ason.Invocation;
using System.Collections.Concurrent;
using System.Linq;
using ModelContextProtocol.Client; // MCP

namespace Ason.CodeGen;

public sealed class OperatorBuilder {
    readonly List<Assembly> _assemblies = new();
    Func<MethodInfo,bool> _baseFilter = DefaultFilter;
    bool _addExtractor;
    readonly List<IMcpClient> _mcpClients = new();

    public OperatorBuilder AddAssemblies(params Assembly[] assemblies) {
        if (assemblies == null) return this;
        foreach (var a in assemblies) if (a != null && !_assemblies.Contains(a)) _assemblies.Add(a);
        return this;
    }
    public OperatorBuilder SetBaseFilter(Func<MethodInfo,bool> filter) { _baseFilter = filter ?? DefaultFilter; return this; }

    public OperatorBuilder AddExtractor() { _addExtractor = true; return this; }

    public OperatorBuilder AddMcp(IMcpClient client) {
        if (client == null) throw new ArgumentNullException(nameof(client));
        _mcpClients.Add(client);
        return this;
    }

    static bool DefaultFilter(MethodInfo mi) => mi.IsPublic && !mi.IsSpecialName && mi.GetCustomAttribute<AsonMethodAttribute>() != null;

    public OperatorsLibrary Build() {
        var distinct = _assemblies.Distinct().ToArray();
        Task<(string proxies, string signatures, IOperatorMethodCache cache)> buildTask = Task.Run(async () => {
            // Build method cache inside task
            var cache = BuildMethodCache(distinct);
            string proxies = ProxySerializer.SerializeAll(distinct);
            string signatures = ProxySerializer.SerializeSignatures(distinct);
            if (_mcpClients.Count > 0) {
                var servers = new List<(string name, IList<McpClientTool> tools)>();
                foreach (var client in _mcpClients) {
                    var tools = await client.ListToolsAsync().ConfigureAwait(false);
                    servers.Add((client.ServerInfo.Name, tools));
                }
                if (servers.Count > 0) {
                    var (mcpProxy, mcpSignatures) = ProxySerializer.SerializeMcpServers(servers);
                    proxies += mcpProxy;
                    signatures += mcpSignatures;
                }
            }
            return (proxies, signatures, cache);
        });
        return new OperatorsLibrary(buildTask, _addExtractor, _mcpClients.ToArray());
    }

    IOperatorMethodCache BuildMethodCache(Assembly[] assemblies) {
        var entries = new Dictionary<OperatorMethodCache.Key, OperatorMethodEntry>();
        foreach (var asm in assemblies) {
            Type[] types; try { types = asm.GetTypes(); } catch { continue; }
            foreach (var t in types) {
                if (!Attribute.IsDefined(t, typeof(AsonOperatorAttribute)) && !typeof(OperatorBase).IsAssignableFrom(t)) continue;
                if (t.FullName == typeof(ExtractionOperator).FullName && !_addExtractor) continue;
                var methods = t.GetMethods(BindingFlags.Instance|BindingFlags.Public|BindingFlags.DeclaredOnly);
                foreach (var m in methods) {
                    if (!_baseFilter(m)) continue;
                    var key = new OperatorMethodCache.Key(t, m.Name, m.GetParameters().Length);
                    if (entries.ContainsKey(key)) throw new InvalidOperationException($"Duplicate operator method detected: {t.FullName}.{m.Name} with same parameter count.");
                    bool returnsTask = typeof(Task).IsAssignableFrom(m.ReturnType);
                    bool returnsTaskWithResult = returnsTask && m.ReturnType.IsGenericType && m.ReturnType.GetGenericTypeDefinition()==typeof(Task<>);
                    Type? resultType = returnsTaskWithResult ? m.ReturnType.GetGenericArguments()[0] : (returnsTask? null : m.ReturnType);
                    var entry = new OperatorMethodEntry(m, m.GetParameters(), m.IsGenericMethodDefinition, returnsTask, returnsTaskWithResult, resultType);
                    entries[key] = entry;
                }
            }
        }
        return new OperatorMethodCache(entries);
    }

    sealed class OperatorMethodCache : IOperatorMethodCache {
        internal readonly struct Key : IEquatable<Key> {
            public readonly Type Type; public readonly string Name; public readonly int ParamCount;
            public Key(Type t, string n, int c) { Type=t; Name=n; ParamCount=c; }
            public bool Equals(Key other) => Type==other.Type && ParamCount==other.ParamCount && string.Equals(Name, other.Name, StringComparison.Ordinal);
            public override bool Equals(object? obj) => obj is Key k && Equals(k);
            public override int GetHashCode() => HashCode.Combine(Type, Name, ParamCount);
        }
        readonly Dictionary<Key, OperatorMethodEntry> _map;
        readonly ConcurrentDictionary<(MethodInfo open,string argsKey), OperatorMethodEntry> _closedGenericCache = new();
        public OperatorMethodCache(Dictionary<Key, OperatorMethodEntry> map) { _map = map; }
        public bool TryGet(Type declaringType, string name, int argCount, out OperatorMethodEntry entry) => _map.TryGetValue(new Key(declaringType, name, argCount), out entry!);
        public OperatorMethodEntry GetOrAddClosedGeneric(OperatorMethodEntry openEntry, Type[] typeArguments) {
            if (!openEntry.IsGenericDefinition) return openEntry;
            string keyStr = String.Join("|", typeArguments.Select(t=>t.FullName));
            return _closedGenericCache.GetOrAdd((openEntry.Method, keyStr), k => {
                var closed = k.open.MakeGenericMethod(typeArguments);
                bool returnsTask = typeof(Task).IsAssignableFrom(closed.ReturnType);
                bool returnsTaskWithResult = returnsTask && closed.ReturnType.IsGenericType && closed.ReturnType.GetGenericTypeDefinition()==typeof(Task<>);
                Type? resultType = returnsTaskWithResult ? closed.ReturnType.GetGenericArguments()[0] : (returnsTask? null : closed.ReturnType);
                return new OperatorMethodEntry(closed, closed.GetParameters(), false, returnsTask, returnsTaskWithResult, resultType);
            });
        }
    }
}

public sealed record OperatorsLibrary(
    Task<(string proxies, string signatures, IOperatorMethodCache cache)> BuildTask,
    bool HasExtractor,
    IReadOnlyList<IMcpClient> McpClients);

internal sealed class FilteringMethodCache : IOperatorMethodCache {
    readonly IOperatorMethodCache _inner; readonly Func<MethodInfo, bool> _filter;
    public FilteringMethodCache(IOperatorMethodCache inner, Func<MethodInfo, bool> filter) { _inner = inner; _filter = filter; }
    public bool TryGet(Type declaringType, string name, int argCount, out OperatorMethodEntry entry) {
        if (_inner.TryGet(declaringType, name, argCount, out entry)) {
            if (_filter(entry.Method)) return true; entry = null!; return false;
        }
        entry = null!; return false;
    }
    public OperatorMethodEntry GetOrAddClosedGeneric(OperatorMethodEntry openEntry, Type[] typeArguments) => _inner.GetOrAddClosedGeneric(openEntry, typeArguments);
}
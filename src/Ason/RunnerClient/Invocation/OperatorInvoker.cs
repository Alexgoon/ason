using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Ason.Invocation;

public sealed record OperatorMethodEntry(MethodInfo Method, ParameterInfo[] Parameters, bool IsGenericDefinition, bool ReturnsTask, bool ReturnsTaskWithResult, Type? ResultType);

public interface IOperatorMethodCache {
    bool TryGet(Type declaringType, string name, int argCount, out OperatorMethodEntry entry);
    // For generic methods create/lookup closed form
    OperatorMethodEntry GetOrAddClosedGeneric(OperatorMethodEntry openEntry, Type[] typeArguments);
}

internal sealed class OperatorInvoker : IOperatorInvoker {
    readonly ConcurrentDictionary<string, OperatorBase> _handleToObject;
    readonly IInvocationScheduler _scheduler;
    readonly JsonSerializerOptions _jsonOptions;
    readonly IOperatorMethodCache _methodCache;

    public OperatorInvoker(ConcurrentDictionary<string, OperatorBase> handleToObject, IInvocationScheduler scheduler, JsonSerializerOptions jsonOptions, IOperatorMethodCache methodCache) {
        _handleToObject = handleToObject; _scheduler = scheduler; _jsonOptions = jsonOptions; _methodCache = methodCache;
    }

    public async Task<object?> InvokeAsync(string target, string method, string? handleId, object?[]? args) {
        if (string.IsNullOrEmpty(handleId)) throw new ArgumentNullException(nameof(handleId));
        if (!_handleToObject.TryGetValue(handleId, out var instance) || instance is null) {
            _handleToObject.TryRemove(handleId, out _);
            throw new ObjectDisposedException(handleId);
        }
        var type = instance.GetType();
        var argCount = args?.Length ?? 0;
        if (!_methodCache.TryGet(type, method, argCount, out var entry)) {
            throw new MissingMethodException(type.FullName, method);
        }

        // Generic method inference placeholder (simple heuristic based on argument runtime types)
        if (entry.IsGenericDefinition) {
            var genArgs = entry.Method.GetGenericArguments();
            var inferred = new Type[genArgs.Length];
            for (int i = 0; i < genArgs.Length; i++) inferred[i] = typeof(object); // placeholder inference strategy
            entry = _methodCache.GetOrAddClosedGeneric(entry, inferred);
        }

        var coerced = entry.Method.CoerceMethodArguments(args ?? Array.Empty<object?>(), _jsonOptions);

        async Task<object?> InvokeCoreAsync() {
            if (instance is OperatorBase op) await op.Reload();
            var res = entry.Method.Invoke(instance, coerced);
            return await NormalizeResultAsync(res).ConfigureAwait(false);
        }

        return await _scheduler.InvokeAsync(InvokeCoreAsync);
    }

    static async Task<object?> NormalizeResultAsync(object? invocationResult) {
        if (invocationResult is Task task) {
            await task.ConfigureAwait(false);
            if (task.GetType().IsGenericType) {
                object? value = task.GetType().GetProperty("Result")!.GetValue(task);
                if (value is OperatorBase modelOperator) return modelOperator.Handle;
                return value;
            }
            return null;
        }
        // NEW: convert direct OperatorBase returns to handle so proxy pattern stays uniform (previously only Task<OperatorBase> was handled)
        if (invocationResult is OperatorBase opInstance) return opInstance.Handle;
        return invocationResult;
    }
}

using System.Reflection;
using System.Text.Json;

namespace Ason; 
public static class ArgumentsHelper {
    public static object?[] ExtractArgs(this JsonElement root) {
        if (!root.TryGetProperty("args", out var argsEl) || argsEl.ValueKind != JsonValueKind.Array) return Array.Empty<object?>();
        var list = new List<object?>();
        foreach (var item in argsEl.EnumerateArray()) {
            // Keep as JsonElement for later coercion in CoerceMethodArguments based on parameter types
            list.Add(item.Clone());
        }
        return list.ToArray();
    }
    public static object?[] CoerceMethodArguments(this MethodBase mi, object?[] args, JsonSerializerOptions JsonOptions) {
        var pars = mi.GetParameters();
        if (pars.Length == 0) return Array.Empty<object?>();
        var coerced = new object?[pars.Length];
        for (int i = 0; i < pars.Length; i++) {
            var pt = pars[i].ParameterType;
            object? provided = i < args.Length ? args[i] : null;

            if (provided is JsonElement je) {
                // If target expects object, pass JsonElement as-is
                if (pt == typeof(object)) { coerced[i] = je; continue; }
                try {
                    coerced[i] = je.Deserialize(pt, JsonOptions);
                }
                catch {
                    // fallback generic Object deserialization then convert if simple
                    coerced[i] = je.DeserializeToObject();
                    if (coerced[i] is not null && pt.IsInstanceOfType(coerced[i])) { /* ok */ }
                }
            }
            else {
                // SPECIAL CASE: array-of-POCO shape adaptation (in-process script types yield Submission#X+Type[])
                if (provided is Array arr && pt.IsArray) {
                    var targetElem = pt.GetElementType();
                    var providedElem = provided.GetType().GetElementType();
                    if (targetElem != null && providedElem != null && targetElem != providedElem) {
                        // Attempt fast JSON round-trip for the entire array
                        try {
                            var jsonElem = JsonSerializer.SerializeToElement(provided, JsonOptions);
                            var adaptedArray = jsonElem.Deserialize(pt, JsonOptions);
                            if (adaptedArray != null) { coerced[i] = adaptedArray; continue; }
                        } catch { /* ignore */ }
                        // Fallback: element-wise adaptation
                        try {
                            var len = arr.Length;
                            var newArr = Array.CreateInstance(targetElem, len);
                            for (int idx = 0; idx < len; idx++) {
                                var srcVal = arr.GetValue(idx);
                                if (srcVal == null) { newArr.SetValue(null, idx); continue; }
                                if (targetElem.IsInstanceOfType(srcVal)) { newArr.SetValue(srcVal, idx); continue; }
                                try {
                                    var elemJson = JsonSerializer.SerializeToElement(srcVal, JsonOptions);
                                    var adaptedElem = elemJson.Deserialize(targetElem, JsonOptions);
                                    newArr.SetValue(adaptedElem, idx);
                                } catch { newArr.SetValue(null, idx); }
                            }
                            coerced[i] = newArr; continue;
                        } catch { /* swallow and continue to other adaptation paths */ }
                    }
                }

                // If the provided value is a different POCO type (common in InProcess mode where script-defined
                // types are loaded in a transient Assembly), attempt a JSON round-trip to adapt shape.
                if (provided is not null && !pt.IsInstanceOfType(provided) && ShouldAttemptPocoAdapt(provided.GetType(), pt)) {
                    try {
                        var jsonElem = JsonSerializer.SerializeToElement(provided, JsonOptions);
                        var adapted = jsonElem.Deserialize(pt, JsonOptions);
                        if (adapted != null) { coerced[i] = adapted; continue; }
                    } catch { /* ignore and fallback */ }
                }
                // Best-effort simple conversion
                coerced[i] = ChangeType(provided, pt);
            }
        }
        return coerced;
    }

    static bool ShouldAttemptPocoAdapt(Type sourceType, Type targetType) {
        if (sourceType == targetType) return false;
        if (targetType.IsAssignableFrom(sourceType)) return false;
        // Only attempt for class/record (reference) types that are not known simple types
        return IsPocoLike(sourceType) && IsPocoLike(targetType);
    }

    static bool IsPocoLike(Type t) {
        if (t.IsPrimitive || t.IsEnum) return false;
        if (t == typeof(string) || t == typeof(Guid) || t == typeof(DateTime) || t == typeof(DateTimeOffset) || t == typeof(TimeSpan) || t == typeof(decimal)) return false;
        if (typeof(System.Collections.IEnumerable).IsAssignableFrom(t) && t != typeof(byte[])) return false; // avoid collections here
        return t.IsClass && t != typeof(object);
    }

    static object? ChangeType(object? value, Type targetType) {
        if (value == null) return targetType.IsValueType && Nullable.GetUnderlyingType(targetType) == null ? Activator.CreateInstance(targetType) : null;
        var underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;
        if (underlying.IsInstanceOfType(value)) return value;

        // Handle enums
        if (underlying.IsEnum) {
            try {
                if (value is string s) return Enum.Parse(underlying, s, ignoreCase: true);
                if (IsNumber(value)) return Enum.ToObject(underlying, value);
            } catch { return value; }
            return value;
        }

        // Guid
        if (underlying == typeof(Guid)) {
            if (value is string gs && Guid.TryParse(gs, out var g)) return g;
            return value;
        }
        // Date/Time
        if (underlying == typeof(DateTime)) {
            if (value is string dts && DateTime.TryParse(dts, out var dt)) return dt;
            return value;
        }
        if (underlying == typeof(DateTimeOffset)) {
            if (value is string dots && DateTimeOffset.TryParse(dots, out var dto)) return dto;
            return value;
        }
        if (underlying == typeof(TimeSpan)) {
            if (value is string tss && TimeSpan.TryParse(tss, out var ts)) return ts;
            return value;
        }

        // Primitive / convertible path
        try {
            if (value is IConvertible && underlying != typeof(object)) {
                return Convert.ChangeType(value, underlying);
            }
        } catch { /* swallow and fallback */ }

        return value; // give up, leave original; caller may still fail if truly incompatible
    }

    static bool IsNumber(object o) {
        return o is byte or sbyte or short or ushort or int or uint or long or ulong;
    }
}

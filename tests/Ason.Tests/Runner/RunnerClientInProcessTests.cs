using System;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
using Ason;
using System.Reflection;

namespace Ason.Tests.Runner;

public class RunnerClientInProcessTests {
    [AsonOperator]
    private sealed class SimpleOp : OperatorBase {
        public SimpleOp() { }
        [AsonMethod] public int Add(int a, int b) => a + b;
        [AsonMethod] public async Task<int> AddAsync(int a, int b) { await Task.Yield(); return a + b; }
    }

    private RunnerClient CreateRunner(out SimpleOp op) {
        var root = new RootOperator(new object());
        op = new SimpleOp();
        var handleField = typeof(OperatorBase).GetField("Handle", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy)!;
        handleField.SetValue(op, op.CreateHandle());
        var handleValue = handleField.GetValue(op) as string ?? throw new InvalidOperationException("Handle not set");
        root.OperatorInstances[handleValue] = op;
        return new RunnerClient(root.OperatorInstances, synchronizationContext: null) { Mode = AsonRunner.ExecutionMode.InProcess };
    }


    [Fact]
    public async Task MethodInvoking_Cancellation() {
        var runner = CreateRunner(out var op);
        var handleField = typeof(OperatorBase).GetField("Handle", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy)!;
        var handle = handleField.GetValue(op) as string ?? throw new InvalidOperationException("Handle missing");
        runner.MethodInvoking += (s,e)=> { e.Cancel = true; };
        await Assert.ThrowsAnyAsync<Exception>(async ()=> await runner.InternalInvokeOperatorAsync(op.GetType().Name, nameof(SimpleOp.AddAsync), handle, new object?[]{1,2}));
    }
}

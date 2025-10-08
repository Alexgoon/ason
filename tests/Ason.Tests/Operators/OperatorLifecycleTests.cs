using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Ason;

namespace Ason.Tests.Operators;

public class OperatorLifecycleTests {
    // Simple concrete operator used for testing
    public class TestOp : OperatorBase { }

    [Fact]
    public void Attach_NewOperator_SetsInitializedAndObject() {
        var root = new RootOperator(new object());
        var payload = new object();
        root.AttachChildOperator<TestOp>(payload, id: "A");
        var handle = typeof(TestOp).Name + "A";
        Assert.True(root.OperatorInstances.TryGetValue(handle, out var op));
        Assert.True(op.IsInitialized);
        Assert.Same(payload, op.AttachedObject);
    }

    [Fact]
    public void Reattach_UpdatesAttachedObject_DoesNotDuplicate() {
        var root = new RootOperator(new object());
        var first = new object();
        root.AttachChildOperator<TestOp>(first, id: "A");
        var handle = typeof(TestOp).Name + "A";
        var beforeCount = root.OperatorInstances.Count;
        var second = new object();
        root.AttachChildOperator<TestOp>(second, id: "A"); // reattach
        Assert.Equal(beforeCount, root.OperatorInstances.Count); // no duplication
        var op = root.OperatorInstances[handle];
        Assert.Same(second, op.AttachedObject); // replaced
    }

    [Fact]
    public void Attach_SameTypeDifferentIds_CreatesDistinctOperators() {
        var root = new RootOperator(new object());
        root.AttachChildOperator<TestOp>(new object(), id: "1");
        root.AttachChildOperator<TestOp>(new object(), id: "2");
        Assert.True(root.OperatorInstances.ContainsKey("TestOp1"));
        Assert.True(root.OperatorInstances.ContainsKey("TestOp2"));
        Assert.NotSame(root.OperatorInstances["TestOp1"], root.OperatorInstances["TestOp2"]);
    }

    [Fact]
    public void Detach_SetsIsInitializedFalse() {
        var root = new RootOperator(new object());
        root.AttachChildOperator<TestOp>(new object(), id: "A");
        var handle = "TestOpA";
        var op = root.OperatorInstances[handle];
        Assert.True(op.IsInitialized);
        root.DetachChildOperator<TestOp>("A");
        Assert.False(op.IsInitialized);
        Assert.Null(op.AttachedObject);
    }

    [Fact]
    public async Task Reload_Waits_For_Reattachment() {
        var root = new RootOperator(new object());
        root.AttachChildOperator<TestOp>(new object(), id: "A");
        var handle = "TestOpA";
        var op = (TestOp)root.OperatorInstances[handle];
        root.DetachChildOperator<TestOp>("A");
        Assert.False(op.IsInitialized);

        var newObj = new object();
        var reloadTask = op.Reload(); // internal method accessible via InternalsVisibleTo
        // Simulate UI reattachment on another task
        _ = Task.Run(() => root.AttachChildOperator<TestOp>(newObj, id: "A"));
        await reloadTask; // should complete when reattached
        Assert.True(op.IsInitialized);
        Assert.Same(newObj, op.AttachedObject);
    }
}

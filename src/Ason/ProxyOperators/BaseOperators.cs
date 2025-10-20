using System.Collections.Concurrent;

namespace Ason;

[AsonOperator]
public class OperatorBase {
    Action? OpenViewAction;
    RootOperator? rootOperator;
    protected internal string Handle = string.Empty;
    public OperatorBase? ParentOperator { get; private set; }
    public object? AttachedObject { get; internal set; }

    // Allow descendants to customize how long Reload waits for reattachment
    protected virtual TimeSpan ReloadTimeout => TimeSpan.FromSeconds(40);

    RootOperator RootOperator {
        get {
            if (rootOperator != null)
                return rootOperator;

            if (this is RootOperator directRoot) {
                rootOperator = directRoot;
                return directRoot;
            }

            OperatorBase? currentOperator = this;
            while (currentOperator != null && !(currentOperator is RootOperator)) {
                currentOperator = currentOperator.ParentOperator;
                if (currentOperator == null)
                    throw new InvalidOperationException("Operator chain broken: encountered null ParentOperator before reaching RootOperator.");
            }
            rootOperator = (RootOperator)currentOperator!;
            return rootOperator;
        }
    }

    void Reopen() => OpenViewAction?.Invoke();

    internal async Task Reload() {
        if (!IsInitialized) {
            var taskCompletionSource = new TaskCompletionSource();
            RootOperator.OperatorTaskCompletions[Handle] = taskCompletionSource;
            Reopen();
            await Task.Delay(1).ConfigureAwait(false); // Allow UI thread to process unload

            var timeout = ReloadTimeout;
            var completed = await Task.WhenAny(taskCompletionSource.Task, Task.Delay(timeout)).ConfigureAwait(false);
            if (completed != taskCompletionSource.Task) {
                RootOperator.OperatorTaskCompletions.TryRemove(Handle, out _);
                throw new TimeoutException("Operator loading timeout. Make sure you call RootOperator.AttachChildOperator when the object associated with the operator is loaded (typically when the page is loaded).");
            }
            await taskCompletionSource.Task.ConfigureAwait(false); // Propagate any exception (shouldn't happen here)
        }
        else {
            Reopen();
        }
    }

    internal bool IsInitialized = false;

    protected async Task<TOperator> GetViewOperator<TOperator>(Action? openViewAction = null, string? id = null) where TOperator : OperatorBase, new() {
        string handle = CreateHandle(id, typeof(TOperator));

        if (RootOperator.OperatorInstances.TryGetValue(handle, out OperatorBase? existingOperator)) {
            existingOperator.OpenViewAction = openViewAction;
            await existingOperator.Reload().ConfigureAwait(false);
            return (TOperator)existingOperator;
        }

        var newOperator = CreateNewOperator<TOperator>(handle, openViewAction);
        await newOperator.Reload().ConfigureAwait(false);
        return newOperator;
    }

    internal string CreateHandle(string? id = null, Type? type = null) {
        type ??= GetType();
        return id == null ? type.Name : type.Name + id;
    }

    internal void Attach(object attachedObject) {
        AttachedObject = attachedObject;
        IsInitialized = true;
    }

    internal void Detach() {
        AttachedObject = null;
        IsInitialized = false;
    }

    internal TOperator CreateNewOperator<TOperator>(string handle, Action? openAction = null) where TOperator : OperatorBase, new() {
        var newOperator = new TOperator {
            Handle = handle,
            OpenViewAction = openAction,
            ParentOperator = this
        };
        RootOperator.OperatorInstances[handle] = newOperator;
        return newOperator;
    }
}

// Generic variant providing a strongly typed AttachedObject
public class OperatorBase<TAttached> : OperatorBase {
    public new TAttached? AttachedObject => (TAttached?)base.AttachedObject;
    internal void Attach(TAttached attachedObject) => base.Attach(attachedObject!);
}

public class RootOperator : OperatorBase {
    internal readonly ConcurrentDictionary<string, TaskCompletionSource> OperatorTaskCompletions = new();

    internal readonly ConcurrentDictionary<string, OperatorBase> OperatorInstances = new();

    public RootOperator(object attachedObject) {
        Attach(attachedObject);
        Handle = CreateHandle();
        OperatorInstances[Handle] = this;
    }

    internal void CompleteNavigationTask(OperatorBase childOperator) {
        if (OperatorTaskCompletions.TryRemove(childOperator.Handle, out TaskCompletionSource? tcs)) {
            tcs.SetResult();
        }
    }

    public void AttachChildOperator<TOperator>(object attachedObject, string? id = null) where TOperator : OperatorBase, new() {
        string handle = CreateHandle(id, typeof(TOperator));
        if (OperatorInstances.TryGetValue(handle, out var op)) {
            op.Attach(attachedObject);
            CompleteNavigationTask(op);
        }
        else {
            CreateNewOperator<TOperator>(handle).Attach(attachedObject);
        }
    }

    public void DetachChildOperator<TOperator>(string? id = null) {
        string handle = CreateHandle(id, typeof(TOperator));
        if (OperatorInstances.TryGetValue(handle, out var op)) {
            op.Detach();
        }
    }
}

// Generic RootOperator variant for strongly typed AttachedObject
public class RootOperator<TAttached> : RootOperator {
    public new TAttached AttachedObject => (TAttached)base.AttachedObject!;
    public RootOperator(TAttached attachedObject) : base(attachedObject!) { }
}

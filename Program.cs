// This takes the address of, gets the size of, or declares a pointer to a managed type
#pragma warning disable CS8500

using System.Runtime;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Internal.Runtime;

static unsafe partial class Program
{
    static void Main()
    {
        Console.WriteLine($"My PID: {Environment.ProcessId}");
        Console.WriteLine("Press enter to continue");
        Console.ReadLine();

        nint nativeObj = CreateAndMarshalObject();

        // Trigger a collection while the ref count is still above 0.
        GC.Collect();

        // We pass ownership to the the unmanaged side. It will take care of releasing.
        MyUnmanagedFunction(nativeObj);
        nativeObj = 0;

        // Clean up the ref-counted handle
        for (int i = 0; i < 10; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
    }

    // no inlining to make sure the lifetime of `obj` ends at the end of this function.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static nint CreateAndMarshalObject()
    {
        var obj = new MyInteropObject(42);
        return SimpleInteropSystem.GetNativeObject(obj);
    }

    [LibraryImport("*")]
    private static partial void MyUnmanagedFunction(nint nativeObj);
}

class MyInteropObject
{
    [UnmanagedCallersOnly(EntryPoint = "GetObjectInfo")]
    static int GetObjectInfo(nint nativeObj)
    {
        MyInteropObject obj = (MyInteropObject)SimpleInteropSystem.GetManagedObject(nativeObj)!;
        return obj._info;
    }

    private int _info;

    public MyInteropObject(int info)
    {
        _info = info;
    }
}

unsafe struct ManagedObjectWrapper
{
    internal GCHandle _holderHandle;
    internal int _refCount;

    public bool IsRooted => _refCount != 0;

    [UnmanagedCallersOnly(EntryPoint = "MyAddRef")]
    static int AddRef(nint nativeObj)
    {
        ManagedObjectWrapper* wrapper = (ManagedObjectWrapper*)nativeObj;
        return Interlocked.Increment(ref wrapper->_refCount);
    }

    [UnmanagedCallersOnly(EntryPoint = "MyRelease")]
    static int Release(nint nativeObj)
    {
        ManagedObjectWrapper* wrapper = (ManagedObjectWrapper*)nativeObj;
        return Interlocked.Decrement(ref wrapper->_refCount);
    }
}

unsafe class ManagedObjectWrapperHolder
{
    static ManagedObjectWrapperHolder()
    {
        delegate* unmanaged<IntPtr, bool> callback = &IsRootedCallback;
        RuntimeImports.RhRegisterRefCountedHandleCallback((nint)callback, MethodTable.Of<ManagedObjectWrapperHolder>());
    }

    [UnmanagedCallersOnly]
    static bool IsRootedCallback(IntPtr pObj)
    {
        // We are paused in the GC, so this is safe.
        ManagedObjectWrapperHolder* holder = (ManagedObjectWrapperHolder*)&pObj;
        return holder->_wrapper->IsRooted;
    }

    internal ManagedObjectWrapper* _wrapper;
    internal readonly object _wrappedObject;

    public ManagedObjectWrapperHolder(ManagedObjectWrapper* wrapper, object wrappedObject)
    {
        _wrapper = wrapper;
        _wrappedObject = wrappedObject;
        _wrapper->_holderHandle = GCHandle.FromIntPtr(RuntimeImports.RhHandleAllocRefCounted(this));
    }

    ~ManagedObjectWrapperHolder()
    {
        _wrapper->_holderHandle.Free();
        NativeMemory.Free(_wrapper);
    }
}

public static unsafe class SimpleInteropSystem
{
    private static readonly ConditionalWeakTable<object, ManagedObjectWrapperHolder> s_objects = new();

    public static nint GetNativeObject(object obj)
    {
        ManagedObjectWrapperHolder holder = s_objects.GetValue(obj, static key => {
            ManagedObjectWrapper* wrapper = (ManagedObjectWrapper*)NativeMemory.AllocZeroed((nuint)sizeof(ManagedObjectWrapper));
            wrapper->_refCount = 1;
            return new ManagedObjectWrapperHolder(wrapper, key);
        });
        return (nint)holder._wrapper;
    }

    public static object? GetManagedObject(nint nativeObj)
    {
        if (nativeObj == 0)
            throw new ArgumentNullException();
        ManagedObjectWrapper* wrapper = (ManagedObjectWrapper*)nativeObj;
        return ((ManagedObjectWrapperHolder)wrapper->_holderHandle.Target!)._wrappedObject;
    }
}

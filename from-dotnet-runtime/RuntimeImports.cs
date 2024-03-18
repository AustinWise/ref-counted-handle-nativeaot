// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Internal.Runtime;

namespace System.Runtime;

// These call into the NativeAOT C++ runtime.
static class RuntimeImports
{
    private const string RuntimeLibrary = "*";
    [MethodImpl(MethodImplOptions.InternalCall)]
    [RuntimeImport(RuntimeLibrary, "RhRegisterRefCountedHandleCallback")]
    internal static extern unsafe bool RhRegisterRefCountedHandleCallback(IntPtr pCalloutMethod, MethodTable* pTypeFilter);

    [MethodImpl(MethodImplOptions.InternalCall)]
    [RuntimeImport(RuntimeLibrary, "RhpHandleAlloc")]
    private static extern IntPtr RhpHandleAlloc(object value, GCHandleType type);

    internal static IntPtr RhHandleAlloc(object value, GCHandleType type)
    {
        IntPtr h = RhpHandleAlloc(value, type);
        if (h == IntPtr.Zero)
            throw new OutOfMemoryException();
        return h;
    }

    internal static IntPtr RhHandleAllocRefCounted(object value)
    {
        const int HNDTYPE_REFCOUNTED = 5;
        return RhHandleAlloc(value, (GCHandleType)HNDTYPE_REFCOUNTED);
    }
}

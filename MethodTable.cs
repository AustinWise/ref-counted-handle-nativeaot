// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Internal.Runtime;

[StructLayout(LayoutKind.Sequential)]
unsafe struct MethodTable
{
    [Intrinsic]
    internal static extern MethodTable* Of<T>();
}

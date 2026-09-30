#if !NET5_0_OR_GREATER
namespace System.Runtime.CompilerServices
{
    /// <summary>Lets Unity's BCL compile C# 9 init-only setters (used by the Survivor defs).</summary>
    internal static class IsExternalInit
    {
    }
}
#endif

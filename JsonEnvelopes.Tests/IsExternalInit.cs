#if NETFRAMEWORK
namespace System.Runtime.CompilerServices;

/// <summary>
/// Lets the compiler emit <c>init</c> accessors and positional records on .NET Framework,
/// whose base class library does not define this type.
/// </summary>
internal static class IsExternalInit
{
}
#endif

// Polyfill required for C# 9+ init-only setters on .NET 4.x
namespace System.Runtime.CompilerServices
{
    internal sealed class IsExternalInit { }
}

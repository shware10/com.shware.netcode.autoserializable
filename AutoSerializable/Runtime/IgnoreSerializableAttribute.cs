using System;

namespace Shware.Netcode
{
    /// <summary>
    /// Prevents a field from being included in auto-generated serialization.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
    public sealed class IgnoreSerializableAttribute : Attribute
    {}
}
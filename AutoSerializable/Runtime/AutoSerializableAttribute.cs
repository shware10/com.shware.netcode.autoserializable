using System;

namespace Shware.Netcode
{
    /// <summary>
    /// Marks a struct to automatically generate INetworkSerializable
    /// (and optionally IEquatable) via source generator.
    /// </summary>
    [AttributeUsage(AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
    public sealed class AutoSerializableAttribute : Attribute
    {
        /// <summary>
        /// Whether to generate IEquatable<T> implementation.
        /// Default: true
        /// </summary>
        public bool GenerateEquatable { get; set; } = true;
    }
}
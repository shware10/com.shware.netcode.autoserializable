# AutoSerializableGenerator

Compile-time enforced payload serialization for **Unity Netcode for GameObjects (NGO)**.

A Roslyn Incremental Source Generator that automatically generates safe and validated
`INetworkSerializable`/`IEquatable<T>` implementations for **network payload / command / DTO structs**.

---

## What is this?

`AutoSerializableGenerator` eliminates the need to manually implement
`INetworkSerializable` in Unity Netcode projects.

It is designed specifically for **pure data payloads** used in:
- RPC payloads
- Command / event buses
- Network DTOs

 This generator enforces NGO best practices **at compile time**, not runtime.

---

## Key Features

- Automatic `INetworkSerializable` generation
- Array serialization with element-type validation
- Compile-time diagnostics for invalid fields
- Optional `IEquatable<T>` generation
- Safe handling of empty payloads
- Explicit exclusion of invalid NGO types (e.g. `NetworkVariable<T>`)

---

## Design Philosophy

This generator is **intentionally opinionated**.

It exists to serialize **data only**, not runtime state or behavior.

### Explicitly NOT supported
- `NetworkVariable<T>`
- `NetworkObject`
- `NetworkBehaviour`
- Reference types (`class`, `string`)
- Unity runtime objects
- Circular references

If you need runtime synchronization, use NGO’s built-in systems.  
If you need persistence, use a separate SaveData / JSON model.

---

## Supported Field Types

### Value Types
- Primitive types (`int`, `float`, `bool`, etc.)
- `enum`
- Unity value structs:
  - `Vector2`, `Vector3`, `Vector4`
  - `Vector2Int`, `Vector3Int`
  - `Quaternion`
  - `Color`, `Color32`
  - `Ray`, `Ray2D`

### Special Types
- `FixedStringXXBytes`
- Custom structs implementing `INetworkSerializable`

### Arrays
- Arrays of all supported value types

---

## Nested Struct Support

### Supported

Custom nested structs are supported **only if they explicitly implement**
`INetworkSerializable`.

```csharp
public struct Stats : INetworkSerializable
{
    public int Strength;
    public int Dexterity;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer)
        where T : IReaderWriter
    {
        serializer.SerializeValue(ref Strength);
        serializer.SerializeValue(ref Dexterity);
    }
}

[AutoSerializable]
public partial struct PlayerPayload
{
    public int PlayerId;
    public Stats Stats; // ✅ Supported
}

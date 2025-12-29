# AutoSerializableGenerator

Compile-time enforced payload serialization for **Unity Netcode for GameObjects (NGO)**.

A Roslyn Incremental Source Generator that automatically generates safe and validated
`INetworkSerializable`/`IEquatable<T>` implementations for **network payload / command / DTO structs**.

---

## Why AutoSerializable? Comparison: Manual vs AutoSerializable

The following examples show **the exact same payload**, implemented:

1. ❌ Manually (without AutoSerializable)
2. ✅ Automatically (with AutoSerializableGenerator)

This highlights the difference in **boilerplate, safety, and maintainability**.

---

### ❌ Without AutoSerializable (Manual Implementation)

```csharp
public struct PlayerStatePayload : INetworkSerializable, IEquatable<PlayerStatePayload>
{
    // Primitive
    public int PlayerId;
    public bool IsAlive;

    // Unity value types
    public Vector3 Position;
    public Quaternion Rotation;

    // FixedString
    public FixedString64Bytes PlayerName;

    // Array
    public int[] EquippedItemIds;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer)
        where T : IReaderWriter
    {
        serializer.SerializeValue(ref PlayerId);
        serializer.SerializeValue(ref IsAlive);
        serializer.SerializeValue(ref Position);
        serializer.SerializeValue(ref Rotation);
        serializer.SerializeValue(ref PlayerName);

        int count = EquippedItemIds?.Length ?? 0;
        serializer.SerializeValue(ref count);

        if (serializer.IsReader)
            EquippedItemIds = new int[count];

        for (int i = 0; i < count; i++)
            serializer.SerializeValue(ref EquippedItemIds[i]);
    }

    public bool Equals(PlayerStatePayload other)
    {
        if (PlayerId != other.PlayerId) return false;
        if (IsAlive != other.IsAlive) return false;
        if (Position != other.Position) return false;
        if (Rotation != other.Rotation) return false;
        if (PlayerName != other.PlayerName) return false;

        if ((EquippedItemIds == null) != (other.EquippedItemIds == null))
            return false;

        if (EquippedItemIds != null)
        {
            if (EquippedItemIds.Length != other.EquippedItemIds.Length)
                return false;

            for (int i = 0; i < EquippedItemIds.Length; i++)
                if (EquippedItemIds[i] != other.EquippedItemIds[i])
                    return false;
        }

        return true;
    }

    public override int GetHashCode()
    {
        var hash = new System.HashCode();
        hash.Add(PlayerId);
        hash.Add(IsAlive);
        hash.Add(Position);
        hash.Add(Rotation);
        hash.Add(PlayerName);

        if (EquippedItemIds != null)
        {
            foreach (var id in EquippedItemIds)
                hash.Add(id);
        }

        return hash.ToHashCode();
    }
}
```
### ✅With AutoSerializable (Automatically Implementation)

```csharp
[AutoSerializable(GenerateEquatable = true)]
public partial struct PlayerStatePayload
{
    // Primitive
    public int PlayerId;
    public bool IsAlive;

    // Unity value types
    public Vector3 Position;
    public Quaternion Rotation;

    // FixedString
    public FixedString64Bytes PlayerName;

    // Array
    public int[] EquippedItemIds;
}
```

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
    public Stats Stats; // Supported
}
```
---



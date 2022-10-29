using System;
using System.Runtime.InteropServices;
using Dalamud.Game.ClientState.Objects.Enums;
using Newtonsoft.Json.Linq;
using Penumbra.String;

namespace Penumbra.GameData.Actors;

[StructLayout( LayoutKind.Explicit )]
public readonly struct ActorIdentifier : IEquatable< ActorIdentifier >
{
    public static ActorManager? Manager;

    public static readonly ActorIdentifier Invalid = new(IdentifierType.Invalid, 0, 0, 0, ByteString.Empty);

    // @formatter:off
    [FieldOffset( 0 )] public readonly IdentifierType Type;       // All
    [FieldOffset( 1 )] public readonly ObjectKind     Kind;       // Npc, Owned
    [FieldOffset( 2 )] public readonly ushort         HomeWorld;  // Player, Owned
    [FieldOffset( 2 )] public readonly ushort         Index;      // NPC
    [FieldOffset( 2 )] public readonly SpecialActor   Special;    // Special
    [FieldOffset( 4 )] public readonly uint           DataId;     // Owned, NPC
    [FieldOffset( 8 )] public readonly ByteString     PlayerName; // Player, Owned
    // @formatter:on

    public ActorIdentifier CreatePermanent()
        => new(Type, Kind, Index, DataId, PlayerName.Clone());

    public bool Equals( ActorIdentifier other )
    {
        if( Type != other.Type )
        {
            return false;
        }

        return Type switch
        {
            IdentifierType.Player  => HomeWorld == other.HomeWorld && PlayerName.EqualsCi( other.PlayerName ),
            IdentifierType.Owned   => HomeWorld == other.HomeWorld && PlayerName.EqualsCi( other.PlayerName ) && Manager.DataIdEquals( this, other ),
            IdentifierType.Special => Special == other.Special,
            IdentifierType.Npc     => Index == other.Index && DataId == other.DataId && Manager.DataIdEquals( this, other ),
            _                      => false,
        };
    }

    public override bool Equals( object? obj )
        => obj is ActorIdentifier other && Equals( other );

    public bool IsValid
        => Type != IdentifierType.Invalid;

    public override string ToString()
        => Manager?.ToString( this )
         ?? Type switch
            {
                IdentifierType.Player  => $"{PlayerName} ({HomeWorld})",
                IdentifierType.Owned   => $"{PlayerName}s {Kind} {DataId} ({HomeWorld})",
                IdentifierType.Special => ActorManager.ToName( Special ),
                IdentifierType.Npc =>
                    Index == ushort.MaxValue
                        ? $"{Kind} #{DataId}"
                        : $"{Kind} #{DataId} at {Index}",
                _ => "Invalid",
            };

    public override int GetHashCode()
        => Type switch
        {
            IdentifierType.Player  => HashCode.Combine( IdentifierType.Player, PlayerName, HomeWorld ),
            IdentifierType.Owned   => HashCode.Combine( IdentifierType.Owned, Kind, PlayerName, HomeWorld, DataId ),
            IdentifierType.Special => HashCode.Combine( IdentifierType.Special, Special ),
            IdentifierType.Npc     => HashCode.Combine( IdentifierType.Npc, Kind, Index, DataId ),
            _                      => 0,
        };

    internal ActorIdentifier( IdentifierType type, ObjectKind kind, ushort index, uint data, ByteString playerName )
    {
        Type       = type;
        Kind       = kind;
        Special    = ( SpecialActor )index;
        HomeWorld  = Index = index;
        DataId     = data;
        PlayerName = playerName;
    }


    public JObject ToJson()
    {
        var ret = new JObject { { nameof( Type ), Type.ToString() } };
        switch( Type )
        {
            case IdentifierType.Player:
                ret.Add( nameof( PlayerName ), PlayerName.ToString() );
                ret.Add( nameof( HomeWorld ), HomeWorld );
                return ret;
            case IdentifierType.Owned:
                ret.Add( nameof( PlayerName ), PlayerName.ToString() );
                ret.Add( nameof( HomeWorld ), HomeWorld );
                ret.Add( nameof( Kind ), Kind.ToString() );
                ret.Add( nameof( DataId ), DataId );
                return ret;
            case IdentifierType.Special:
                ret.Add( nameof( Special ), Special.ToString() );
                return ret;
            case IdentifierType.Npc:
                ret.Add( nameof( Kind ), Kind.ToString() );
                ret.Add( nameof( Index ), Index );
                ret.Add( nameof( DataId ), DataId );
                return ret;
        }

        return ret;
    }
}

public static class ActorManagerExtensions
{
    public static bool DataIdEquals( this ActorManager? manager, ActorIdentifier lhs, ActorIdentifier rhs )
    {
        if( lhs.Kind != rhs.Kind )
        {
            return false;
        }

        if( lhs.DataId == rhs.DataId )
        {
            return true;
        }

        if( manager == null )
        {
            return lhs.Kind == rhs.Kind && lhs.DataId == rhs.DataId || lhs.DataId == uint.MaxValue || rhs.DataId == uint.MaxValue;
        }

        return lhs.Kind switch
        {
            ObjectKind.MountType => manager.Mounts.TryGetValue( lhs.DataId, out var lhsName )
             && manager.Mounts.TryGetValue( rhs.DataId, out var rhsName )
             && lhsName.Equals( rhsName, StringComparison.OrdinalIgnoreCase ),
            ObjectKind.Companion => manager.Companions.TryGetValue( lhs.DataId, out var lhsName )
             && manager.Companions.TryGetValue( rhs.DataId, out var rhsName )
             && lhsName.Equals( rhsName, StringComparison.OrdinalIgnoreCase ),
            ObjectKind.BattleNpc => manager.BNpcs.TryGetValue( lhs.DataId, out var lhsName )
             && manager.BNpcs.TryGetValue( rhs.DataId, out var rhsName )
             && lhsName.Equals( rhsName, StringComparison.OrdinalIgnoreCase ),
            ObjectKind.EventNpc => manager.ENpcs.TryGetValue( lhs.DataId, out var lhsName )
             && manager.ENpcs.TryGetValue( rhs.DataId, out var rhsName )
             && lhsName.Equals( rhsName, StringComparison.OrdinalIgnoreCase ),
            _ => false,
        };
    }
}
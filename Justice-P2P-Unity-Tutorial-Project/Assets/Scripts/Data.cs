// Copyright (c) 2022 AccelByte Inc. All Rights Reserved.
// This is licensed software from AccelByte Inc, for limitations
// and restrictions contact your company contract manager.

using System;
using Unity.Collections;
using Unity.Netcode;

/// <summary>
/// Struct to store player's information data
/// </summary>
[Serializable]
public struct PlayerInfo : INetworkSerializable, IEquatable<PlayerInfo>
{
    public ulong clientId;
    public FixedString64Bytes userId;
    public FixedString64Bytes displayName;
    public bool isReady;

    public PlayerInfo(ulong playerClientId, FixedString64Bytes playerUserId, FixedString64Bytes playerDisplayName, bool playerIsReady)
    {
        clientId = playerClientId;
        userId = playerUserId;
        displayName = playerDisplayName;
        isReady = playerIsReady;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref clientId);
        serializer.SerializeValue(ref userId);
        serializer.SerializeValue(ref displayName);
        serializer.SerializeValue(ref isReady);
    }

    public bool Equals(PlayerInfo other)
    {
        return clientId == other.clientId && userId == other.userId && displayName == other.displayName && isReady == other.isReady;
    }
}
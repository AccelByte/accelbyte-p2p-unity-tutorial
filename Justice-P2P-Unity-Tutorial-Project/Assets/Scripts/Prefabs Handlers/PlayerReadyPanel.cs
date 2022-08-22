// Copyright (c) 2022 AccelByte Inc. All Rights Reserved.
// This is licensed software from AccelByte Inc, for limitations
// and restrictions contact your company contract manager.

using UnityEngine;
using UnityEngine.UI;

public class PlayerReadyPanel : MonoBehaviour
{
    [SerializeField]
    private HorizontalLayoutGroup playerReadyHorizLayoutGroup;

    [SerializeField]
    private Text displayNameText;
    [SerializeField]
    private Toggle readyConsentToggle;

    /// <summary>
    /// Update player data to the Player Name Panel's UI
    /// </summary>
    /// <param name="playerInfo">Player Info Data</param>
    public void UpdatePlayerInfo(PlayerInfo playerInfo)
    {
        displayNameText.text = playerInfo.displayName.ToString();
        readyConsentToggle.isOn = playerInfo.isReady;
    }

    /// <summary>
    /// Reverse Arrangement for Player Name Panel
    /// </summary>
    /// <param name="isReverse">reverse arrangement toggle value</param>
    public void SetReverseArrangementPanel(bool isReverse)
    {
        playerReadyHorizLayoutGroup.reverseArrangement = isReverse;
    }
}

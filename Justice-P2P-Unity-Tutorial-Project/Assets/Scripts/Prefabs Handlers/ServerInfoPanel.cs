// Copyright (c) 2022 AccelByte Inc. All Rights Reserved.
// This is licensed software from AccelByte Inc, for limitations
// and restrictions contact your company contract manager.

using AccelByte.Models;
using UnityEngine;
using UnityEngine.UI;

public class ServerInfoPanel : MonoBehaviour
{
    [SerializeField]
    private Text sessionNameText;
    [SerializeField]
    private Text roomAccessibilityText;
    [SerializeField]
    private Text gameModeText;
    [SerializeField]
    private Text serverCapacityText;
    [SerializeField]
    private Button joinButton;

    private string hostUserId;
    private string roomAccessibility;

    /// <summary>
    /// Update session data to the Server Info Panel's UI
    /// </summary>
    /// <param name="sessionData">data of the room session</param>
    public void Create(SessionBrowserData sessionData)
    {
        Debug.Log("[ServerInfo] Updating Server Info data...");

        hostUserId = sessionData.user_id;
        roomAccessibility = sessionData.game_session_setting.settings.GetValue("room_accessibility").ToString();

        // update data to Text UIs
        sessionNameText.text = sessionData.game_session_setting.settings.GetValue("session_name").ToString();
        gameModeText.text = sessionData.game_session_setting.mode;
        roomAccessibilityText.text = roomAccessibility;

        int currentPlayers = sessionData.players.Length;
        int maxPlayers = sessionData.game_session_setting.max_player;
        string capacity = currentPlayers.ToString() + "/" + maxPlayers.ToString();
        serverCapacityText.text = capacity;

        // add listener to button
        joinButton.onClick.AddListener(() => 
        {
            CustomGamesHandler.Instance.CheckSessionPassword(hostUserId, sessionData.session_id, roomAccessibility);
        });

        // disable button if room session is full
        if (currentPlayers == maxPlayers)
        {
            joinButton.interactable = false;
        }
        else
        {
            joinButton.interactable = true;
        }
    }
}

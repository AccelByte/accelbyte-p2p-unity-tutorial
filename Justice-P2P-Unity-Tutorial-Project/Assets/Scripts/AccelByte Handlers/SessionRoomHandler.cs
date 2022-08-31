// Copyright (c) 2022 AccelByte Inc. All Rights Reserved.
// This is licensed software from AccelByte Inc, for limitations
// and restrictions contact your company contract manager.

using AccelByte.Api;
using AccelByte.Core;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class SessionRoomHandler : NetworkBehaviour
{
    #region UI Binding
    public GameObject SessionRoomWindow;

    [SerializeField]
    private Text serverNameText;
    [SerializeField]
    private GameObject playerReadyPrefab;

    [SerializeField]
    private Transform teamAPanel;
    [SerializeField]
    private Transform teamBPanel;

    [SerializeField]
    private Button startButton;
    [SerializeField]
    private Button readyButton;
    [SerializeField]
    private Button leaveButton;
    #endregion

    private ApiClient apiClient;
    private SessionBrowser sessionBrowser;

    [Header("Network Related")]
    [SerializeField]
    private AccelByteNetworkTransportManager transportManager;

    private string currentSessionId;
    private bool isInitialized = false;
    private static bool isEmptySessionId = false;
    private const int maxPlayer = 2;

    private NetworkList<PlayerInfo> playerInfoList;
    private NetworkVariable<bool> isGameStarted;

    // TEMPORARY OBJECTS (SOON TO BE DELETED)
    [Header("TEMPORARY (soon to be deleted)")]
    public GameObject GameplayWindow;
    public Button exitGameplayButton;
    // END OF TEMPORARY OBJECTS

    private void Start()
    {
        // configure API Client for player
        apiClient = MultiRegistry.GetApiClient();
        sessionBrowser = apiClient.GetApi<SessionBrowser, SessionBrowserApi>();

        playerInfoList = new NetworkList<PlayerInfo>();
        isGameStarted = new NetworkVariable<bool>();
    }

    private void FixedUpdate()
    {
        if (isEmptySessionId && currentSessionId != transportManager.GetHostedSessionID())
        {
            isEmptySessionId = false;

            // set current session id
            currentSessionId = transportManager.GetHostedSessionID();
            GetGameSession();
        }
    }

    #region NetworkManager Override Functions
    public override void OnNetworkSpawn()
    {
        // if received a maessage with MessageName = "Register", register player to session
        NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("Register", (senderId, messagePayload) =>
        {
            string receivedMessageContent;
            messagePayload.ReadValueSafe(out receivedMessageContent);
            Debug.Log($"[ReadyMatch] Received message data from {senderId} || {receivedMessageContent} || {currentSessionId} ||");

            if (receivedMessageContent.Split(':').Length > 1)
            {
                string senderUserId = receivedMessageContent.Split(':')[0];
                string senderDisplayName = receivedMessageContent.Split(':')[1];

                playerInfoList.Add(new PlayerInfo(senderId, senderUserId, senderDisplayName, false));
                RegisterPlayerToSession(currentSessionId, senderUserId);
            }
        });

        if (NetworkManager.Singleton.IsClient)
        {
            playerInfoList.OnListChanged += OnPlayerInfoListChanged;
            isGameStarted.OnValueChanged += OnIsGameStartedValueChanged;
        }
        if (NetworkManager.Singleton.IsServer)
        {
            isGameStarted.Value = false;

            NetworkManager.Singleton.OnServerStarted += OnServerStarted;
        }
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
    }

    public override void OnNetworkDespawn()
    {
        NetworkManager.Singleton.CustomMessagingManager.UnregisterNamedMessageHandler("Register");

        playerInfoList.OnListChanged -= OnPlayerInfoListChanged;
        isGameStarted.OnValueChanged -= OnIsGameStartedValueChanged;

        NetworkManager.Singleton.OnServerStarted -= OnServerStarted;
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
    }
    #endregion

    /// <summary>
    /// Setup ReadyMatchPanel's UI and state
    /// </summary>
    public void Setup()
    {
        SessionRoomWindow.SetActive(true);
        if (NetworkManager.Singleton.IsHost)
        {
            startButton.gameObject.SetActive(true);
            exitGameplayButton.gameObject.SetActive(true);
        }
        else
        {
            startButton.gameObject.SetActive(false);
            exitGameplayButton.gameObject.SetActive(false);
        }

        readyButton.interactable = true;

        // reset and display the session name
        serverNameText.text = "";

        if (NetworkManager.Singleton.IsHost)
        {
            currentSessionId = transportManager.GetHostedSessionID();
        }
        GetGameSession();

        // Setup UI listeners
        if (!isInitialized)
        {
            isInitialized = true;

            startButton.onClick.AddListener(() =>
            {
                StartGameServerRpc();
            });
            readyButton.onClick.AddListener(() =>
            {
                readyButton.interactable = false;
                ToggleReadyServerRpc();
            });
            leaveButton.onClick.AddListener(() => LeaveSession());

            // TEMPORARY SETUP
            exitGameplayButton.onClick.AddListener(() =>
            {
                isGameStarted.Value = false;
            });
            // END OF TEMPORARY SETUP
        }
    }

    #region P2P Functionalities
    /// <summary>
    /// Leave from the current session
    /// </summary>
    private void LeaveSession()
    {
        Debug.Log($"[SessionRoom] Leaving the current session, disconnect from host.. {transportManager.GetHostedSessionID()} ||");
        NetworkManager.Singleton.Shutdown();

        // Reset session data and clear player info list
        playerInfoList.Clear();
        isGameStarted.Value = false;
        Debug.Log($"[SessionRoom] Clearing.. {playerInfoList.Count} || {isGameStarted.Value} ||");

        // change view to CustomGamesPanel
        SessionRoomWindow.SetActive(false);
        GetComponent<CustomGamesHandler>().Setup();
    }

    /// <summary>
    /// Add specific player data to selected active session
    /// </summary>
    /// <param name="sessionId">session id of the selected session</param>
    /// <param name="userId">user id of the targeted player</param>
    public void RegisterPlayerToSession(string sessionId, string userId)
    {
        sessionBrowser.RegisterPlayer(sessionId, userId.ToString(), false, result =>
        {
            if (!result.IsError)
            {
                Debug.Log($"[SessionRoom] Register player {userId} to session success");
            }
            else
            {
                Debug.Log($"[SessionRoom] Failed to register player to session, Error: {result.Error.Code}, Description: {result.Error.Message}");
            }
        });
    }

    /// <summary>
    /// Remove specific player data from selected active session
    /// </summary>
    /// <param name="sessionId">session id of the selected session</param>
    /// <param name="userId">user id of the targeted player</param>
    private void UnregisterPlayerFromSession(string sessionId, string userId)
    {
        sessionBrowser.UnregisterPlayer(sessionId, userId, result =>
        {
            if (!result.IsError)
            {
                Debug.Log($"[ReadyMatch] Unregister player {userId} to session success");

            }
            else
            {
                Debug.Log($"[ReaadyMatch] Failed to unregister player from session, Error: {result.Error.Code}, Description: {result.Error.Message}");
            }
        });
    }

    /// <summary>
    /// Get current session data and update them to UI
    /// </summary>
    /// <param name="sessionId"></param>
    private void GetGameSession()
    {
        // Avoid sending empty session id
        if (string.IsNullOrEmpty(currentSessionId))
        {
            isEmptySessionId = true;
            return;
        }

        sessionBrowser.GetGameSession(currentSessionId, result => 
        {
            if (!result.IsError)
            {
                Debug.Log($"[SessionRoom] Get game session success ||");

                // update server name
                serverNameText.text = result.Value.game_session_setting.settings.GetValue("session_name").ToString();
            }
            else
            {
                Debug.Log($"[SessionRoom] Get game session failed, Error: {result.Error.Code}, Description: {result.Error.Message}");
                isEmptySessionId = true;
            }
        });
    }

    #endregion

    /// <summary>
    /// Set current session id
    /// </summary>
    /// <param name="sessionId"></param>
    public void SetSessionId(string sessionId)
    {
        Debug.Log($"[SessionRoom] Setting session id..");
        currentSessionId = sessionId;
    }

    #region ServerRPC Functions
    /// <summary>
    /// Update player list when received player's ready consent
    /// </summary>
    /// <param name="serverRpcParams"></param>
    [ServerRpc(RequireOwnership = false)]
    private void ToggleReadyServerRpc(ServerRpcParams serverRpcParams = default)
    {
        for (int i = 0; i < playerInfoList.Count; i++)
        {
            if (playerInfoList[i].clientId == serverRpcParams.Receive.SenderClientId)
            {
                playerInfoList[i] = new PlayerInfo(
                    playerInfoList[i].clientId,
                    playerInfoList[i].userId,
                    playerInfoList[i].displayName,
                    !playerInfoList[i].isReady
                );
            }
        }
    }

    /// <summary>
    /// Start game by changing to the game scene
    /// </summary>
    /// <param name="serverRpcParams"></param>
    [ServerRpc(RequireOwnership = false)]
    private void StartGameServerRpc(ServerRpcParams serverRpcParams = default)
    {
        // check if the LocalClient has permission access to write NetworkVariable
        if (isGameStarted.CanClientWrite(NetworkManager.LocalClientId))
        {
            isGameStarted.Value = true;
        }
        
        // Use this to move to the Gameplay Scene
        //NetworkManager.Singleton.SceneManager.LoadScene("WatchGame", LoadSceneMode.Single);

        // Reset ready state for all player
        for (int i = 0; i < playerInfoList.Count; i++)
        {
            playerInfoList[i] = new PlayerInfo(
                playerInfoList[i].clientId,
                playerInfoList[i].userId,
                playerInfoList[i].displayName,
                false
            );
        }
    }
    #endregion

    #region Callback functions
    /// <summary>
    /// Callback on the playerInfoList value update
    /// </summary>
    /// <param name="playerInfoEvent">network event of the playerInfoList</param>
    public void OnPlayerInfoListChanged(NetworkListEvent<PlayerInfo> playerInfoEvent)
    {
        Debug.Log($"[SessionRoom] Player List changed! {playerInfoList.Count}");

        LoopThroughTransformAndDestroy(teamAPanel);
        LoopThroughTransformAndDestroy(teamBPanel);

        // current display list only for 1vs1 mode
        for (int i = 0; i < maxPlayer; i++)
        {
            if (playerInfoList.Count > i)
            {
                PlayerReadyPanel playerPanel = null;
                if (i == 0)
                {
                    playerPanel = Instantiate(playerReadyPrefab, teamAPanel).GetComponent<PlayerReadyPanel>();
                    playerPanel.SetReverseArrangementPanel(false);
                }
                else
                {
                    playerPanel = Instantiate(playerReadyPrefab, teamBPanel).GetComponent<PlayerReadyPanel>();
                    playerPanel.SetReverseArrangementPanel(true);
                }
                playerPanel.UpdatePlayerInfo(playerInfoList[i]);
            }
        }
    }

    /// <summary>
    /// Callback on isGameStarted value update
    /// </summary>
    /// <param name="oldValue">old value of isGameStarted</param>
    /// <param name="newValue">updated value of isGameStarted</param>
    public void OnIsGameStartedValueChanged(bool oldValue, bool newValue)
    {
        if (newValue)
        {
            // move to next panel
            SessionRoomWindow.SetActive(false);
            GameplayWindow.SetActive(true);
        }
        else
        {
            // move to next panel
            GameplayWindow.SetActive(false);
            Setup();
        }
    }

    /// <summary>
    /// Callback on the server started
    /// </summary>
    public void OnServerStarted()
    {
        Debug.Log($"[SessionRoom] OnServerStarted => user: {GetComponent<CustomGamesHandler>().userSession.displayName}");
        playerInfoList.Add(new PlayerInfo
            (
                NetworkManager.ServerClientId, 
                GetComponent<CustomGamesHandler>().userSession.userId, 
                GetComponent<CustomGamesHandler>().userSession.displayName, 
                false
            ));
    }

    /// <summary>
    /// Callback on the client disconnected from session
    /// </summary>
    /// <param name="clientId">the player's client id from NetworkManager</param>
    public void OnClientDisconnected(ulong clientId)
    {
        // if the current player is also the server
        if (IsServer)
        {
            Debug.Log($"[SessionRoom] Removing user: {clientId}");
            for (int i = 0; i < playerInfoList.Count; i++)
            {
                if (playerInfoList[i].clientId == clientId)
                {
                    UnregisterPlayerFromSession(currentSessionId, playerInfoList[i].userId.ToString());
                    playerInfoList.RemoveAt(i);
                    break;
                }
            }
        }
        // if the current player is the client
        if (IsClient)
        {
            // if the client who disconnected is the server/host
            if (clientId == NetworkManager.ServerClientId)
            {
                NetworkManager.Singleton.Shutdown();

                // Reset session data and clear player info list
                playerInfoList.Clear();
                isGameStarted.Value = false;
                Debug.Log($"[SessionRoom] Clearing.. {playerInfoList.Count} || {isGameStarted.Value} ||");

                // change view to CustomGamesPanel
                SessionRoomWindow.SetActive(false);
                GetComponent<CustomGamesHandler>().Setup();
            }
        }
    }
    #endregion

    #region Utitilies
    /// <summary>
    /// A utility function to Destroy all Children of the parent transform. Optionally do not remove a specific Transform
    /// </summary>
    /// <param name="parent">Parent Object to destroy children</param>
    /// <param name="doNotRemove">Optional specified Transform that should NOT be destroyed</param>
    private void LoopThroughTransformAndDestroy(Transform parent, Transform doNotRemove = null)
    {
        Debug.Log("[CustomGames] Loop Destroy..");

        //Loop through all the children and add them to a List to then be deleted
        List<GameObject> toBeDeleted = new List<GameObject>();
        foreach (Transform t in parent)
        {
            //except the Do Not Remove transform if there is one
            if (t != doNotRemove)
            {
                toBeDeleted.Add(t.gameObject);
            }
        }
        //Loop through list and Delete all Children
        for (int i = 0; i < toBeDeleted.Count; i++)
        {
            Destroy(toBeDeleted[i]);
        }
    }
    #endregion
}

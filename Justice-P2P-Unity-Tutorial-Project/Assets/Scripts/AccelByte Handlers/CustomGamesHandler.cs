// Copyright (c) 2022 AccelByte Inc. All Rights Reserved.
// This is licensed software from AccelByte Inc, for limitations
// and restrictions contact your company contract manager.

using AccelByte.Api;
using AccelByte.Core;
using AccelByte.Models;
using System;
using Unity.Netcode;
using Unity.WebRTC;
using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

public class CustomGamesHandler : MonoBehaviour
{
    #region Instance
    /// <summary>
    /// Private Instance
    /// </summary>
    static CustomGamesHandler _instance;

    /// <summary>
    /// The Instance Getter
    /// </summary>
    public static CustomGamesHandler Instance => _instance;
    #endregion

    #region UI Binding
    public GameObject CustomGamesWindow;

    [SerializeField]
    private Button refreshListButton;
    [SerializeField]
    private Button backToLobbyButton;

    [Header("Server List")]
    [SerializeField]
    private Transform serverListContent;
    [SerializeField]
    private Transform serverListPlaceholderText;
    [SerializeField]
    private GameObject serverInfoPrefab;

    [Header("Host a Server")]
    [SerializeField]
    private GameObject sessionPasswordPanel;
    [SerializeField]
    private Toggle publicToggle;
    [SerializeField]
    private Toggle privateToggle;
    [SerializeField]
    private InputField sessionNameInputField;
    [SerializeField]
    private InputField sessionPasswordInputField;
    [SerializeField]
    private Button createServerButton;

    [Header("Join Session")]
    [SerializeField]
    private GameObject joinPasswordPopUpPanel;
    [SerializeField]
    private InputField joinPasswordInputField;
    [SerializeField]
    private Button confirmPasswordButton;
    [SerializeField]
    private Button exitButton;

    #endregion

    private AccelByteNetworkTransportManager transportManager;

    private ApiClient apiClient;
    private User user;
    private Lobby lobby;
    private SessionBrowser sessionBrowser;
    
    public PublicUserData userSession;

    private const string gameVersion = "1.0.0";
    private const string gameMode = "1vs1";
    private string roomAccessibility = "public";

    private bool isInitialized = false;

    private void Awake()
    {
        //Check if another Instance is already created, and if so delete this one, otherwise destroy the object
        if (_instance != null && _instance != this)
        {
            Destroy(this);
            return;
        }
        else
        {
            _instance = this;
        }
    }

    private void Start()
    {
        WebRTC.Initialize(WebRTC.HardwareEncoderSupport() ? EncoderType.Hardware : EncoderType.Software, true, true, NativeLoggingSeverity.LS_ERROR);

        // configure API Client for player
        apiClient = MultiRegistry.GetApiClient();
        user = apiClient.GetApi<User, UserApi>();
        lobby = apiClient.GetApi<Lobby, LobbyApi>();
        sessionBrowser = apiClient.GetApi<SessionBrowser, SessionBrowserApi>();
    }

    private void OnApplicationQuit()
    {
        WebRTC.Dispose();
    }

    /// <summary>
    /// Connect Lobby service
    /// </summary>
    public void ConnectLobby()
    {
        Setup();

        lobby.Connected += InitializeNetworkManager;

        //Connect to the Lobby
        if (!lobby.IsConnected)
        {
            lobby.Connect();
        }
    }

    /// <summary>
    /// Disconnect Lobby service
    /// </summary>
    public void DisconnectLobby()
    {
        lobby.Connected -= InitializeNetworkManager;
        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;

        if (lobby.IsConnected)
        {
            lobby.Disconnect();
        }
    }

    #region Network Initialization
    /// <summary>
    /// Initialize Network Transport Manager
    /// </summary>
    private void InitializeNetworkManager()
    {
        Debug.Log("[CustomGames] Websocket connection is established");

        // Initialize AccelByteNetworkTransportManager
        if (transportManager == null)
        {
            if (NetworkManager.Singleton)
            {
                transportManager = NetworkManager.Singleton.GetComponent<AccelByteNetworkTransportManager>();
            }
            else 
            {
                transportManager = gameObject.AddComponent<AccelByteNetworkTransportManager>();
            }
        }
        transportManager.Initialize(apiClient);

        // Initialize TransportEventDelegate
        NetworkTransport.TransportEventDelegate action = (NetworkEvent eventType, ulong clientId, ArraySegment<byte> payload, float receiveTime) =>
        {
            switch (eventType)
            {
                case NetworkEvent.Data:
                    Debug.Log("[TransportEventDelegate] Received data");
                    break;
                case NetworkEvent.Connect:
                    Debug.Log($"[TransportEventDelegate] Connection established with clientid: {clientId} || current userid: {user.Session.UserId} {NetworkManager.Singleton.LocalClientId}||");
                    if (NetworkManager.Singleton.IsHost)
                    {
                        Debug.Log($"[TransportEventDelegate] DETECTED! IS HOST || host_session_id: {transportManager.GetHostedSessionID()} || server_client_id: {transportManager.ServerClientId} ||");

                    }
                    else if (NetworkManager.Singleton.IsClient)
                    {
                        Debug.Log($"[TransportEventDelegate] DETECTED! IS CLIENT || server_client_id: {transportManager.ServerClientId} || local_client: {NetworkManager.Singleton.NetworkConfig.ConnectionData.Length} ||");

                    }
                    break;
                case NetworkEvent.Disconnect:
                    Debug.Log($"[TransportEventDelegate] Disconnected from clientid: {clientId} || current userid: {user.Session.UserId} ||");
                    if (NetworkManager.Singleton.IsHost)
                    {
                        Debug.Log("[TransportEventDelegate] Unregistering player from session..");
                    }
                    break;
                case NetworkEvent.Nothing:
                    Debug.Log("[TransportEventDelegate] Nothing");
                    break;
            };
        };
        transportManager.OnTransportEvent += action;

        // Initialize NetworkManager Callback
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
    }
    #endregion

    #region UI Initialization
    /// <summary>
    /// Setup CustomGamesPanel's UI and state
    /// </summary>
    public void Setup()
    {
        CustomGamesWindow.SetActive(true);

        if (!isInitialized)
        {
            isInitialized = true;

            // setup HostServerPanel's UIs
            publicToggle.onValueChanged.AddListener((bool isOn) =>
            {
                if (isOn)
                {
                    sessionPasswordPanel.SetActive(false);
                    roomAccessibility = "public";
                }
            });
            privateToggle.onValueChanged.AddListener((bool isOn) =>
            {
                if (isOn)
                {
                    sessionPasswordPanel.SetActive(true);
                    roomAccessibility = "private";
                }
            });
            createServerButton.onClick.AddListener(() =>
            {
                CreateSession(sessionNameInputField.text, sessionPasswordInputField.text);

                // reset HostServerPanel's UIs
                sessionNameInputField.text = "";
                sessionPasswordInputField.text = "";
                publicToggle.isOn = true;
                privateToggle.isOn = false;
            });

            // setup ButtonPanel's UIs
            refreshListButton.onClick.AddListener(() =>
            {
                FindSessions();
            });
            backToLobbyButton.onClick.AddListener(() =>
            {
                CustomGamesWindow.SetActive(false);

                DisconnectLobby();
                GetComponent<AuthenticationHandler>().OnLogoutClicked();
            });

            // setup JoinPassword's UIs
            exitButton.onClick.AddListener(() =>
            {
                joinPasswordPopUpPanel.SetActive(false);
            });
        }

        // refresh the server list on opening the Custom Games page
        refreshListButton.onClick.Invoke();

        // get current user data
        user.GetUserByUserId(user.Session.UserId, userResult =>
        {
            if (!userResult.IsError)
            {
                userSession = userResult.Value;

                Debug.Log($"current userid: {userSession.userId}");
            }
        });
    }

    /// <summary>
    /// Function to check if the session need password to join
    /// </summary>
    /// <param name="targetHostUserId">the session host's user id</param>
    /// <param name="sessionId">the session id</param>
    /// <param name="roomAccessibility">the room accessibility of the session (public/private)</param>
    public void CheckSessionPassword(string targetHostUserId, string sessionId, string roomAccessibility)
    {
        if (roomAccessibility == "private")
        {
            // reset JoinPasswordPanel
            joinPasswordInputField.text = "";
            confirmPasswordButton.onClick.RemoveAllListeners();

            joinPasswordPopUpPanel.SetActive(true);

            Debug.Log("[CustomGames] Room session is private");
            confirmPasswordButton.onClick.AddListener(() =>
            {
                JoinSession(targetHostUserId, sessionId, joinPasswordInputField.text);
                joinPasswordPopUpPanel.SetActive(false);
            });
        }
        else
        {
            Debug.Log("[CustomGames] Room session is public");
            JoinSession(targetHostUserId, sessionId);
        }
    }
    #endregion

    #region P2P Functionalities
    /// <summary>
    /// Create a new room session
    /// </summary>
    /// <param name="sessionName">the session's name</param>
    /// <param name="sessionPassword">the session's password</param>
    public void CreateSession(string sessionName, string sessionPassword)
    {
        Debug.Log($"[CustomGames] Creating session.. {sessionName} || {sessionPassword} || {roomAccessibility}");

        SessionBrowserGameSetting setting = new SessionBrowserGameSetting();
        setting.mode = gameMode;
        setting.map_name = "default";
        setting.max_player = 2;
        setting.current_player = 1;
        setting.allow_join_in_progress = true;

        // set password if the room accessibility is private
        if (roomAccessibility == "private")
        {
            setting.password = sessionPassword;
        }

        // store session name and room accessibility data to settings JSON Object
        JObject settingJsonObject = new JObject();
        settingJsonObject.Add("session_name", sessionName);
        settingJsonObject.Add("room_accessibility", roomAccessibility);
        setting.settings = settingJsonObject;

        // create a session with SessionBrowser API
        transportManager.SetSessionBrowserCreationRequest(setting, userSession.userName, gameVersion);
        // network manager start host + server
        NetworkManager.Singleton.StartHost();
    }

    /// <summary>
    /// List all of available room sessions
    /// </summary>
    public void FindSessions()
    {
        // set filter for the GetGameSessions query result
        SessionBrowserQuerySessionFilter sessionFilter = new SessionBrowserQuerySessionFilter();
        sessionFilter.sessionType = SessionType.p2p;

        // get all sessions with SessionBrowser API
        sessionBrowser.GetGameSessions(sessionFilter, result => 
        {
            if (result.IsError || result.Value.sessions == null)
            {
                Debug.Log($"[CustomGames] Get Game Sessions failed");
                // reset Server List Panel
                LoopThroughTransformAndDestroy(serverListContent, serverListPlaceholderText);
                serverListPlaceholderText.gameObject.SetActive(true);
            }
            else
            {
                Debug.Log("[CustomGames] Get Game Sessions successful");
                Debug.Log($"sessions : {result.Value.sessions.Length}");
                serverListPlaceholderText.gameObject.SetActive(false);

                // reset Server List Panel
                LoopThroughTransformAndDestroy(serverListContent, serverListPlaceholderText);

                // loop all available sessions
                foreach (SessionBrowserData sessionData in result.Value.sessions)
                {
                    Debug.Log($"[CustomGames] session name: {sessionData.game_session_setting.settings.GetValue("session_name")} || {sessionData.session_id}");

                    ServerInfoPanel infoPanel = Instantiate(serverInfoPrefab, serverListContent).GetComponent<ServerInfoPanel>();
                    infoPanel.Create(sessionData);
                }
            }
        });
    }

    /// <summary>
    /// Join session with SessionBrowser API and P2P connect to host 
    /// </summary>
    /// <param name="targetHostUserID">the host's user id</param>
    /// <param name="sessionId">the selected session id</param>
    /// <param name="sessionPassword">the session's password that need validation</param>
    public void JoinSession(string targetHostUserID, string sessionId, string sessionPassword = "")
    {
        // join session with SessionBrowser API
        sessionBrowser.JoinSession(sessionId, sessionPassword, result => 
        {
            if (!result.IsError)
            {
                Debug.Log("[CustomGames]Join Session success");

                // join p2p connection to session's host
                Debug.Log($"[CustomGames] Connecting to: {targetHostUserID} ||");
                transportManager.SetTargetHostUserId(targetHostUserID);
                NetworkManager.Singleton.StartClient();

                // set the current session id from client side
                GetComponent<SessionRoomHandler>().SetSessionId(sessionId);
            }
            else
            {
                Debug.Log($"[CustomGames] Join session failed, Error: {result.Error.Code}, Description: {result.Error.Message}");
            }
        });
    }
    #endregion

    #region Callback Functions
    /// <summary>
    /// On any client connected to server (including host)
    /// </summary>
    /// <param name="clientId">the player's client id from NetworkManager</param>
    private void OnClientConnected(ulong clientId)
    {
        if (!NetworkManager.Singleton.IsHost)
        {
            Debug.Log($"[OnClientConnected] Trying to send data via NetworkManager to {NetworkManager.ServerClientId} ||");
            string messageJson = userSession.userId + ":" + userSession.displayName;
            FastBufferWriter writer = new FastBufferWriter(1100, Unity.Collections.Allocator.Temp);
            writer.WriteValueSafe(messageJson);
            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage("Register", NetworkManager.ServerClientId, writer);
        }

        Debug.Log($"[OnClientConnected] {clientId} || {NetworkManager.Singleton.LocalClientId} ||");
        // move to SessionRoomPanel
        CustomGamesWindow.SetActive(false);
        GetComponent<SessionRoomHandler>().Setup();

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

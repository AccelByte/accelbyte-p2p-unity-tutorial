// Copyright (c) 2022 AccelByte Inc. All Rights Reserved.
// This is licensed software from AccelByte Inc, for limitations
// and restrictions contact your company contract manager.

using AccelByte.Api;
using AccelByte.Core;
using UnityEngine;
using UnityEngine.UI;

public class AuthenticationHandler : MonoBehaviour
{
    #region UI Binding
    public GameObject AuthenticationWindow;

    [SerializeField]
    private InputField usernameInputField;
    [SerializeField]
    private InputField passwordInputField;
    [SerializeField]
    private Button loginButton;
    [SerializeField]
    private Text statusText;
    #endregion

    private ApiClient apiClient;
    private User user;

    void Start()
    {
        // configure API Client for player
        apiClient = MultiRegistry.GetApiClient();
        user = apiClient.GetApi<User, UserApi>();
    }

    private void OnEnable()
    {
        // On Enable, setup the Authentication Panel UIs and state
        statusText.text = "Please Login";
        loginButton.onClick.AddListener(() => 
        {
            statusText.text = "Attempting Login";
            OnLoginClicked(usernameInputField.text, passwordInputField.text);
        });
    }

    private void OnDisable()
    {
        // On Disable, remove all listeners from available UIs
        loginButton.onClick.RemoveAllListeners();
    }

    /// <summary>
    /// Login to AccelByte's IAM services
    /// </summary>
    /// <param name="username">username of the player account</param>
    /// <param name="password">password of the player account</param>
    private void OnLoginClicked(string username, string password)
    {
        loginButton.interactable = false;
        statusText.text = "Logging in...";

        user.LoginWithUsername(username, password, new ResultCallback(result =>
        {
            if (!result.IsError)
            {
                Debug.Log("[Authentication] Login successful");

                AuthenticationWindow.gameObject.SetActive(false);
                GetComponent<CustomGamesHandler>().ConnectLobby();
            }
            else
            {
                // display the error to debug log and login's status text
                Debug.Log($"[Authentication] Login failed : {result.Error.Code}, Description : {result.Error.Message}");
                statusText.text = $"Login failed : {result.Error.Code}\n Description : {result.Error.Message}";
            }

            // Enable interaction with the Button again
            loginButton.interactable = true;
        }));
    }

    /// <summary>
    /// Logout from AccelByte's IAM services
    /// </summary>
    public void OnLogoutClicked()
    {
        Debug.Log($"[Authentication] Logging out...");

        //Calling the Logout Function and supplying a callback
        user.Logout((Result result) => 
        {
            if (!result.IsError)
            {
                Debug.Log("[Authentication] Logout successful");

                // Open Login UI
                AuthenticationWindow.gameObject.SetActive(true);
                // reset status text's default text value
                statusText.text = "Please Login";
            }
            else
            {
                Debug.Log($"[Authentication] Logout failed : {result.Error.Code} Description : {result.Error.Message}");
            }
        });
    }
}

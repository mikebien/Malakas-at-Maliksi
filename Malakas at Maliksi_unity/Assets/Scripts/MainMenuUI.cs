using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuUI : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject lobbyHubPanel;
    [SerializeField] private GameObject hostPanel;
    [SerializeField] private GameObject joinPanel;
    [SerializeField] private GameObject roomPanel;

    [Header("Hub Controls")]
    [SerializeField] private TMP_InputField playerNameInput;
    [SerializeField] private Button openCreateLobbyButton;
    [SerializeField] private Button openJoinMenuButton;
    [SerializeField] private Button hubBackButton;

    [Header("Host Panel Controls")]
    [SerializeField] private TMP_Text hostPlayerListText;
    [SerializeField] private TMP_Dropdown hostCharacterDropdown;
    [SerializeField] private Button hostStartGameButton;
    [SerializeField] private Button hostBackButton;

    [Header("Join Panel (Browser) Controls")]
    [SerializeField] private Transform lobbyListContent;
    [SerializeField] private GameObject lobbyItemPrefab;
    [SerializeField] private Button refreshLobbiesButton;
    [SerializeField] private Button joinPanelBackButton;

    [Header("Room Panel (Client in Lobby) Controls")]
    [SerializeField] private TMP_Text roomPlayerListText;
    [SerializeField] private TMP_Dropdown roomCharacterDropdown;
    [SerializeField] private Button roomLeaveButton;

    private void Start()
    {
        ShowPanel(mainMenuPanel);

        openCreateLobbyButton.onClick.AddListener(OnOpenCreateLobbyClicked);
        openJoinMenuButton.onClick.AddListener(OnOpenJoinMenuClicked);
        hubBackButton.onClick.AddListener(() => ShowPanel(mainMenuPanel));

        hostCharacterDropdown.onValueChanged.AddListener(OnHostCharacterChanged);
        hostStartGameButton.onClick.AddListener(OnStartGameClicked);
        hostBackButton.onClick.AddListener(OnLeaveLobbyClicked);

        refreshLobbiesButton.onClick.AddListener(RefreshLobbyList);
        joinPanelBackButton.onClick.AddListener(() => ShowPanel(lobbyHubPanel));

        roomCharacterDropdown.onValueChanged.AddListener(OnRoomCharacterChanged);
        roomLeaveButton.onClick.AddListener(OnLeaveLobbyClicked);
    }

    private void Update()
    {
        if (LobbyManager.Instance != null && LobbyManager.Instance.CurrentLobby != null)
        {
            UpdateLobbyUI();
        }
    }

    private void ShowPanel(GameObject targetPanel)
    {
        mainMenuPanel.SetActive(targetPanel == mainMenuPanel);
        lobbyHubPanel.SetActive(targetPanel == lobbyHubPanel);
        hostPanel.SetActive(targetPanel == hostPanel);
        joinPanel.SetActive(targetPanel == joinPanel);
        roomPanel.SetActive(targetPanel == roomPanel);
    }

    public void OpenHubPanel()
    {
        if (playerNameInput != null && !string.IsNullOrEmpty(playerNameInput.text))
        {
            LobbyManager.Instance.PlayerName = playerNameInput.text;
        }
        ShowPanel(lobbyHubPanel);
    }

    public void ExitGame()
    {
        Debug.Log("Exiting Game...");
        Application.Quit();
    }

    private async void OnOpenCreateLobbyClicked()
    {
        if (playerNameInput != null)
        {
            LobbyManager.Instance.PlayerName = string.IsNullOrEmpty(playerNameInput.text) ? "Player" : playerNameInput.text;
        }

        bool success = await LobbyManager.Instance.CreateLobbyAsync($"{LobbyManager.Instance.PlayerName}'s Lobby");
        if (success)
        {
            ShowPanel(hostPanel);
            hostCharacterDropdown.value = 0;
            await LobbyManager.Instance.UpdatePlayerCharacter("Unassigned");
        }
    }

    private void OnOpenJoinMenuClicked()
    {
        if (playerNameInput != null)
        {
            LobbyManager.Instance.PlayerName = string.IsNullOrEmpty(playerNameInput.text) ? "Player" : playerNameInput.text;
        }
        ShowPanel(joinPanel);
        RefreshLobbyList();
    }

    private async void RefreshLobbyList()
    {
        foreach (Transform child in lobbyListContent)
        {
            Destroy(child.gameObject);
        }

        var response = await LobbyManager.Instance.GetLobbiesAsync();
        if (response != null && response.Results != null)
        {
            foreach (var lobby in response.Results)
            {
                if (lobbyItemPrefab != null)
                {
                    GameObject item = Instantiate(lobbyItemPrefab, lobbyListContent);
                    TMP_Text textComponent = item.GetComponentInChildren<TMP_Text>();
                    Button joinBtn = item.GetComponentInChildren<Button>();

                    if (textComponent != null)
                    {
                        textComponent.text = $"{lobby.Name} ({lobby.Players.Count}/{lobby.MaxPlayers})";
                    }

                    if (joinBtn != null)
                    {
                        string lobbyId = lobby.Id;
                        joinBtn.onClick.AddListener(async () =>
                        {
                            bool joined = await LobbyManager.Instance.JoinLobbyByIdAsync(lobbyId);
                            if (joined)
                            {
                                ShowPanel(roomPanel);
                                roomCharacterDropdown.value = 0;
                                await LobbyManager.Instance.UpdatePlayerCharacter("Unassigned");
                            }
                        });
                    }
                }
            }
        }
    }

    private async void OnHostCharacterChanged(int index)
    {
        string selectedChar = hostCharacterDropdown.options[index].text;
        await LobbyManager.Instance.UpdatePlayerCharacter(selectedChar);
    }

    private async void OnRoomCharacterChanged(int index)
    {
        string selectedChar = roomCharacterDropdown.options[index].text;
        await LobbyManager.Instance.UpdatePlayerCharacter(selectedChar);
    }

    private async void OnStartGameClicked()
    {
        if (LobbyManager.Instance.IsHost() && LobbyManager.Instance.CanStartGame())
        {
            await LobbyManager.Instance.StartHostGameWithRelay();
        }
    }

    private async void OnLeaveLobbyClicked()
    {
        await LobbyManager.Instance.LeaveLobbyAsync();
        ShowPanel(lobbyHubPanel);
    }

    private void UpdateLobbyUI()
    {
        var lobby = LobbyManager.Instance.CurrentLobby;
        if (lobby == null) return;

        string playerListStr = "<b>Players in Lobby:</b>\n";
        foreach (var player in lobby.Players)
        {
            string pName = LobbyManager.Instance.GetPlayerName(player);
            string pChar = LobbyManager.Instance.GetPlayerCharacter(player);
            bool isHostPlayer = (player.Id == lobby.HostId);
            playerListStr += $"- {pName} [{pChar}] {(isHostPlayer ? "(Host)" : "")}\n";
        }

        if (LobbyManager.Instance.IsHost())
        {
            hostPlayerListText.text = playerListStr;
            hostStartGameButton.interactable = LobbyManager.Instance.CanStartGame();
        }
        else
        {
            roomPlayerListText.text = playerListStr;
        }
    }
}
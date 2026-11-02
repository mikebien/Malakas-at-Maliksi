using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LobbyManager : MonoBehaviour
{
    public static LobbyManager Instance { get; private set; }

    public Lobby CurrentLobby { get; private set; }
    public string PlayerName { get; set; } = "Player";
    public string SelectedCharacter { get; private set; } = "Unassigned";

    private float heartbeatTimer;
    private float lobbyPollTimer;
    private bool isConnectingToRelay = false;

    private async void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        await InitializeUGSAsync();
    }

    private async Task InitializeUGSAsync()
    {
        if (UnityServices.State == ServicesInitializationState.Initialized) return;

        await UnityServices.InitializeAsync();
        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
            Debug.Log($"UGS Signed In. Player ID: {AuthenticationService.Instance.PlayerId}");
        }
    }

    private void Update()
    {
        HandleHeartbeat();
        HandleLobbyPolling();
    }

    private async void HandleHeartbeat()
    {
        if (IsHost() && CurrentLobby != null)
        {
            heartbeatTimer -= Time.deltaTime;
            if (heartbeatTimer <= 0f)
            {
                heartbeatTimer = 15f;
                try
                {
                    await LobbyService.Instance.SendHeartbeatPingAsync(CurrentLobby.Id);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Heartbeat error: {e.Message}");
                }
            }
        }
    }

    private async void HandleLobbyPolling()
    {
        if (CurrentLobby != null)
        {
            lobbyPollTimer -= Time.deltaTime;
            if (lobbyPollTimer <= 0f)
            {
                lobbyPollTimer = 1.5f;
                try
                {
                    CurrentLobby = await LobbyService.Instance.GetLobbyAsync(CurrentLobby.Id);

                    // If we are a client and the host has published a Relay Join Code, auto-connect!
                    if (!IsHost() && !isConnectingToRelay && !NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsConnectedClient)
                    {
                        if (CurrentLobby.Data != null && CurrentLobby.Data.TryGetValue("RelayJoinCode", out var dataObj))
                        {
                            if (!string.IsNullOrEmpty(dataObj.Value))
                            {
                                isConnectingToRelay = true;
                                await JoinRelayGameWithCodeAsync(dataObj.Value);
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Lobby poll error: {e.Message}");
                }
            }
        }
    }

    public bool IsHost()
    {
        return CurrentLobby != null && AuthenticationService.Instance.IsSignedIn && CurrentLobby.HostId == AuthenticationService.Instance.PlayerId;
    }

    public async Task<bool> CreateLobbyAsync(string lobbyName)
    {
        try
        {
            CreateLobbyOptions options = new CreateLobbyOptions
            {
                IsPrivate = false,
                Player = CreatePlayerObject()
            };

            CurrentLobby = await LobbyService.Instance.CreateLobbyAsync(string.IsNullOrEmpty(lobbyName) ? $"{PlayerName}'s Lobby" : lobbyName, 2, options);
            return true;
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError($"Failed to create lobby: {e}");
            return false;
        }
    }

    public async Task<QueryResponse> GetLobbiesAsync()
    {
        try
        {
            QueryLobbiesOptions options = new QueryLobbiesOptions
            {
                Count = 25,
                Filters = new List<QueryFilter>
                {
                    new QueryFilter(QueryFilter.FieldOptions.AvailableSlots, "0", QueryFilter.OpOptions.GT)
                }
            };
            return await LobbyService.Instance.QueryLobbiesAsync(options);
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError($"Failed to get lobbies: {e}");
            return null;
        }
    }

    public async Task<bool> JoinLobbyByIdAsync(string lobbyId)
    {
        try
        {
            JoinLobbyByIdOptions options = new JoinLobbyByIdOptions
            {
                Player = CreatePlayerObject()
            };
            CurrentLobby = await LobbyService.Instance.JoinLobbyByIdAsync(lobbyId, options);
            return true;
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError($"Failed to join lobby: {e}");
            return false;
        }
    }

    public async Task LeaveLobbyAsync()
    {
        if (CurrentLobby == null) return;
        try
        {
            string playerId = AuthenticationService.Instance.PlayerId;
            if (IsHost())
            {
                await LobbyService.Instance.DeleteLobbyAsync(CurrentLobby.Id);
            }
            else
            {
                await LobbyService.Instance.RemovePlayerAsync(CurrentLobby.Id, playerId);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Error leaving lobby: {e.Message}");
        }
        finally
        {
            CurrentLobby = null;
            SelectedCharacter = "Unassigned";
            isConnectingToRelay = false;
        }
    }

    private Player CreatePlayerObject()
    {
        return new Player
        {
            Data = new Dictionary<string, PlayerDataObject>
            {
                { "PlayerName", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, string.IsNullOrEmpty(PlayerName) ? "Player" : PlayerName) },
                { "Character", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, "Unassigned") }
            }
        };
    }

    public async Task UpdatePlayerCharacter(string characterChoice)
    {
        SelectedCharacter = characterChoice;
        if (CurrentLobby == null) return;

        try
        {
            UpdatePlayerOptions options = new UpdatePlayerOptions
            {
                Data = new Dictionary<string, PlayerDataObject>
                {
                    { "Character", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, characterChoice) }
                }
            };
            string playerId = AuthenticationService.Instance.PlayerId;
            CurrentLobby = await LobbyService.Instance.UpdatePlayerAsync(CurrentLobby.Id, playerId, options);
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError($"Failed to update character: {e}");
        }
    }

    public bool CanStartGame()
    {
        if (CurrentLobby == null || CurrentLobby.Players == null || CurrentLobby.Players.Count < 2) return false;

        string char1 = GetPlayerCharacter(CurrentLobby.Players[0]);
        string char2 = GetPlayerCharacter(CurrentLobby.Players[1]);

        if (string.IsNullOrEmpty(char1) || string.IsNullOrEmpty(char2)) return false;
        if (char1 == "Unassigned" || char2 == "Unassigned") return false;
        if (char1 == char2) return false;

        return true;
    }

    public string GetPlayerCharacter(Player player)
    {
        if (player.Data != null && player.Data.TryGetValue("Character", out var dataObj))
        {
            return dataObj.Value;
        }
        return "Unassigned";
    }

    public string GetPlayerName(Player player)
    {
        if (player.Data != null && player.Data.TryGetValue("PlayerName", out var dataObj))
        {
            return dataObj.Value;
        }
        return "Unknown";
    }

    // Host starts game: creates Relay allocation, publishes join code to lobby data, starts host, and loads Level 1
    public async Task StartHostGameWithRelay()
    {
        try
        {
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(2);
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            // Publish Relay Join Code to Lobby Data so clients can read it
            UpdateLobbyOptions updateOptions = new UpdateLobbyOptions
            {
                Data = new Dictionary<string, DataObject>
                {
                    { "RelayJoinCode", new DataObject(DataObject.VisibilityOptions.Member, joinCode) }
                }
            };
            CurrentLobby = await LobbyService.Instance.UpdateLobbyAsync(CurrentLobby.Id, updateOptions);

            NetworkManager.Singleton.GetComponent<UnityTransport>().SetHostRelayData(
                allocation.RelayServer.IpV4,
                (ushort)allocation.RelayServer.Port,
                allocation.AllocationIdBytes,
                allocation.Key,
                allocation.ConnectionData
            );

            NetworkManager.Singleton.StartHost();
            NetworkManager.Singleton.SceneManager.LoadScene("Level1", LoadSceneMode.Single);
        }
        catch (Exception e)
        {
            Debug.LogError($"Relay Host Error: {e}");
        }
    }

    // Client connects to Relay using the join code found in the lobby data
    private async Task JoinRelayGameWithCodeAsync(string joinCode)
    {
        try
        {
            JoinAllocation allocation = await RelayService.Instance.JoinAllocationAsync(joinCode);

            NetworkManager.Singleton.GetComponent<UnityTransport>().SetClientRelayData(
                allocation.RelayServer.IpV4,
                (ushort)allocation.RelayServer.Port,
                allocation.AllocationIdBytes,
                allocation.Key,
                allocation.ConnectionData,
                allocation.HostConnectionData
            );

            NetworkManager.Singleton.StartClient();
            Debug.Log("Successfully connected to Relay as Client!");
        }
        catch (Exception e)
        {
            Debug.LogError($"Relay Client Join Error: {e}");
            isConnectingToRelay = false;
        }
    }
}
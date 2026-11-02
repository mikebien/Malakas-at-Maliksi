using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class CharacterSpawner : NetworkBehaviour
{
    [SerializeField] private GameObject malakasPrefab;
    [SerializeField] private GameObject maliksiPrefab;
    
    [SerializeField] private Transform malakasSpawnPoint;
    [SerializeField] private Transform maliksiSpawnPoint;

    private void Awake()
    {
        if (NetworkManager.Singleton != null)
        {
            // Listen for client connection events (handles late connections)
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;

            if (NetworkManager.Singleton.SceneManager != null)
            {
                // Listen for network scene transitions
                NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += OnLoadEventCompleted;
            }
        }
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;

            if (NetworkManager.Singleton.SceneManager != null)
            {
                NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= OnLoadEventCompleted;
            }
        }
    }

    private void OnLoadEventCompleted(string sceneName, UnityEngine.SceneManagement.LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        if (!IsServer || sceneName != "Level1") return;

        foreach (ulong clientId in clientsCompleted)
        {
            SpawnPlayerForClient(clientId);
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        if (!IsServer) return;

        // If the server is already running Level1, spawn the late-joining client immediately
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Level1")
        {
            SpawnPlayerForClient(clientId);
        }
    }

    private void SpawnPlayerForClient(ulong clientId)
    {
        // Prevent duplicate spawns for the same client ID
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client) && client.PlayerObject != null)
        {
            return;
        }

        string chosenCharacter = "Unassigned";

        if (LobbyManager.Instance != null && LobbyManager.Instance.CurrentLobby != null)
        {
            var lobbyPlayers = LobbyManager.Instance.CurrentLobby.Players;
            int clientIndex = 0;

            foreach (var clientPair in NetworkManager.Singleton.ConnectedClients)
            {
                if (clientPair.Key == clientId)
                {
                    if (clientIndex < lobbyPlayers.Count)
                    {
                        chosenCharacter = LobbyManager.Instance.GetPlayerCharacter(lobbyPlayers[clientIndex]);
                    }
                    break;
                }
                clientIndex++;
            }
        }

        GameObject prefabToSpawn = (chosenCharacter == "Maliksi") ? maliksiPrefab : malakasPrefab;
        Transform spawnPoint = (chosenCharacter == "Maliksi") ? maliksiSpawnPoint : malakasSpawnPoint;

        GameObject playerInstance = Instantiate(prefabToSpawn, spawnPoint.position, spawnPoint.rotation);
        playerInstance.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId);

        Debug.Log($"Successfully spawned {chosenCharacter} for Client ID: {clientId}");
    }
}
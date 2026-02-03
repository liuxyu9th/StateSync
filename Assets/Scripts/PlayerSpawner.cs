using System;
using UnityEngine;
using Unity.Netcode;

public class PlayerSpawner : NetworkBehaviour
{
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private Vector2[] spawnPositions;
    
    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        }
    }

    private void Update()
    {
        if (IsSpawned)
        {
            
        }
        
    }

    private void OnClientConnected(ulong clientId)
    {
        if (IsServer)
        {
            SpawnPlayer(clientId);
        }
    }
    
    private void SpawnPlayer(ulong clientId)
    {
        Vector2 spawnPos = spawnPositions.Length > 0 
            ? spawnPositions[clientId % (ulong)spawnPositions.Length] 
            : Vector2.zero;
        NetworkObject networkObject = playerPrefab.GetComponent<NetworkObject>();
        networkObject.SpawnAsPlayerObject(clientId);
        networkObject.transform.position = spawnPos;
    }
    
    public override void OnNetworkDespawn()
    {
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        }
    }
}
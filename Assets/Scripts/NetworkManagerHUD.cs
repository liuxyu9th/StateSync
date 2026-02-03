using UnityEngine;
using Unity.Netcode;

public class NetworkManagerHUD : MonoBehaviour
{
    private NetworkManager networkManager;
    
    private void Awake()
    {
        networkManager = GetComponent<NetworkManager>();
    }
    
    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 300, 300));
        
        if (!networkManager.IsClient && !networkManager.IsServer)
        {
            if (GUILayout.Button("Host"))
            {
                networkManager.StartHost();
            }
            
            if (GUILayout.Button("Client"))
            {
                networkManager.StartClient();
            }
            
            if (GUILayout.Button("Server"))
            {
                networkManager.StartServer();
            }
        }
        else
        {
            string mode = networkManager.IsHost ? "Host" : 
                networkManager.IsServer ? "Server" : "Client";
            
            GUILayout.Label($"Mode: {mode}");
            
            if (GUILayout.Button("Shutdown"))
            {
                networkManager.Shutdown();
            }
        }
        
        GUILayout.EndArea();
    }
}
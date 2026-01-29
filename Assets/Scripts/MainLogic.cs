using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MainLogic : MonoBehaviour
{
    [Header("玩家预制")]
    public GameObject PlayerPrefab;
    [Header("是否是服务器")]
    public bool IsServer;
    Dictionary<int,Player> players = new Dictionary<int, Player>();
    Server server;
    Client client;
    public int MyPlayerId;
    public Player MyPlayer;
    public void GetGameState(GameState refState)
    {
        refState.PlayerPos.Clear();
        foreach (var item in players)
        {
            refState.PlayerPos.Add(item.Key, item.Value.PositionData);
        }
    }

    public void UpdateGameState(int deltaTime)
    {
        foreach (var item in server.operationRequests)
        {
            Player p;
            if(!players.TryGetValue(item.Key, out p))
                continue;
            var deltaMove = item.Value.moveForward.ToVector3() * deltaTime;
            p.SyncState(p.transform.position + deltaMove);
        }
    }

    public Player AddPlayer(int id)
    {
        GameObject go = Instantiate(PlayerPrefab);
        var player = go.GetComponent<Player>();
        players.Add(id, player);
        player.Init(this);
        return player;
    }
    public void RemovePlayer(int id)
    {
        if (players.Remove(id, out Player p))
        {
            Destroy(p.gameObject);
        }
    }
    private void Awake()
    {
        if (IsServer)
        {
            server = new Server(9999, this);
            server.StartServer();
        }
        else
        {
            client = new Client(this);
            client.Connect("127.0.0.1",9999);
        }
    }

    public void SyncState(GameState state)
    {
        if(state == null)
            return;
        if (MyPlayerId == 0)
        {
            MyPlayerId = state.myId;
        }
        if(state.PlayerPos == null)
            return;
        foreach (var item in state.PlayerPos)
        {
            Player p;
            if (!players.TryGetValue(item.Key, out p))
            {
                p = AddPlayer(item.Key);
            }

            if (MyPlayerId == item.Key && MyPlayer == null)
            {
                MyPlayer = p;
            }
            p.transform.position = item.Value.ToVector3();
        }
    }
    private void Start()
    {
        if (IsServer)
        {
            MyPlayerId = 0;
            AddPlayer(MyPlayerId);
        }
    }
}

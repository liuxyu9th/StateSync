
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using UnityEngine;

public class Server
{
    private readonly object _stateLock = new object();
    private readonly object _clientsLock = new object();
    private TcpListener tcpListener;
    List<ClientHandler> clients = new List<ClientHandler>();
    public bool Running = false;
    private GameState State = new GameState();
    public int BroadCastInterval = 50;
    private MainLogic mainLogic;
    private bool stateUpdated = false;
    public ConcurrentDictionary<int ,OperationRequest> operationRequests = new ConcurrentDictionary<int ,OperationRequest>();
    public Server(int port,MainLogic mainLogic)
    {
        tcpListener = new TcpListener(IPAddress.Any, port);
        this.mainLogic = mainLogic;
    }

    public void StartServer()
    {
        Running = true;
        Task.Run(Listen);
        Task.Run(SendToAll);
    }

    private void Listen()
    {
        tcpListener.Start();
        while (Running)
        {
            var client = tcpListener.AcceptTcpClient();
            lock (_clientsLock)
            {
                var handler = new ClientHandler(client, this, clients.Count + 1, mainLogic);
                clients.Add(handler);
                Task.Run(() => handler.HandleClient());
            }
        }
    }

    long lastUpdate = 0;
    public void UpdateGameState(object o)
    {
        lock (_stateLock)
        {
            int deltaTime = BroadCastInterval;
            if (lastUpdate != 0)
            {
                deltaTime = (int)(lastUpdate - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            }
            mainLogic.UpdateGameState(deltaTime);
            mainLogic.GetGameState(State);
            stateUpdated = true;
            lastUpdate = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
    }

    private void SendToAll()
    {
        while (Running)
        {
            int t1 = DateTime.Now.Millisecond;
            Loom.QueueOnMainThread(UpdateGameState,null);
            while (!stateUpdated || DateTime.Now.Millisecond - t1 < BroadCastInterval)
            {
                Thread.Sleep(Math.Clamp(BroadCastInterval - (DateTime.Now.Millisecond - t1),1,BroadCastInterval));
            }

            string json;
            lock (_stateLock)
            {
                try
                {
                    json = JsonConvert.SerializeObject(State);
                }
                catch (Exception e)
                {
                    Debug.LogError(e);
                    throw;
                }
            }
            byte[] buffer = Encoding.UTF8.GetBytes(json);
            lock (_clientsLock)
            {
                foreach (var client in clients)
                {
                    client.SendData(buffer);
                }
            }
            stateUpdated = false;
        }
    }

    public void RemoveClient(ClientHandler clientHandler)
    {
        lock (_clientsLock)
        {
            clients.Remove(clientHandler);
        }
    }
}

public class ClientHandler
{
    private TcpClient _client;
    private NetworkStream _stream;
    private Server _server;
    public int ClientId { get; }
    public bool IsConnected { get; private set; }

    private MainLogic mainLogic;
    public ClientHandler(TcpClient client, Server server, int clientId,MainLogic logic)
    {
        _client = client;
        _server = server;
        ClientId = clientId;
        _stream = client.GetStream();
        _stream.WriteTimeout = 5000;
        _stream.ReadTimeout = 5000;
        IsConnected = true;
        mainLogic = logic;
    }
    public void SendData(string data)
    {
        try
        {
            byte[] buffer = Encoding.UTF8.GetBytes(data);
            _stream.Write(buffer, 0, buffer.Length);
            _stream.Flush();
        }
        catch
        {
            Disconnect();
        }
    }
    public void SendData(byte[] buffer)
    {
        try
        {
            _stream.Write(buffer, 0, buffer.Length);
            _stream.Flush();
        }
        catch
        {
            Disconnect();
        }
    }
    public void Disconnect()
    {
        if (IsConnected)
        {
            IsConnected = false;
            _stream?.Close();
            _client?.Close();
            _server.RemoveClient(this);
            Debug.LogError($"Client {ClientId} disconnected");
        }
    }
    
    private void ProcessOperation(OperationRequest op)
    {
        if(op.ClientId == 0)
            return;
        _server.operationRequests[op.ClientId] = op;
    }
    public void HandleClient()
    {
        try
        {
            Loom.QueueOnMainThread((o) =>
            {
                mainLogic.AddPlayer(ClientId);
            },null);
            GameState state = new GameState();
            state.myId = ClientId;
            SendData(JsonConvert.SerializeObject(state));
            Debug.Log($"Client {ClientId} connected from {_client.Client.RemoteEndPoint}");
            
            byte[] buffer = new byte[4096];
            while (IsConnected && _client.Connected && _server.Running)
            {
                int bytesRead;
                try
                {
                    bytesRead = _stream.Read(buffer, 0, buffer.Length);
                    if (bytesRead == 0) continue;
                }
                catch (Exception e)
                {
                    Debug.LogError(e);
                    continue;
                }

                string json = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                var op = JsonConvert.DeserializeObject<OperationRequest>(json);
                ProcessOperation(op);
                Thread.Sleep(20);
            }
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            throw;
        }
    }
}
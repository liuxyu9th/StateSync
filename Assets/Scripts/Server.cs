
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
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
    Thread listenThread;
    Thread broadcastThread;
    public Server(int port,MainLogic mainLogic)
    {
        tcpListener = new TcpListener(IPAddress.Any, port);
        this.mainLogic = mainLogic;
    }

    public void Clear()
    {
        try
        {
            listenThread?.Abort();
            broadcastThread?.Abort();
            foreach (var item in clients)
            {
                item.Clear();
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    public void StartServer()
    {
        Running = true;
        listenThread = new Thread(Listen);
        broadcastThread = new Thread(SendToAll);
        listenThread.Start();
        broadcastThread.Start();
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
                handler.Start();
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
                deltaTime = (int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - lastUpdate);
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
            long t1 = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            Loom.QueueOnMainThread(UpdateGameState,null);
            int passTime = (int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - t1);
            while (!stateUpdated || passTime < BroadCastInterval)
            {
                Thread.Sleep(Math.Clamp(BroadCastInterval - passTime,10,BroadCastInterval));
                passTime = (int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - t1);
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
            byte[] buffer = Encoding.UTF8.GetBytes(json + "|");
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
    public Thread handlerThread;
    private TcpClient _client;
    private Server _server;
    public int ClientId { get; }
    public bool IsConnected { get; private set; }

    private MainLogic mainLogic;
    public ClientHandler(TcpClient client, Server server, int clientId,MainLogic logic)
    {
        _client = client;
        _server = server;
        ClientId = clientId;
        /*_stream.WriteTimeout = 5000;
        _stream.ReadTimeout = 5000;*/
        IsConnected = true;
        mainLogic = logic;
    }

    public void Start()
    {
        handlerThread = new Thread(HandleClient);
        handlerThread.Start();
    }
    public void SendData(string data)
    {
        try
        {
            byte[] buffer = Encoding.UTF8.GetBytes(data + "|");
            _client.Client.Send(buffer);
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
            _client.Client.Send(buffer);
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
            _client?.Close();
            _server.RemoveClient(this);
            Debug.LogError($"Client {ClientId} disconnected");
        }
    }

    public void Clear()
    {
        try
        {
            _client?.Close();
            handlerThread?.Abort();
        }
        catch (Exception e)
        {
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
            SendData(JsonConvert.SerializeObject(state) + "|");
            Debug.Log($"Client {ClientId} connected from {_client.Client.RemoteEndPoint}");
            
            byte[] buffer = new byte[4096];
            while (IsConnected && _client.Connected && _server.Running)
            {
                int bytesRead;
                try
                {
                    bytesRead = _client.Client.Receive(buffer);
                    if (bytesRead == 0) continue;
                }
                catch (Exception e)
                {
                    Debug.LogError(e);
                    continue;
                }

                string json = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                json = json.Split("|")[0];
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
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using UnityEngine;

public class OperationRequest
{
    public Vector3Data moveForward;
    public int ClientId { get; set; }

    public void Clear()
    {
        moveForward = Vector3.zero.ConvertToGameStateData();
    }
}
public class Client
{
    public int id;
    private TcpClient _client;
    private NetworkStream _stream;
    private bool _isConnected;
    public MainLogic mainLogic;
    private GameState curState;
    private Player player;
    private OperationRequest curOperations = new OperationRequest();
    
    private readonly object _stateLock = new object();
    private readonly object _opertationLock = new object();

    private Thread recThread;
    private Thread sendThread;
    public bool Running = false;

    public Client(MainLogic logic)
    {
        mainLogic = logic;
    }

    public void Clear()
    {
        Running = false;
        try
        {
            recThread?.Abort();
            sendThread?.Abort();
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }
    public void Connect(string serverIp, int port)
    {
        try
        {
            _client = new TcpClient();
            _client.Connect(serverIp, port);
            _stream = _client.GetStream();
            /*_stream.WriteTimeout = 5000;
            _stream.ReadTimeout = 5000;*/
            _isConnected = true;
            Debug.Log($"Connected to server {serverIp}:{port}");

            Running = true;
            recThread = new Thread(ReceiveMessages);
            sendThread = new Thread(SendOperation);
            recThread.Start();
            sendThread.Start();
        }
        catch (Exception ex)
        {
            Debug.LogError($"Connection error: {ex.Message}");
        }
    }

    private void SyncState(object o)
    {
        lock (_stateLock)
        {
            if (curState != null)
            {
                if (id == 0)
                {
                    id = curState.myId;
                }
                mainLogic.SyncState(curState);
            }
        }
    }
    
    private void Disconnect()
    {
        if (_isConnected)
        {
            _isConnected = false;
            _stream?.Close();
            _client?.Close();
            Running = false;
            Console.WriteLine("Disconnected from server");
        }
    }
    private void SendOperation()
    {
        while (_isConnected && Running)
        {
            try
            {
                string json;
                lock (_opertationLock)
                {
                    if (curOperations.ClientId == 0)
                    {
                        curOperations.ClientId = id;
                    }
                    if (mainLogic.MyPlayer != null)
                    {
                        curOperations.moveForward = mainLogic.MyPlayer.moveForward.ConvertToGameStateData();
                    }
                    json = JsonConvert.SerializeObject(curOperations);
                }
                var data = Encoding.UTF8.GetBytes(json);
                try
                {
                    _stream.Write(data, 0, data.Length);
                    _stream.Flush();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    continue;
                }
                
                lock (_opertationLock)
                {
                    curOperations.Clear();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Send error: {ex.Message}");
                Disconnect();
            }
            Thread.Sleep(30);
        }
    }

    private void ReceiveMessages()
    {
        byte[] buffer = new byte[4096];
        while (_isConnected && _client.Connected && Running)
        {
            try
            {
                int bytesRead;
                try
                {
                    bytesRead = _stream.Read(buffer, 0, buffer.Length);
                    if (bytesRead == 0) continue;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    continue;
                }

                string data = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                lock (_stateLock)
                {
                     curState = JsonConvert.DeserializeObject<GameState>(data);
                }
                Loom.QueueOnMainThread(SyncState,null);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Receive error: {ex.Message}");
                continue;
            }
            Thread.Sleep(10);
        }
        Disconnect();
    }

}

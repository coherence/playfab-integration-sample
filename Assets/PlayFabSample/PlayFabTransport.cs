using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.InteropServices;
using Coherence.Brook;
using Coherence.Brook.Octet;
using Coherence.Common;
using Coherence.Connection;
using Coherence.Stats;
using Coherence.Transport;
using PlayFab.Party;
using Logger = Coherence.Log.Logger;

public class PlayFabTransport : ITransport
{
    public event Action OnOpen;
    public event Action<ConnectionException> OnError;
    public TransportState State { get; private set; }
    public bool IsReliable => false;
    public bool CanSend => true;
    public int HeaderSize { get; }
    public string Description => "PlayFabTransport";

    private PlayFabMultiplayerManager _playFabMultiplayerManager;
    private List<PlayFabPlayer> host;
    private string hostId;
    private string networkId;
    private Logger _logger;

    private bool isClosing;
    
    private readonly Queue<byte[]> incomingPackets = new();

    public PlayFabTransport(Logger logger, string networkId, string hostId)
    {
        this.networkId = networkId;
        this.hostId = hostId;
        _playFabMultiplayerManager = PlayFabMultiplayerManager.Get();
        _logger = logger.With<PlayFabTransport>();
    }
    
    public void Open(EndpointData _, ConnectionSettings __)
    {
        State = TransportState.Opening;
        _playFabMultiplayerManager.JoinNetwork(networkId);
        _playFabMultiplayerManager.OnNetworkJoined += OnNetworkJoined;
        _playFabMultiplayerManager.OnRemotePlayerJoined += OnRemotePlayerJoined;
        _playFabMultiplayerManager.OnDataMessageNoCopyReceived += OnDataMessageNoCopyReceived;
        _playFabMultiplayerManager.OnError += OnPlayFabError;
    }

    private void OnNetworkJoined(object sender, string networkid)
    {
        _logger.Info("Connected to network", ("Network Id", networkId), ("State", State), ("Network State", _playFabMultiplayerManager.State));

        if (State != TransportState.Open && host != null)
        {
            OpenNetwork();
        }
    }

    private void OnRemotePlayerJoined(object sender, PlayFabPlayer player)
    {
        _logger.Info("Player Joined: Searching for host", ("Network Id", networkId), ("State", State),
            ("Network State", _playFabMultiplayerManager.State), ("Host Id", hostId), ("Player Joined Id", player.EntityKey.Id));

        if (!player.EntityKey.Id.Equals(hostId)) return;

        _logger.Info("Found host", ("Network Id", networkId), ("State", State),
            ("Network State", _playFabMultiplayerManager.State), ("Host Id", hostId));

        if (_playFabMultiplayerManager.State != PlayFabMultiplayerManagerState.ConnectedToNetwork)
        {
            return;
        }
        
        host = new List<PlayFabPlayer>() { player };
        OpenNetwork();
    }

    private void OpenNetwork()
    {
        State = TransportState.Open;
        OnOpen?.Invoke();
    }

    private void OnPlayFabError(object sender, PlayFabMultiplayerManagerErrorArgs args)
    {
        _logger.Error("PlayFab Error", ("Message", args.Message));

        if (!isClosing)
        {
            OnError?.Invoke(new ConnectionException(args.Message));
        }
    }

    private void OnDataMessageNoCopyReceived(object sender, PlayFabPlayer from, IntPtr buffer, uint buffersize)
    {
        // Copy packet data into managed byte array
        var packet = new byte[buffersize];
        Marshal.Copy(buffer, packet, 0 , (int)buffersize);

        incomingPackets.Enqueue(packet);
    }
    
    public void Close()
    {
        _logger.Info("Leaving PlayFab network");
        _playFabMultiplayerManager.LeaveNetwork();
        _playFabMultiplayerManager.OnRemotePlayerJoined -= OnRemotePlayerJoined;
        _playFabMultiplayerManager.OnDataMessageNoCopyReceived -= OnDataMessageNoCopyReceived;
        _playFabMultiplayerManager.OnError -= OnPlayFabError;
    }
    
    public void Send(IOutOctetStream data)
    {
        // Disconnect packet needs to be sent reliably, otherwise it will be discarded when the connection is closed
        var sendType = isClosing ? DeliveryOption.Guaranteed : DeliveryOption.BestEffort;

        var buffer = data.Close();
        var result = _playFabMultiplayerManager.SendDataMessage(buffer.ToArray(), host, sendType);
        
        if (!result)
        {
            _logger.Error($"{nameof(PlayFabTransport)} failed to send PlayFab Party packet.");
        }
    }

    public void Receive(List<(IInOctetStream, IPEndPoint)> buffer)
    {
        while (incomingPackets.Count > 0)
        {
            var packet = incomingPackets.Dequeue();
            var stream = new InOctetStream(packet);
            buffer.Add((stream, default));
        }
    }

    public void PrepareDisconnect()
    {
        isClosing = true;
    }
}

public class PlayFabTransportFactory : ITransportFactory
{
    private string networkId;
    private string host;
    
    public ITransport Create(ushort mtu, IStats stats, Logger logger)
    {
        return new PlayFabTransport(logger, networkId, host);
    }
    
    public PlayFabTransportFactory(string networkId, string host)
    {
        this.networkId = networkId;
        this.host = host;
    }
}

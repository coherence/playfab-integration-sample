using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Coherence.Log;
using Coherence.Toolkit.Relay;
using PartyCSharpSDK;
using PlayFab.Party;
using UnityEngine;
using Logger = Coherence.Log.Logger;

public class PlayFabRelay : IRelay
{
    private PlayFabMultiplayerManager _playFabMultiplayerManager;
    private Dictionary<PlayFabPlayer, PlayFabRelayConnection> _connectionMap = new();
    private PARTY_DIRECT_PEER_CONNECTIVITY_OPTIONS _connectivityOptions;
    private Logger _logger;

    public CoherenceRelayManager RelayManager { get; set; }

    public PlayFabRelay(PARTY_DIRECT_PEER_CONNECTIVITY_OPTIONS connectivityOptions)
    {
        _logger = Log.GetLogger<PlayFabRelay>();
        _connectivityOptions = connectivityOptions;
    }
    
    public void Open()
    {
        _playFabMultiplayerManager = PlayFabMultiplayerManager.Get();
        
        if (_playFabMultiplayerManager.State != PlayFabMultiplayerManagerState.ConnectedToNetwork)
        {
            _logger.Error("Must be connected to a PlayFab Network to open relay.");
            return;
        }

        _playFabMultiplayerManager.OnRemotePlayerJoined += OnRemotePlayerJoined;
        _playFabMultiplayerManager.OnRemotePlayerLeft += OnRemotePlayerLeft;
        _playFabMultiplayerManager.OnDataMessageNoCopyReceived += OnDataMessageNoCopyReceived;
    }

    private void OnDataMessageNoCopyReceived(object sender, PlayFabPlayer from, IntPtr buffer, uint buffersize)
    {
        // Copy packet data into managed byte array
        var packet = new byte[buffersize];
        Marshal.Copy(buffer, packet, 0 , (int)buffersize);

        if (!_connectionMap.TryGetValue(from, out var relayConnection))
        {
            _logger.Error($"{nameof(PlayFabRelay)} Failed to find client for connection.", ("Connection Id", from.EntityKey.Id));
            return;
        }

        // Push message to the relay connection
        relayConnection.EnqueueMessageFromPlayFab(new ArraySegment<byte>(packet));
    }

    private void OnRemotePlayerJoined(object sender, PlayFabPlayer player)
    {
        var relayConnection = new PlayFabRelayConnection(player, _playFabMultiplayerManager);
        
        _connectionMap.Add(player, relayConnection);
        
        RelayManager.OpenRelayConnection(relayConnection);
    }
    
    private void OnRemotePlayerLeft(object sender, PlayFabPlayer player)
    {
        if (!_connectionMap.TryGetValue(player, out var relayConnection))
        {
            _logger.Error("Missing Relay Connection", ("Connection Id", player.EntityKey.Id));
            Debug.LogError($"Missing relay connection for {player.EntityKey}");
            return;
        }
        
        RelayManager.CloseAndRemoveRelayConnection(relayConnection);
        _connectionMap.Remove(player);
    }

    public void Close()
    {
        _playFabMultiplayerManager.OnRemotePlayerJoined -= OnRemotePlayerJoined;
        _playFabMultiplayerManager.OnRemotePlayerLeft -= OnRemotePlayerLeft;
        _playFabMultiplayerManager.OnDataMessageNoCopyReceived -= OnDataMessageNoCopyReceived;
        _logger.Info("Leaving PlayFab Network", ("My Id", _playFabMultiplayerManager.LocalPlayer?.EntityKey?.Id ?? string.Empty));
        _playFabMultiplayerManager.LeaveNetwork();
    }
    
    public void Update()
    {
    }
}

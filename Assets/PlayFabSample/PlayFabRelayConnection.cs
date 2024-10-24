using System;
using System.Collections.Generic;
using Coherence.Toolkit.Relay;
using PlayFab.Party;

public class PlayFabRelayConnection : IRelayConnection
{
    private IEnumerable<PlayFabPlayer> player;
    private PlayFabMultiplayerManager manager;
    
    private readonly Queue<ArraySegment<byte>> messagesFromPlayFabToServer = new();

    public PlayFabRelayConnection(PlayFabPlayer player, PlayFabMultiplayerManager manager)
    {
        this.player = new List<PlayFabPlayer>() { player };
        this.manager = manager;
    }
    
    public void OnConnectionOpened()
    {
    }

    public void OnConnectionClosed()
    {
        messagesFromPlayFabToServer.Clear();
    }

    public void ReceiveMessagesFromClient(List<ArraySegment<byte>> packetBuffer)
    {
        // Transfer packets to the coherence replication server
        while (messagesFromPlayFabToServer.Count > 0)
        {
            var packetData = messagesFromPlayFabToServer.Dequeue();
            packetBuffer.Add(packetData);
        }
    }

    public void SendMessageToClient(ReadOnlySpan<byte> packetData)
    {
        manager.SendDataMessage(packetData.ToArray(), player, DeliveryOption.BestEffort);
    }

    public void EnqueueMessageFromPlayFab(ArraySegment<byte> packet)
    {
        messagesFromPlayFabToServer.Enqueue(packet);
    }
}

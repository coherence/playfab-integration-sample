using System;
using System.IO;
using Coherence;
using Coherence.Common;
using Coherence.Connection;
using Coherence.Toolkit;
using Coherence.Toolkit.ReplicationServer;
using Coherence.Transport;
using PartyCSharpSDK;
using PlayFab;
using PlayFab.ClientModels;
using PlayFab.Party;
using UnityEngine;
using UnityEngine.Events;

namespace PlayFabSample
{
    [DefaultExecutionOrder(-100)]
    public class PlayFabManager : MonoBehaviour
    {
        [Tooltip("Copy paste your Title ID from https://developer.playfab.com/")]
        public string PlayFabTitleId;

        [Tooltip("PlayFab P2P/Relay Connectivity Options")]
        public PARTY_DIRECT_PEER_CONNECTIVITY_OPTIONS ConnectivityOptions = PARTY_DIRECT_PEER_CONNECTIVITY_OPTIONS.PARTY_DIRECT_PEER_CONNECTIVITY_OPTIONS_ANY_PLATFORM_TYPE | 
                                                                            PARTY_DIRECT_PEER_CONNECTIVITY_OPTIONS.PARTY_DIRECT_PEER_CONNECTIVITY_OPTIONS_ANY_ENTITY_LOGIN_PROVIDER;

        public UnityAction Connected;
        public UnityAction Disconnected;
        public UnityAction LoggedIn;
        public UnityAction<string> NetworkJoined;

        private CoherenceBridge bridge;
        private IReplicationServer replicationServer;
        private EndpointData endpointData;
        
        public bool HasReplicationServer => replicationServer != null;

        public void JoinGame(string networkId, string hostId)
        {
            if (string.IsNullOrEmpty(networkId) || string.IsNullOrEmpty(hostId))
            {
                Debug.LogError("Empty networkId or hostId");
                return;    
            }
            
            bridge.SetRelay(null);
            bridge.SetTransportFactory(new PlayFabTransportFactory(networkId, hostId));
            bridge.Connect(endpointData, new ConnectionSettings()
            {
                Mtu = 1384
            });
        }

        public void HostGame()
        {
            StartReplicationServer();
            
            var playFabMultiplayerManager = PlayFabMultiplayerManager.Get();
            playFabMultiplayerManager.OnNetworkJoined += OnHostNetworkJoined;
            playFabMultiplayerManager.OnError += OnNetworkError;
            
            Debug.Log($"Started Replication Server. Creating PlayFab Network...");
       
            playFabMultiplayerManager.CreateAndJoinNetwork(new PlayFabNetworkConfiguration()
            {
                DirectPeerConnectivityOptions = ConnectivityOptions,
                MaxPlayerCount = 4
            });
        }

        private void OnNetworkError(object sender, PlayFabMultiplayerManagerErrorArgs args)
        {
            if (bridge.IsConnected)
            {
                bridge.Disconnect();
                PlayFabMultiplayerManager.Get().OnNetworkJoined -= OnHostNetworkJoined;
                PlayFabMultiplayerManager.Get().OnError -= OnNetworkError;
            }
        }

        private void OnHostNetworkJoined(object sender, string networkid)
        {
            Debug.Log($"Created PlayFab Network: {networkid}. Connecting to Replication Server...");
            // Init PlayFab Relay
            bridge.SetRelay(new PlayFabRelay(ConnectivityOptions));

            // Connect to Replication Server using the normal UDP transport
            bridge.SetTransportType(TransportType.UDPWithTCPFallback, TransportConfiguration.Default);
            bridge.Connect(endpointData, new ConnectionSettings()
            {
                Mtu = 1384
            });
        }

        private void Awake()
        {
            // Make sure the scene contains a CoherenceBridge
            if (!CoherenceBridgeStore.TryGetBridge(gameObject.scene, out bridge))
            {
                throw new Exception("Could not find a CoherenceBridge in the scene.");
            }

            if (string.IsNullOrEmpty(PlayFabTitleId))
            {
                throw new Exception("PlayFab Title ID is not set.");
            }
            
            InitializePlayFab();

            bridge.onConnected.AddListener(_ => Connected?.Invoke());
            bridge.onDisconnected.AddListener((_, _) => Disconnected?.Invoke());
            PlayFabMultiplayerManager.Get().OnNetworkJoined += (_, networkId) => NetworkJoined?.Invoke(networkId);
            
            InitEndpoint();
        }
        
        private void InitEndpoint()
        {
            endpointData = new EndpointData
            {
                host = RuntimeSettings.Instance.LocalHost,
                port = RuntimeSettings.Instance.LocalWorldUDPPort,
                region = EndpointData.LocalRegion,
                schemaId = RuntimeSettings.Instance.SchemaID,
            };

            // Validate the endpoint
            var (valid, error) = endpointData.Validate();
            if (!valid)
            {
                throw new Exception($"Invalid {nameof(EndpointData)}: {error}");
            }
        }

        private void InitializePlayFab()
        {
            PlayFabSettings.TitleId = PlayFabTitleId;

            var req = new LoginWithCustomIDRequest()
            {
                CreateAccount = true,
                CustomId = Guid.NewGuid().ToString(),
                TitleId = PlayFabTitleId
            };

            PlayFabClientAPI.LoginWithCustomID(req, OnLoggedIn, ErrorCallback);
        }

        private void ErrorCallback(PlayFabError error)
        {
            Debug.LogError($"Failed to login with PlayFab: {error.ErrorMessage}");
        }

        private void OnLoggedIn(LoginResult loginResult)
        {
            Debug.Log($"Logged in with PlayFab: {loginResult.PlayFabId}");
            PlayFabMultiplayerManager.Get().LocalPlayer.IsMuted = true;
            LoggedIn?.Invoke();
        }
        
        private void StartReplicationServer()
        {
            if (replicationServer != null)
            {
                Debug.LogWarning("The replication server is already running");
                return;
            }

            var config = new ReplicationServerConfig
            {
                Mode = Mode.World,
                APIPort = (ushort)RuntimeSettings.Instance.WorldsAPIPort,
                UDPPort = 32001,
                SignallingPort = 32002,
                SendFrequency = 20,
                ReceiveFrequency = 60,
                Token = RuntimeSettings.Instance.ReplicationServerToken,
                DisableThrottling = true,
                AutoShutdownTimeout = 10000 // 10 seconds in milliseconds
            };

            var consoleLogDir = Path.GetDirectoryName(Application.consoleLogPath);
            var logFilePath = Path.Combine(consoleLogDir, "coherence-server");
            replicationServer = Launcher.Create(config, $"--log-file \"{logFilePath}\"");
            replicationServer.OnLog += ReplicationServer_OnLog;
            replicationServer.OnExit += ReplicationServer_OnExit;
            replicationServer.Start();
        }

        private void StopReplicationServer()
        {
            replicationServer?.Stop();
            replicationServer = null;
        }

        private void ReplicationServer_OnLog(string log)
        {
            Debug.Log(log);
        }

        private void ReplicationServer_OnExit(int code)
        {
            Debug.Log($"Replication server exited with code {code}.");
            replicationServer = null;
        }
        
        private void OnDestroy()
        {
            StopReplicationServer();
        }
    }
}

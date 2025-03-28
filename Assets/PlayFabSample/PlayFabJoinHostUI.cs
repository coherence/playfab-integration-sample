using PlayFab.Party;
using PlayFabSample;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayFabJoinHostUI : MonoBehaviour
{
    private PlayFabManager playFabManager;
    
    public TMP_InputField NetworkId;
    public TMP_InputField HostId;
    public Button HostButton;
    public Button JoinButton;
    public GameObject PreGameContainer;
    public GameObject InGameContainer;
    public TextMeshProUGUI InGameNetworkId;
    public TextMeshProUGUI InGameHostId;
    public Button CopyNetworkIdButton;
    public Button CopyHostIdButton;

    void Awake()
    {
        PreGameContainer.SetActive(false);
        InGameContainer.SetActive(false);
    }
    
    void Start()
    {
        playFabManager = FindFirstObjectByType<PlayFabManager>();
        if (!playFabManager)
        {
            Debug.LogWarning("UI requires a PlayFabManager to function");
            return;
        }
        
        HostButton.onClick.AddListener(playFabManager.HostGame);
        JoinButton.onClick.AddListener(() => playFabManager.JoinGame(NetworkId.text, HostId.text));
        CopyNetworkIdButton.onClick.AddListener(() => GUIUtility.systemCopyBuffer = InGameNetworkId.text);
        CopyHostIdButton.onClick.AddListener(() => GUIUtility.systemCopyBuffer = InGameHostId.text);

        playFabManager.Connected += () =>
        {
            PreGameContainer.SetActive(false);
            InGameContainer.SetActive(true);
        };
        
        playFabManager.Disconnected += () =>
        {
            PreGameContainer.SetActive(true);
            InGameContainer.SetActive(false);
        };

        playFabManager.LoggedIn += () =>
        {
            PreGameContainer.SetActive(true);
        };

        playFabManager.NetworkJoined += (networkId) =>
        {
            InGameNetworkId.text = networkId;
            InGameHostId.text = playFabManager.HasReplicationServer
                ? PlayFabMultiplayerManager.Get().LocalPlayer.EntityKey.Id
                : HostId.text;
        };
    }
}

using Coherence.Toolkit;
using UnityEngine;

public class PlayerFactory : MonoBehaviour
{
    [SerializeField] private CoherenceBridge bridge;
    [SerializeField] private GameObject playerPrefab;

    private void Start()
    {
        bridge.onConnected.AddListener(_ =>
        {
            Instantiate(playerPrefab);
        });
    }
}

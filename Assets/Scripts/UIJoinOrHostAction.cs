using UnityEngine;

public class UIJoinOrHostAction : MonoBehaviour
{
    [SerializeField] private GameObject[] objectsToDisable;
    [SerializeField] private TMPro.TextMeshProUGUI statusTextField;
    [SerializeField] private string statusText;

    public void JoinOrHostClicked()
    {
        foreach(var obj in objectsToDisable)
        {
            obj.SetActive(false);
        }
        
        statusTextField.text = statusText;
    }
}

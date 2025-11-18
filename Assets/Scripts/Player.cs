using UnityEngine;

public class Player : MonoBehaviour
{
    public float speed = 1f;

    void Update()
    {
        var h = Input.GetAxisRaw("Horizontal");
        var v = Input.GetAxisRaw("Vertical");
    
        var spf = speed * Time.deltaTime;

        transform.position += transform.forward * (v * spf);
        transform.position += transform.right * (h * spf);
    }
}

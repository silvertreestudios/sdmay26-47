using UnityEngine;

public class DisableVisual : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        foreach (var renderer in GetComponentsInChildren<Renderer>())
        {
            renderer.enabled = false;
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}

using UnityEngine;

public class ParallaxManager : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    [SerializeField] private Transform cam;
    [SerializeField] private ParallaxLayer[] parallaxLayers;

    private Vector3 lastCamPos;

    private void Start()
    {
        if (cam == null)
            cam = Camera.main.transform;

        lastCamPos = cam.position;
    }

    private void LateUpdate()
    {
        Vector3 deltaMovement = cam.position - lastCamPos;

        foreach (ParallaxLayer layer in parallaxLayers)
        {
            layer.Move(deltaMovement);
        }

        lastCamPos = cam.position;
    }
}

using UnityEngine;

public class ParallaxLayer : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created

        public Transform layerTransform;
        public Vector2 parallaxMultiplier;

    public void Awake()
    {
        layerTransform = transform;
    }

    public void Move(Vector3 deltaMovement)
        {
            layerTransform.position += new Vector3(deltaMovement.x * parallaxMultiplier.x, deltaMovement.y * parallaxMultiplier.y, 0f);
        }
    

}

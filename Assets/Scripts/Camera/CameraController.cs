using UnityEngine;
using UnityEngine.Rendering;

public class CameraController : MonoBehaviour
{
    private PlayerController player;
    public BoxCollider2D boundsBox;

    private float halfHeight, halfWidth;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        TryResolvePlayer();

        halfHeight = Camera.main.orthographicSize;
        halfWidth = halfHeight * Camera.main.aspect;
    }

    // Update is called once per frame
    void Update()
    {
        if (player == null)
            TryResolvePlayer();

        if (player != null)
        {
            transform.position = new Vector3
                (
                    Mathf.Clamp(player.transform.position.x, boundsBox.bounds.min.x + halfWidth, boundsBox.bounds.max.x - halfWidth),
                    Mathf.Clamp(player.transform.position.y, boundsBox.bounds.min.y + halfHeight, boundsBox.bounds.max.y - halfHeight), 
                    transform.position.z
                );
        }
    }

    private void TryResolvePlayer()
    {
        player = FindAnyObjectByType<PlayerController>(FindObjectsInactive.Include);
    }
}

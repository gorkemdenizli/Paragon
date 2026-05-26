using UnityEngine;
using TMPro;

public class AbilityUnlock : MonoBehaviour
{
    [SerializeField] private bool unlockDoubleJump;
    [SerializeField] private bool unlockDash;
    [SerializeField] private bool unlockDropBomb;

    [SerializeField] private GameObject pickupEffect;

    [SerializeField] private string unlockMessage;
    [SerializeField] private TMP_Text unlockText;

    private void Start()
    {
        unlockMessage = string.Empty;

        if (unlockDoubleJump)
        {
            unlockMessage += "Unlock Double Jump\n";
        }

        if (unlockDash)
        {
            unlockMessage += "Unlock Dash\n";
        }

        if (unlockDropBomb)
        {
            unlockMessage += "Unlock Bomb\n";
        }

        unlockText.text = unlockMessage;
        unlockText.gameObject.SetActive(true);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerAbilityTracker player = other.GetComponentInParent<PlayerAbilityTracker>();

            if (unlockDoubleJump)
            {
                player.canDoubleJump = true;
            }

            if (unlockDash)
            {
                player.canDash = true;
            }

            if (unlockDropBomb)
            {
                player.canDropBomb = true;
            }

            Instantiate(pickupEffect, transform.position, transform.rotation);

            Destroy(gameObject);
        }
    }
}

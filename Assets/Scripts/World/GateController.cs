using UnityEngine; 
using System.Collections;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class GateController : MonoBehaviour
{
    [SerializeField] private Animator anim;
    [SerializeField] private string levelToLoad;
    [SerializeField] private Transform spawnPoint;

    private PlayerController thePlayer;
    private bool playerExiting;
    private bool playerInRange;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        thePlayer = PlayerHealthController.instance.GetComponent<PlayerController>();
    }

    // Update is kept empty for now; gate opens via interaction
    void Update() 
    { 
        
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = true;
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = false;
        }
    }

    // Called by PlayerController when interact input is pressed
    public void HandlePlayerInteract()
    {
        if (playerExiting)
            return;

        if (!playerInRange)
            return;

        thePlayer.canMove = false;
        anim.SetBool("isGateOpen", true);
        StartCoroutine(UseGateCoroutine());
    }
    
    IEnumerator UseGateCoroutine()
    {
        playerExiting = true;

        UIController.instance.StartFadeToBlack();
        
        yield return new WaitForSeconds(1.5f);

        RespawnController.instance.PrepareGateTransition(spawnPoint.position);

        thePlayer.canMove = true;

        UIController.instance.StartFadeFromBlack();

        SceneManager.LoadScene(levelToLoad);
    }
}

using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class RespawnController : MonoBehaviour
{
    public static RespawnController instance;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
        } 
        else 
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        if (instance == this)
            SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    [SerializeField] private float waitToRespawn = 1f;
    [SerializeField] private GameObject deathEffect;

    private Vector3 respawnPoint;
    private GameObject thePlayer;

    /// <summary>Set by gate before LoadScene; applied once in OnSceneLoaded (no death VFX / wait).</summary>
    private bool pendingGateTeleport;

    private void Start()
    {
        thePlayer = PlayerHealthController.instance.gameObject;
        respawnPoint = thePlayer.transform.position;
    }

    public void SetRespawnPoint(Vector3 newPoint)
    {
        respawnPoint = newPoint;
    }

    /// <summary>Call before <see cref="SceneManager.LoadScene"/> to move the DDOL player after the new scene loads.</summary>
    public void PrepareGateTransition(Vector3 worldSpawn)
    {
        pendingGateTeleport = true;
        respawnPoint = worldSpawn;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!pendingGateTeleport)
            return;

        pendingGateTeleport = false;

        Transform playerTransform = PlayerHealthController.instance.transform;
        playerTransform.position = respawnPoint;
    }

    public void Respawn()
    {
        StartCoroutine(RespawnCo());
    }

    private IEnumerator RespawnCo()
    {
        GameObject playerGo = PlayerHealthController.instance.gameObject;

        playerGo.SetActive(false);
        if (deathEffect != null)
        {
            Instantiate(deathEffect, playerGo.transform.position, playerGo.transform.rotation);
        }

        yield return new WaitForSeconds(waitToRespawn);

        playerGo.transform.position = respawnPoint;
        playerGo.SetActive(true);

        PlayerHealthController.instance.fillHealth();
    }
}

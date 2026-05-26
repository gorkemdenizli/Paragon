using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Runs only on the persistent (DontDestroyOnLoad) player. Rebinds Cinemachine after load
/// and restores Input System actions after duplicate scene players tear down.
/// Add this to the same GameObject as <see cref="PlayerHealthController"/>.
/// </summary>
[RequireComponent(typeof(PlayerHealthController))]
public class PlayerSceneTransitionSupport : MonoBehaviour
{
    private bool subscribed;

    private void Start()
    {
        if (PlayerHealthController.instance == null ||
            PlayerHealthController.instance.gameObject != gameObject)
            return;

        SceneManager.sceneLoaded += OnSceneLoaded;
        subscribed = true;
    }

    private void OnDestroy()
    {
        if (subscribed)
            SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Transform t = transform;
        foreach (CinemachineCamera vcam in FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None))
        {
            if (vcam == null)
                continue;

            CameraTarget ct = vcam.Target;
            ct.TrackingTarget = t;
            vcam.Target = ct;
        }

        StartCoroutine(RestorePlayerInputAfterSceneDuplicateTeardown());
    }

    private IEnumerator RestorePlayerInputAfterSceneDuplicateTeardown()
    {
        yield return null;

        PlayerController pc = GetComponent<PlayerController>();
        if (pc != null)
            pc.RestoreInputAfterSceneLoad();
    }
}

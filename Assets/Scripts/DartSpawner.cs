using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// Pune pe un obiect gol in dreptul "mesei" cu sageti. Spawneaza o sageata noua dupa ce cea curenta e apucata.
public class DartSpawner : MonoBehaviour
{
    public GameObject dartPrefab;
    public int maxDarts = 10;

    int spawned;

    void Start() => SpawnNext();

    void SpawnNext()
    {
        if (spawned >= maxDarts) return;
        spawned++;
        var go = Instantiate(dartPrefab, transform.position, transform.rotation);
        var grab = go.GetComponent<XRGrabInteractable>();
        // La prima apucare, pregatim urmatoarea sageata
        void OnFirstGrab(UnityEngine.XR.Interaction.Toolkit.SelectEnterEventArgs _)
        {
            grab.selectEntered.RemoveListener(OnFirstGrab);
            Invoke(nameof(SpawnNext), 0.8f);
        }
        grab.selectEntered.AddListener(OnFirstGrab);
    }
}

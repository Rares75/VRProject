using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// Pune pe prefab-ul Dart (Rigidbody + Collider + XRGrabInteractable).
[RequireComponent(typeof(Rigidbody), typeof(XRGrabInteractable))]
public class Dart : MonoBehaviour
{
    public bool HasStuck { get; private set; }

    Rigidbody rb;
    XRGrabInteractable grab;
    bool thrown;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        grab = GetComponent<XRGrabInteractable>();
        grab.selectExited.AddListener(_ => thrown = true);   // eliberat = aruncat
        grab.selectEntered.AddListener(_ => thrown = false);
    }

    void FixedUpdate()
    {
        // Sageata se orienteaza dupa directia de zbor
        if (thrown && !HasStuck && rb.linearVelocity.sqrMagnitude > 0.5f)
            rb.MoveRotation(Quaternion.LookRotation(rb.linearVelocity.normalized));
    }

    void OnCollisionEnter(Collision c)
    {
        if (HasStuck || !thrown) return;
        var board = c.collider.GetComponentInParent<DartBoard>();
        if (board == null) return;

        HasStuck = true;
        grab.enabled = false;                 // nu mai poate fi luata
        rb.isKinematic = true;
        transform.SetParent(board.transform, true);
        board.RegisterHit(c.GetContact(0).point);
    }

    // Daca sageata cade pe jos, o stergem dupa cateva secunde
    void Update()
    {
        if (!HasStuck && transform.position.y < -1f) Destroy(gameObject);
    }
}

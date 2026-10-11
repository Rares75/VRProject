using System.Collections;
using UnityEngine;

/// Pune pe tinta. Tinta trebuie sa aiba un Collider; fata tintei = axa Z locala (forward spre jucator: -Z).
public class DartBoard : MonoBehaviour
{
    [Header("Geometrie (in metri, in spatiul local al tintei)")]
    public float boardRadius = 0.225f;        // 45 cm diametru
    public float bullseyeRadius = 0.02f;      // 100 puncte
    public float innerRadius = 0.06f;         // 50 puncte

    [Header("Feedback vizual")]
    public Renderer boardRenderer;
    public Color hitFlashColor = Color.green;
    public ParticleSystem hitParticles;       // optional
    public AudioSource hitAudio;              // optional

    Color originalColor;
    MaterialPropertyBlock mpb;

    void Awake()
    {
        if (boardRenderer != null)
        {
            mpb = new MaterialPropertyBlock();
            originalColor = boardRenderer.sharedMaterial.HasProperty("_BaseColor")
                ? boardRenderer.sharedMaterial.GetColor("_BaseColor") : Color.white;
        }
    }

    public void RegisterHit(Vector3 worldPoint)
    {
        Vector3 local = transform.InverseTransformPoint(worldPoint);
        float dist = new Vector2(local.x, local.y).magnitude * Mathf.Abs(transform.lossyScale.x);

        int score = ComputeScore(dist);
        ScoreManager.Instance.AddScore(score, dist);

        // Feedback
        if (hitParticles != null)
        {
            hitParticles.transform.position = worldPoint;
            hitParticles.Play();
        }
        if (hitAudio != null) hitAudio.Play();
        if (boardRenderer != null) StartCoroutine(Flash(score >= 50 ? Color.yellow : hitFlashColor));
    }

    int ComputeScore(float d)
    {
        if (d <= bullseyeRadius) return 100;
        if (d <= innerRadius)    return 50;
        if (d <= boardRadius * 0.6f) return 25;
        if (d <= boardRadius)    return 10;
        return 0;
    }

    IEnumerator Flash(Color c)
    {
        mpb.SetColor("_BaseColor", c);
        boardRenderer.SetPropertyBlock(mpb);
        yield return new WaitForSeconds(0.25f);
        mpb.SetColor("_BaseColor", originalColor);
        boardRenderer.SetPropertyBlock(mpb);
    }
}

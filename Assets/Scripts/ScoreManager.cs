using TMPro;
using UnityEngine;

/// UI World Space (Canvas) cu doua TextMeshPro: scor total si ultimul hit.
public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }

    public TMP_Text totalText;
    public TMP_Text lastHitText;
    public float popupDuration = 2f;

    int total;
    float hideAt;

    void Awake()
    {
        Instance = this;
        Refresh();
        if (lastHitText) lastHitText.text = "";
    }

    public void AddScore(int points, float distance)
    {
        total += points;
        Refresh();
        if (lastHitText)
        {
            lastHitText.text = points > 0
                ? $"+{points}!  ({distance * 100f:F1} cm de centru)"
                : "Ratat!";
            lastHitText.color = points >= 50 ? Color.yellow : Color.white;
            hideAt = Time.time + popupDuration;
        }
    }

    // Legat la un buton UI (XR Ray Interactor + TrackedDeviceGraphicRaycaster)
    public void ResetScore() { total = 0; Refresh(); }

    void Refresh() { if (totalText) totalText.text = $"Scor: {total}"; }

    void Update()
    {
        if (lastHitText && lastHitText.text != "" && Time.time > hideAt) lastHitText.text = "";
    }
}

// -----------------------------------------------------------------------------
//  HandFingerAnimator
//
//  Mișcă degetele mâinii după două valori 0..1:
//    - grab    -> degetele mijlociu, inelar și mic   (clipul "Grab")
//    - trigger -> degetul arătător                   (clipul "Trigger")
//
//  Clipurile din FBX sunt folosite doar ca "purtători de poză": la pornire scriptul
//  citește poza deschisă (rest) și poza închisă (ultimul cadru) a fiecărui os, apoi
//  în fiecare frame interpolează (Slerp) între ele. Nu are nevoie de Animator Controller.
//
//  Se pune pe obiectul-rădăcină al mâinii (instanța FBX-ului).
// -----------------------------------------------------------------------------
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class HandFingerAnimator : MonoBehaviour
{
    [Header("Clipuri din FBX")]
    public AnimationClip grabClip;
    public AnimationClip triggerClip;

    [Header("Input (XR Interaction Toolkit, valori float 0..1)")]
    [Tooltip("Dacă e bifat, valorile vin din acțiunile de mai jos. Altfel se folosesc slidere-le de test.")]
    public bool useInputActions = false;
    [Tooltip("Ex: XRI LeftHand Interaction / Select Value (grip)")]
    public InputActionProperty gripAction;
    [Tooltip("Ex: XRI LeftHand Interaction / Activate Value (trigger)")]
    public InputActionProperty triggerAction;

    [Header("Test manual (fără căști)")]
    [Range(0f, 1f)] public float grab;
    [Range(0f, 1f)] public float trigger;

    [Tooltip("Cât de repede urmăresc degetele valoarea țintă (mai mare = mai rapid).")]
    [Min(0f)] public float smoothing = 18f;

    class BoneBlend
    {
        public Transform t;
        public Quaternion rest;
        public Quaternion closed;
    }

    readonly List<BoneBlend> grabBones = new List<BoneBlend>();
    readonly List<BoneBlend> triggerBones = new List<BoneBlend>();
    float curGrab, curTrigger;
    bool baked;

    public void SetGrab(float v) { grab = Mathf.Clamp01(v); }
    public void SetTrigger(float v) { trigger = Mathf.Clamp01(v); }

    void Awake()
    {
        Bake();
        var anim = GetComponent<Animator>();
        if (anim != null) anim.enabled = false;   // nu vrem ca Animator-ul să scrie peste oase
    }

    void OnEnable()
    {
        if (!useInputActions) return;
        Enable(gripAction);
        Enable(triggerAction);
    }

    static void Enable(InputActionProperty p)
    {
        var a = p.action;
        if (a != null && !a.enabled) a.Enable();
    }

    // ------------------------------------------------------------------ bake
    void Bake()
    {
        if (baked) return;
        baked = true;

        var all = GetComponentsInChildren<Transform>(true);
        var rp = new Vector3[all.Length];
        var rr = new Quaternion[all.Length];
        var rs = new Vector3[all.Length];
        for (int i = 0; i < all.Length; i++)
        {
            rp[i] = all[i].localPosition;
            rr[i] = all[i].localRotation;
            rs[i] = all[i].localScale;
        }

        BakeClip(grabClip, all, rp, rr, rs, grabBones);
        BakeClip(triggerClip, all, rp, rr, rs, triggerBones);

        if (grabBones.Count == 0 && triggerBones.Count == 0)
            Debug.LogWarning("[HandFingerAnimator] Nu am găsit oase animate. Verifică: FBX > Rig > Animation Type = Generic, " +
                             "FBX > Animation > Import Animation bifat, și că clipurile Grab/Trigger sunt atribuite.", this);
    }

    void BakeClip(AnimationClip clip, Transform[] all, Vector3[] rp, Quaternion[] rr, Vector3[] rs, List<BoneBlend> list)
    {
        if (clip == null) return;

        clip.SampleAnimation(gameObject, clip.length);          // poza "închisă" = ultimul cadru
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == transform) continue;
            if (Quaternion.Angle(rr[i], all[i].localRotation) > 1f)   // doar oasele care chiar se mișcă
                list.Add(new BoneBlend { t = all[i], rest = rr[i], closed = all[i].localRotation });
        }

        for (int i = 0; i < all.Length; i++)                     // înapoi în poza de repaus
        {
            all[i].localPosition = rp[i];
            all[i].localRotation = rr[i];
            all[i].localScale = rs[i];
        }
    }

    // ---------------------------------------------------------------- update
    void LateUpdate()
    {
        float g = grab, t = trigger;
        if (useInputActions)
        {
            if (gripAction.action != null) g = gripAction.action.ReadValue<float>();
            if (triggerAction.action != null) t = triggerAction.action.ReadValue<float>();
        }

        float k = 1f - Mathf.Exp(-smoothing * Time.deltaTime);
        curGrab = Mathf.Lerp(curGrab, Mathf.Clamp01(g), k);
        curTrigger = Mathf.Lerp(curTrigger, Mathf.Clamp01(t), k);

        Apply(grabBones, curGrab);
        Apply(triggerBones, curTrigger);
    }

    static void Apply(List<BoneBlend> bones, float w)
    {
        for (int i = 0; i < bones.Count; i++)
            bones[i].t.localRotation = Quaternion.Slerp(bones[i].rest, bones[i].closed, w);
    }
}

#if UNITY_EDITOR
// -----------------------------------------------------------------------------
//  Tools > VR Hands > Attach Hands To XR Controllers
//
//  1. Setează FBX-ul pe Rig = Generic + Import Animation (pentru oase și clipuri).
//  2. Atașează Hand_R.fbx ca mână DREAPTĂ pe controllerul drept și același model,
//     scalat pe X = -1, ca mână STÂNGĂ pe controllerul stâng.
//  3. Adaugă HandFingerAnimator pe fiecare mână, cu clipurile Grab/Trigger și
//     (dacă le găsește în proiect) acțiunile XRI "Select Value" (grip) și
//     "Activate Value" (trigger).
// -----------------------------------------------------------------------------
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

public class HandsXRSetup : EditorWindow
{
    const string MaterialPath = "Assets/Hands/HandSkin.mat";

    [SerializeField] GameObject handModel;
    [SerializeField] Texture2D albedo;
    [SerializeField] Transform leftController;
    [SerializeField] Transform rightController;

    // Modelul importat din FBX ajunge în Unity cu degetele pe -Z, dosul pe +Y, policele pe +X.
    // Pentru o mână care ține controllerul (degete înainte, deget mare în sus, palma spre interior):
    //   dreapta -> (0, 180, 90)      stânga (oglindită cu scale X = -1) -> (0, 180, -90)
    [SerializeField] Vector3 rightLocalEuler = new Vector3(0f, 180f, 90f);
    [SerializeField] Vector3 leftLocalEuler = new Vector3(0f, 180f, -90f);
    [SerializeField] float handScale = 0.9f;
    [SerializeField] Vector3 localPosition = Vector3.zero;
    [SerializeField] bool hideDefaultControllerModels = true;
    [SerializeField] bool addFingerAnimation = true;

    [MenuItem("Tools/VR Hands/Attach Hands To XR Controllers")]
    static void Open()
    {
        var w = GetWindow<HandsXRSetup>("VR Hands");
        w.minSize = new Vector2(400, 420);
        w.AutoFill();
        w.Show();
    }

    // ------------------------------------------------------------------ UI
    void OnGUI()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Resurse", EditorStyles.boldLabel);
        handModel = (GameObject)EditorGUILayout.ObjectField("Hand model (FBX)", handModel, typeof(GameObject), false);
        albedo = (Texture2D)EditorGUILayout.ObjectField("Albedo texture", albedo, typeof(Texture2D), false);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Controllere din scenă", EditorStyles.boldLabel);
        leftController = (Transform)EditorGUILayout.ObjectField("Left Controller", leftController, typeof(Transform), true);
        rightController = (Transform)EditorGUILayout.ObjectField("Right Controller", rightController, typeof(Transform), true);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Ajustări (local față de controller)", EditorStyles.boldLabel);
        rightLocalEuler = EditorGUILayout.Vector3Field("Rotație mâna dreaptă", rightLocalEuler);
        leftLocalEuler = EditorGUILayout.Vector3Field("Rotație mâna stângă", leftLocalEuler);
        localPosition = EditorGUILayout.Vector3Field("Offset poziție", localPosition);
        handScale = EditorGUILayout.FloatField("Scară mână", handScale);
        hideDefaultControllerModels = EditorGUILayout.Toggle("Ascunde modelul implicit", hideDefaultControllerModels);
        addFingerAnimation = EditorGUILayout.Toggle("Animație degete (grab/trigger)", addFingerAnimation);

        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Auto-detect")) AutoFill();
            using (new EditorGUI.DisabledScope(handModel == null || (leftController == null && rightController == null)))
            {
                if (GUILayout.Button("Attach hands", GUILayout.Height(24))) Apply();
            }
        }
        EditorGUILayout.HelpBox(
            "Mâna stângă = același FBX cu Scale X = -1. Animația citește valorile float ale acțiunilor " +
            "Select Value (grip) și Activate Value (trigger) ale fiecărui controller.",
            MessageType.Info);
    }

    // ------------------------------------------------------------ autodetect
    void AutoFill()
    {
        if (handModel == null)
        {
            var g = AssetDatabase.FindAssets("Hand_R t:Model").FirstOrDefault();
            if (g != null) handModel = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g));
        }
        if (albedo == null)
        {
            var g = AssetDatabase.FindAssets("hand_albedo t:Texture2D").FirstOrDefault();
            if (g != null) albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(g));
        }
        if (leftController == null) leftController = FindController(true);
        if (rightController == null) rightController = FindController(false);
        Repaint();
    }

    static string Norm(string s) => new string(s.ToLowerInvariant().Where(char.IsLetter).ToArray());

    static Transform FindController(bool left)
    {
        string side = left ? "left" : "right";
        string[] wanted = { side + "controller", side + "handcontroller", side + "hand" };
        var all = Resources.FindObjectsOfTypeAll<Transform>()
            .Where(t => t != null && !EditorUtility.IsPersistent(t) && t.gameObject.scene.IsValid())
            .ToArray();
        foreach (var w in wanted)
        {
            var hit = all.FirstOrDefault(t => Norm(t.name) == w);
            if (hit != null) return hit;
        }
        return null;
    }

    // ------------------------------------------------------------------ apply
    void Apply()
    {
        PrepareModelImporter();
        var mat = GetOrCreateMaterial();
        Undo.IncrementCurrentGroup();
        AttachOne(rightController, false, mat);
        AttachOne(leftController, true, mat);
        Undo.SetCurrentGroupName("Attach VR hands");
        var any = rightController != null ? rightController.gameObject : leftController.gameObject;
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(any.scene);
        Debug.Log("[VR Hands] Mâinile au fost atașate la controllere.");
    }

    // FBX: Generic rig + import animații (altfel nu avem oase / clipuri Grab și Trigger)
    void PrepareModelImporter()
    {
        var path = AssetDatabase.GetAssetPath(handModel);
        var imp = AssetImporter.GetAtPath(path) as ModelImporter;
        if (imp == null) return;
        bool changed = false;
        if (imp.animationType != ModelImporterAnimationType.Generic) { imp.animationType = ModelImporterAnimationType.Generic; changed = true; }
        if (!imp.importAnimation) { imp.importAnimation = true; changed = true; }
        if (changed) imp.SaveAndReimport();
    }

    void AttachOne(Transform controller, bool left, Material mat)
    {
        if (controller == null) return;
        string childName = left ? "Hand_L (model)" : "Hand_R (model)";

        var old = controller.Find(childName);
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        var go = (GameObject)PrefabUtility.InstantiatePrefab(handModel, controller);
        Undo.RegisterCreatedObjectUndo(go, "Attach hand");
        go.name = childName;

        var t = go.transform;
        t.localPosition = new Vector3(left ? -localPosition.x : localPosition.x, localPosition.y, localPosition.z);
        t.localRotation = Quaternion.Euler(left ? leftLocalEuler : rightLocalEuler);
        t.localScale = left ? new Vector3(-handScale, handScale, handScale) : Vector3.one * handScale;   // oglindire pentru stânga

        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            Undo.RecordObject(r, "Hand material");
            r.sharedMaterial = mat;
        }

        if (addFingerAnimation) SetupFingerAnimator(go, left);
        if (hideDefaultControllerModels) HideDefaultModel(controller);
    }

    // --------------------------------------------------------- animație degete
    void SetupFingerAnimator(GameObject hand, bool left)
    {
        var comp = hand.GetComponent<HandFingerAnimator>();
        if (comp == null) comp = Undo.AddComponent<HandFingerAnimator>(hand);

        var so = new SerializedObject(comp);
        so.FindProperty("grabClip").objectReferenceValue = FindClip("grab");
        so.FindProperty("triggerClip").objectReferenceValue = FindClip("trigger");

        var gripRef = FindActionRef(left, "select value");
        var trigRef = FindActionRef(left, "activate value");
        SetActionProperty(so.FindProperty("gripAction"), gripRef);
        SetActionProperty(so.FindProperty("triggerAction"), trigRef);
        so.FindProperty("useInputActions").boolValue = gripRef != null && trigRef != null;
        so.ApplyModifiedProperties();

        if (gripRef == null || trigRef == null)
            Debug.LogWarning("[VR Hands] Nu am găsit acțiunile 'Select Value' / 'Activate Value' pentru mâna " +
                             (left ? "stângă" : "dreaptă") + ". Trage-le manual în componenta HandFingerAnimator " +
                             "(Grip Action / Trigger Action) și bifează 'Use Input Actions'.");
    }

    AnimationClip FindClip(string key)
    {
        var path = AssetDatabase.GetAssetPath(handModel);
        return AssetDatabase.LoadAllAssetsAtPath(path)
            .OfType<AnimationClip>()
            .FirstOrDefault(c => !c.name.StartsWith("__preview__") && c.name.ToLowerInvariant().Contains(key));
    }

    static InputActionReference FindActionRef(bool left, string actionName)
    {
        string side = left ? "left" : "right";
        foreach (var guid in AssetDatabase.FindAssets("t:InputActionAsset"))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            foreach (var r in AssetDatabase.LoadAllAssetsAtPath(path).OfType<InputActionReference>())
            {
                var a = r.action;
                if (a == null || a.actionMap == null) continue;
                if (a.name.ToLowerInvariant() != actionName) continue;
                var map = a.actionMap.name.ToLowerInvariant();
                if (map.Contains(side) && map.Contains("interaction") && !map.Contains("haptic"))
                    return r;
            }
        }
        return null;
    }

    static void SetActionProperty(SerializedProperty prop, InputActionReference reference)
    {
        if (prop == null) return;
        var useRef = prop.FindPropertyRelative("m_UseReference");
        var refProp = prop.FindPropertyRelative("m_Reference");
        if (useRef != null) useRef.boolValue = reference != null;
        if (refProp != null) refProp.objectReferenceValue = reference;
    }

    // ------------------------------------------------------ ascunde modele implicite
    static void HideDefaultModel(Transform controller)
    {
        foreach (var c in controller.GetComponents<Component>())
        {
            if (c == null) continue;
            if (!c.GetType().Name.Contains("Controller")) continue;
            var so = new SerializedObject(c);
            var prefab = so.FindProperty("m_ModelPrefab");
            if (prefab != null && prefab.propertyType == SerializedPropertyType.ObjectReference)
            {
                prefab.objectReferenceValue = null;
                so.ApplyModifiedProperties();
            }
        }
        foreach (Transform child in controller)
        {
            if (child.name.StartsWith("Hand_")) continue;
            var n = child.name.ToLowerInvariant();
            if (n.Contains("model parent") || n.Contains("controller model"))
            {
                foreach (var r in child.GetComponentsInChildren<Renderer>(true))
                {
                    Undo.RecordObject(r, "Hide controller model");
                    r.enabled = false;
                }
            }
        }
    }

    // --------------------------------------------------------------- material
    Material GetOrCreateMaterial()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        bool urp = GraphicsSettings.currentRenderPipeline != null;
        Shader sh = urp ? Shader.Find("Universal Render Pipeline/Lit") : Shader.Find("Standard");
        if (sh == null) sh = Shader.Find("Standard");

        if (mat == null)
        {
            mat = new Material(sh) { name = "HandSkin" };
            AssetDatabase.CreateAsset(mat, MaterialPath);
        }
        else if (mat.shader != sh)
        {
            mat.shader = sh;
        }

        if (albedo != null)
        {
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", albedo);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", albedo);
        }
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.38f);
        if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.38f);
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);

        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        return mat;
    }
}
#endif

using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using TMPro;

// Builds Assets/Murals/Leaders/Prefabs/LeadersMural.prefab from the textures in the Textures folder.
// Run from Tools > AR Murals > Build Leaders Prefab. Run again after the real width is measured.
// Portrait titles, stories and audio clips already set on the prefab are kept when rebuilding.
public static class LeadersPrefabBuilder
{
    private const string Folder = "Assets/Murals/Leaders/";
    private const float ImageWidthPx = 1200f;
    private const float ImageHeightPx = 784f;
    private const float DefaultWidthMeters = 4.6f;

    // Guide character. Measure the gap from the bottom of the painting to the floor and update it here.
    private const float FloorGapMeters = 0.25f;
    private const float GuideHeightMeters = 1.75f;
    private static readonly Vector3 GuideSpot = new Vector3(-1.55f, 0.6f, 0f);
    private const string GuideModel = "Models/Guide/Guide.fbx";
    private const string GuideAnimations = "Animations/Guide/";

    // Pixel boxes in the straightened photo: x, y (from the top left), width, height.
    private static readonly Rect[] PortraitBoxes =
    {
        new Rect(665, 60, 165, 230),
        new Rect(855, 90, 190, 240),
        new Rect(875, 322, 120, 148),
        new Rect(1025, 298, 105, 152),
        new Rect(900, 515, 155, 185),
    };

    // Portrait names and stories, in the same order as PortraitBoxes. Confirm wording with guest relations.
    private const string Placeholder = "Ask guest relations";

    private static readonly string[] PortraitNames =
    {
        "Queen Nzinga",
        "Mansa Musa",
        "Thomas Sankara",
        "Kwame Nkrumah",
        "Winnie Madikizela-Mandela",
    };

    private static readonly string[] PortraitStories =
    {
        "Queen Nzinga (Njinga Mbande) ruled the kingdoms of Ndongo and Matamba, in what is now Angola, in the 1600s. "
            + "A skilled diplomat and military leader, she resisted Portuguese colonial expansion for more than thirty years. "
            + "She is remembered as a symbol of African resistance and leadership.",
        "Mansa Musa ruled the Mali Empire in the early 1300s, when it was one of the largest and richest states in the world. "
            + "His pilgrimage to Mecca in 1324 carried so much gold that it became famous across Africa, the Middle East and Europe. "
            + "He built mosques and supported scholars in Timbuktu, helping it become a great centre of learning.",
        "Thomas Sankara led Burkina Faso from 1983 to 1987 and gave the country its name, meaning land of upright people. "
            + "His government ran national vaccination and literacy campaigns, planted millions of trees, and promoted women's rights and self-reliance. "
            + "He was killed in a coup in 1987, aged 37, and remains an icon for young Africans.",
        "Kwame Nkrumah led Ghana to independence from Britain in 1957, one of the first African countries to win freedom from colonial rule. "
            + "As Ghana's first prime minister and president, he championed Pan-Africanism, the idea that Africa should unite. "
            + "He helped found the Organisation of African Unity in 1963.",
        "Winnie Madikizela-Mandela was a South African social worker and anti-apartheid activist. "
            + "While Nelson Mandela spent 27 years in prison, she kept the struggle in the public eye, despite being banned, detained and sent into internal exile. "
            + "Many South Africans call her Mother of the Nation.",
    };

    private static readonly Vector2[] EyeCenters = { new Vector2(490, 535), new Vector2(148, 525) };
    private static readonly float[] EyeRadii = { 45f, 35f };
    private static readonly Rect MaskBox = new Rect(0, 0, 640, 784);

    private enum BlendType { Opaque, Transparent, Additive }

    private static float widthMeters;
    private static float heightMeters;

    [MenuItem("Tools/AR Murals/Build Leaders Prefab")]
    public static void Build()
    {
        if (TMP_Settings.instance == null || TMP_Settings.defaultFontAsset == null)
        {
            Debug.LogError("Import TMP Essential Resources first: Window > TextMeshPro > Import TMP Essential Resources.");
            return;
        }

        MuralData data = AssetDatabase.LoadAssetAtPath<MuralData>(Folder + "Data/LeadersData.asset");
        widthMeters = (data != null && data.widthMeters > 0f) ? data.widthMeters : DefaultWidthMeters;
        heightMeters = widthMeters * ImageHeightPx / ImageWidthPx;

        PrepareTextures();

        Material photoMat = MakeMaterial("M_Photo", "Leaders_Photo.jpg", BlendType.Opaque, false);
        Material veinsMat = MakeMaterial("M_Veins", "Leaders_Veins.png", BlendType.Additive, false);
        Material glowMat = MakeMaterial("M_Glow", "Glow.png", BlendType.Additive, false);
        Material shadowMat = MakeMaterial("M_Shadow", "Shadow.png", BlendType.Transparent, false);
        Material dustMat = MakeMaterial("M_Dust", "SoftDot.png", BlendType.Additive, true);

        GameObject root = new GameObject("LeadersMural");
        LeadersMural mural = root.AddComponent<LeadersMural>();
        mural.data = data;

        AudioSource ambient = root.AddComponent<AudioSource>();
        ambient.playOnAwake = false;
        ambient.loop = true;

        GameObject voice = new GameObject("Voice");
        voice.transform.SetParent(root.transform, false);
        mural.voiceSource = voice.AddComponent<AudioSource>();
        mural.voiceSource.playOnAwake = false;

        // Photo of the mural for checking alignment in the editor. Stays turned off.
        GameObject photo = MakeQuad("PhotoQuad", root.transform, Vector3.zero, new Vector2(widthMeters, heightMeters), photoMat);
        photo.SetActive(false);

        mural.veins = MakeQuad("Veins", root.transform, new Vector3(0f, 0.003f, 0f),
            new Vector2(widthMeters, heightMeters), veinsMat).GetComponent<Renderer>();

        // Mask: a large tap area over the left side, plus a glow on each eye.
        GameObject mask = new GameObject("Mask");
        mask.transform.SetParent(root.transform, false);
        BoxCollider maskCollider = mask.AddComponent<BoxCollider>();
        maskCollider.center = PixelToLocal(MaskBox.center);
        maskCollider.size = new Vector3(MaskBox.width * MetersPerPixel(), 0.02f, MaskBox.height * MetersPerPixel());
        MuralTappable maskTap = mask.AddComponent<MuralTappable>();
        maskTap.id = "Mask";

        mural.maskEyes = new Transform[EyeCenters.Length];
        for (int i = 0; i < EyeCenters.Length; i++)
        {
            float size = EyeRadii[i] * 2f * 2.2f * MetersPerPixel();
            Vector3 position = PixelToLocal(EyeCenters[i]) + new Vector3(0f, 0.004f, 0f);
            mural.maskEyes[i] = MakeQuad("Eye" + (i + 1), mask.transform, position, new Vector2(size, size), glowMat).transform;
        }

        // Portraits
        GameObject group = new GameObject("Portraits");
        group.transform.SetParent(root.transform, false);
        mural.portraits = new LeadersMural.Portrait[PortraitBoxes.Length];

        for (int i = 0; i < PortraitBoxes.Length; i++)
        {
            Rect box = PortraitBoxes[i];
            Vector2 size = new Vector2(box.width * MetersPerPixel(), box.height * MetersPerPixel());

            GameObject portraitRoot = new GameObject("Portrait" + (i + 1));
            portraitRoot.transform.SetParent(group.transform, false);
            portraitRoot.transform.localPosition = PixelToLocal(box.center);

            LeadersMural.Portrait p = new LeadersMural.Portrait();
            p.title = PortraitNames[i];
            p.story = PortraitStories[i];

            p.shadow = MakeQuad("Shadow", portraitRoot.transform, new Vector3(0f, 0.002f, 0f), size * 1.05f, shadowMat).GetComponent<Renderer>();

            GameObject lift = new GameObject("Lift");
            lift.transform.SetParent(portraitRoot.transform, false);
            p.lift = lift.transform;

            GameObject floater = new GameObject("Float");
            floater.transform.SetParent(lift.transform, false);
            p.floater = floater.transform;

            p.glow = MakeQuad("Glow", floater.transform, new Vector3(0f, 0.003f, 0f), size * 1.6f, glowMat).GetComponent<Renderer>();

            Material figureMat = MakeMaterial("M_Portrait" + (i + 1), "Portrait" + (i + 1) + ".png", BlendType.Transparent, false);
            GameObject figure = MakeQuad("Figure", floater.transform, new Vector3(0f, 0.006f, 0f), size, figureMat);
            BoxCollider figureCollider = figure.AddComponent<BoxCollider>();
            figureCollider.size = new Vector3(1f, 1f, 0.02f);
            MuralTappable tap = figure.AddComponent<MuralTappable>();
            tap.id = "Portrait";
            tap.index = i;
            p.figure = figure.GetComponent<Renderer>();

            p.dust = MakeDust("Dust", portraitRoot.transform, new Vector3(size.x, size.y, 0.01f), false, dustMat);

            mural.portraits[i] = p;
        }

        mural.ambientDust = MakeDust("AmbientDust", root.transform,
            new Vector3(widthMeters * 0.45f, heightMeters * 0.9f, 0.05f), true, dustMat);
        mural.ambientDust.transform.localPosition = new Vector3(widthMeters * 0.25f, 0.05f, 0f);

        BuildCard(root.transform, mural);
        mural.extras = AddExtras(root.transform);
        BuildGuide(root.transform, mural, shadowMat);

        string prefabPath = Folder + "Prefabs/LeadersMural.prefab";
        KeepFilledInFields(prefabPath, mural);
        AssignAudio(mural);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);

        if (data != null)
        {
            data.prefab = prefab;
            data.widthMeters = widthMeters;
            data.heightMeters = heightMeters;
            if (data.captions == null || data.captions.Length < 4)
            {
                data.captions = new string[]
                {
                    "Watch the portraits lift out of the wall.",
                    "Tap a portrait to hear their story. Tap the mask to wake them all.",
                    "The mask wakes the portraits.",
                    "You have heard every story.",
                };
            }
            if (string.IsNullOrEmpty(data.scanHint))
            {
                data.scanHint = "Stand 3 to 4 m in front of the mural so the whole painting fits on the screen.";
            }
            EditorUtility.SetDirty(data);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("Leaders prefab built at " + prefabPath + " with width " + widthMeters + " m.");
    }

    // ---------- Guide character ----------

    private static void BuildGuide(Transform parent, LeadersMural mural, Material shadowMat)
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + GuideModel);
        if (model == null)
        {
            Debug.LogWarning("No guide model at " + Folder + GuideModel + ". Building without the guide.");
            return;
        }

        SetupGuideImports();

        // Stands on the floor in front of the mask. Faces the viewer (+Y) with its head up (+Z).
        GameObject holder = new GameObject("Guide");
        holder.transform.SetParent(parent, false);
        holder.transform.localPosition = new Vector3(GuideSpot.x, GuideSpot.y, -heightMeters / 2f - FloorGapMeters);
        holder.transform.localRotation = Quaternion.LookRotation(Vector3.up, Vector3.forward);

        GameObject body = (GameObject)PrefabUtility.InstantiatePrefab(model);
        body.name = "Body";
        float modelHeight = MeasureHeight(body);
        body.transform.SetParent(holder.transform, false);
        body.transform.localPosition = Vector3.zero;
        body.transform.localRotation = Quaternion.identity;
        body.transform.localScale = Vector3.one * (GuideHeightMeters / modelHeight);

        Animator animator = body.GetComponent<Animator>();
        if (animator == null)
        {
            animator = body.AddComponent<Animator>();
        }
        animator.runtimeAnimatorController = BuildGuideController();
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        CapsuleCollider capsule = holder.AddComponent<CapsuleCollider>();
        capsule.direction = 1;
        capsule.height = GuideHeightMeters;
        capsule.radius = 0.3f;
        capsule.center = new Vector3(0f, GuideHeightMeters / 2f, 0f);
        MuralTappable tap = holder.AddComponent<MuralTappable>();
        tap.id = "Guide";

        // Soft shadow on the floor under his feet.
        GameObject shadow = GameObject.CreatePrimitive(PrimitiveType.Quad);
        shadow.name = "FloorShadow";
        Object.DestroyImmediate(shadow.GetComponent<MeshCollider>());
        shadow.transform.SetParent(holder.transform, false);
        shadow.transform.localPosition = new Vector3(0f, 0.01f, 0f);
        shadow.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        shadow.transform.localScale = new Vector3(0.9f, 0.9f, 1f);
        shadow.GetComponent<Renderer>().sharedMaterial = shadowMat;

        mural.guide = holder.transform;
        mural.guideAnimator = animator;
        mural.guideLines = new AudioClip[2];
        mural.guideCaptions = new string[]
        {
            "Welcome. Tap any portrait and I will tell you their story.",
            "Tap the mask on the left to wake all the portraits at once.",
        };
    }

    private static float MeasureHeight(GameObject body)
    {
        Bounds bounds = new Bounds(body.transform.position, Vector3.zero);
        foreach (Renderer rend in body.GetComponentsInChildren<Renderer>())
        {
            bounds.Encapsulate(rend.bounds);
        }
        return bounds.size.y > 0.01f ? bounds.size.y : GuideHeightMeters;
    }

    // Generic rig for the model and every clip. Idle and Talking loop, Wave and Point play once.
    private static void SetupGuideImports()
    {
        string[] paths =
        {
            Folder + GuideModel,
            Folder + GuideAnimations + "Guide@Idle.fbx",
            Folder + GuideAnimations + "Guide@Wave.fbx",
            Folder + GuideAnimations + "Guide@Point.fbx",
        };

        foreach (string path in paths)
        {
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
            {
                continue;
            }

            bool loop = !path.Contains("@Wave") && !path.Contains("@Point");
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;

            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            foreach (ModelImporterClipAnimation clip in clips)
            {
                clip.loopTime = loop;
            }
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }

        // Pulls the textures out of the model file so the materials can find them.
        string textureFolder = Folder + "Models/Guide/Textures";
        if (!AssetDatabase.IsValidFolder(textureFolder))
        {
            AssetDatabase.CreateFolder(Folder + "Models/Guide", "Textures");
            ModelImporter modelImporter = (ModelImporter)AssetImporter.GetAtPath(Folder + GuideModel);
            modelImporter.ExtractTextures(textureFolder);
            AssetDatabase.Refresh();
        }
    }

    private static AnimationClip LoadClip(string path)
    {
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            AnimationClip clip = asset as AnimationClip;
            if (clip != null && !clip.name.StartsWith("__preview__"))
            {
                return clip;
            }
        }
        return null;
    }

    // Idle by default, Talking while the bool is on, Wave and Point play once from any state.
    private static RuntimeAnimatorController BuildGuideController()
    {
        AnimationClip talk = LoadClip(Folder + GuideModel);
        AnimationClip idle = LoadClip(Folder + GuideAnimations + "Guide@Idle.fbx");
        AnimationClip wave = LoadClip(Folder + GuideAnimations + "Guide@Wave.fbx");
        AnimationClip point = LoadClip(Folder + GuideAnimations + "Guide@Point.fbx");

        string path = Folder + GuideAnimations + "GuideAnimator.controller";
        AssetDatabase.DeleteAsset(path);
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        controller.AddParameter("Talking", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Wave", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Point", AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine machine = controller.layers[0].stateMachine;

        // Until an idle clip is added, a slowed-down talking clip stands in for it.
        AnimatorState idleState = machine.AddState("Idle");
        idleState.motion = idle != null ? idle : talk;
        if (idle == null)
        {
            idleState.speed = 0.3f;
        }
        machine.defaultState = idleState;

        AnimatorState talkState = machine.AddState("Talking");
        talkState.motion = talk;

        AnimatorStateTransition toTalk = idleState.AddTransition(talkState);
        toTalk.hasExitTime = false;
        toTalk.duration = 0.25f;
        toTalk.AddCondition(AnimatorConditionMode.If, 0f, "Talking");

        AnimatorStateTransition toIdle = talkState.AddTransition(idleState);
        toIdle.hasExitTime = false;
        toIdle.duration = 0.3f;
        toIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "Talking");

        AddOneShot(machine, "Wave", wave, idleState);
        AddOneShot(machine, "Point", point, idleState);
        return controller;
    }

    private static void AddOneShot(AnimatorStateMachine machine, string name, AnimationClip clip, AnimatorState returnState)
    {
        if (clip == null)
        {
            return;
        }

        AnimatorState state = machine.AddState(name);
        state.motion = clip;

        AnimatorStateTransition enter = machine.AddAnyStateTransition(state);
        enter.hasExitTime = false;
        enter.duration = 0.2f;
        enter.canTransitionToSelf = false;
        enter.AddCondition(AnimatorConditionMode.If, 0f, name);

        AnimatorStateTransition exit = state.AddTransition(returnState);
        exit.hasExitTime = true;
        exit.exitTime = 0.9f;
        exit.duration = 0.25f;
    }

    // Extra models live in their own prefab, nested inside the mural, so rebuilding never deletes them.
    private static Transform AddExtras(Transform parent)
    {
        string path = Folder + "Prefabs/LeadersExtras.prefab";
        GameObject extrasPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (extrasPrefab == null)
        {
            GameObject empty = new GameObject("LeadersExtras");
            extrasPrefab = PrefabUtility.SaveAsPrefabAsset(empty, path);
            Object.DestroyImmediate(empty);
        }

        GameObject extras = (GameObject)PrefabUtility.InstantiatePrefab(extrasPrefab, parent);
        extras.name = "LeadersExtras";
        return extras.transform;
    }

    // Editor test scene: the mural stands up in front of a camera, with mouse taps enabled.
    [MenuItem("Tools/AR Murals/Build Leaders Preview Scene")]
    public static void BuildPreviewScene()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "Prefabs/LeadersMural.prefab");
        if (prefab == null)
        {
            Debug.LogError("Build the Leaders prefab first.");
            return;
        }

        if (!UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        UnityEngine.SceneManagement.Scene scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(
            UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);

        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        Camera cam = cameraObject.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.55f, 0.5f, 0.45f);
        // Slightly to the side so the portraits can be seen lifting out of the wall.
        cameraObject.transform.position = new Vector3(-0.6f, -0.6f, -4.4f);
        cameraObject.transform.LookAt(new Vector3(0.1f, -0.4f, 0f));
        cameraObject.AddComponent<TapInput>();
        cameraObject.AddComponent<AudioListener>();

        GameObject infoPanel = new GameObject("MuralInfoPanel");
        infoPanel.AddComponent<MuralInfoPanel>();

        // Rotated so the mural's +Y (out of the wall) faces the camera and +Z points up.
        GameObject mural = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        mural.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);

        // Shows the mural photo behind the content, standing in for the real wall. Only in this scene.
        mural.transform.Find("PhotoQuad").gameObject.SetActive(true);

        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, Folder + "Preview_Leaders.unity");
        Selection.activeGameObject = mural;
        Debug.Log("Preview scene ready. Press Play, then use Test: Tracking Found on the Leaders Mural component.");
    }

    // ---------- Layout ----------

    private static float MetersPerPixel()
    {
        return widthMeters / ImageWidthPx;
    }

    // Pixel in the straightened photo to a local position on the mural (X right, Z up, Y out of the wall).
    private static Vector3 PixelToLocal(Vector2 pixel)
    {
        float x = (pixel.x / ImageWidthPx - 0.5f) * widthMeters;
        float z = (0.5f - pixel.y / ImageHeightPx) * heightMeters;
        return new Vector3(x, 0f, z);
    }

    // A quad lying flat on the mural, facing out of the wall, with its top towards +Z.
    private static GameObject MakeQuad(string name, Transform parent, Vector3 position, Vector2 size, Material material)
    {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = name;
        Object.DestroyImmediate(quad.GetComponent<MeshCollider>());
        quad.transform.SetParent(parent, false);
        quad.transform.localPosition = position;
        quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        quad.transform.localScale = new Vector3(size.x, size.y, 1f);
        Renderer rend = quad.GetComponent<Renderer>();
        rend.sharedMaterial = material;
        rend.shadowCastingMode = ShadowCastingMode.Off;
        rend.receiveShadows = false;
        return quad;
    }

    // ---------- Particles ----------

    private static ParticleSystem MakeDust(string name, Transform parent, Vector3 boxSize, bool loop, Material material)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = ps.main;
        main.playOnAwake = false;
        main.loop = loop;
        main.duration = loop ? 5f : 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.08f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.01f, 0.03f);
        main.startColor = new Color(1f, 0.82f, 0.45f, 1f);
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = 200;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = loop ? 12f : 0f;
        if (!loop)
        {
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 40) });
        }

        // The box emits along its +Z, rotated so particles drift out of the wall (+Y).
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = boxSize;
        shape.rotation = new Vector3(-90f, 0f, 0f);

        ParticleSystem.ColorOverLifetimeModule color = ps.colorOverLifetime;
        color.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
        color.color = gradient;

        ParticleSystemRenderer psRenderer = go.GetComponent<ParticleSystemRenderer>();
        psRenderer.sharedMaterial = material;
        psRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        return ps;
    }

    // ---------- Info card ----------

    private static void BuildCard(Transform parent, LeadersMural mural)
    {
        GameObject card = new GameObject("InfoCard", typeof(RectTransform), typeof(Canvas));
        card.transform.SetParent(parent, false);
        card.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        RectTransform cardRect = card.GetComponent<RectTransform>();
        cardRect.sizeDelta = new Vector2(900f, 150f);
        cardRect.localScale = Vector3.one * 0.001f;
        cardRect.localRotation = Quaternion.Euler(90f, 0f, 0f);

        GameObject background = new GameObject("Background", typeof(RectTransform), typeof(Image));
        background.transform.SetParent(card.transform, false);
        Stretch(background.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
        background.GetComponent<Image>().color = new Color(0.04f, 0.06f, 0.09f, 0.88f);

        // Name only, large enough to read from 3 to 4 m. The story is shown on screen instead.
        mural.cardTitle = MakeText("Title", card.transform, 96f, FontStyles.Bold, new Color(1f, 0.82f, 0.45f));
        mural.cardTitle.alignment = TextAlignmentOptions.Center;
        mural.cardTitle.enableAutoSizing = true;
        mural.cardTitle.fontSizeMin = 48f;
        mural.cardTitle.fontSizeMax = 96f;
        Stretch(mural.cardTitle.rectTransform, 20f, 10f, 20f, 10f);

        mural.infoCard = card;
        card.SetActive(false);
    }

    private static TMP_Text MakeText(string name, Transform parent, float size, FontStyles style, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        text.fontSize = size;
        text.fontStyle = style;
        text.color = color;
        text.text = name;
        return text;
    }

    // Anchors a rect to fill its parent with the given padding (left, top, right, bottom).
    private static void Stretch(RectTransform rect, float left, float top, float right, float bottom)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    // ---------- Assets ----------

    private static void PrepareTextures()
    {
        string[] names = { "Leaders_Photo.jpg", "Leaders_Veins.png", "Glow.png", "Shadow.png", "SoftDot.png",
                           "Portrait1.png", "Portrait2.png", "Portrait3.png", "Portrait4.png", "Portrait5.png" };
        foreach (string textureName in names)
        {
            TextureImporter importer = AssetImporter.GetAtPath(Folder + "Textures/" + textureName) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning("Missing texture: " + textureName);
                continue;
            }
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = textureName.EndsWith(".png");
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
        }
    }

    private static Material MakeMaterial(string name, string textureName, BlendType blend, bool particles)
    {
        string path = Folder + "Materials/" + name + ".mat";
        string shaderName = particles ? "Universal Render Pipeline/Particles/Unlit" : "Universal Render Pipeline/Unlit";

        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find(shaderName));
            AssetDatabase.CreateAsset(material, path);
        }

        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "Textures/" + textureName));
        material.SetColor("_BaseColor", Color.white);

        if (blend == BlendType.Opaque)
        {
            material.SetFloat("_Surface", 0f);
            material.SetOverrideTag("RenderType", "Opaque");
            material.SetFloat("_SrcBlend", (float)BlendMode.One);
            material.SetFloat("_DstBlend", (float)BlendMode.Zero);
            material.SetFloat("_ZWrite", 1f);
            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Geometry;
        }
        else
        {
            bool additive = blend == BlendType.Additive;
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", additive ? 2f : 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
        }

        EditorUtility.SetDirty(material);
        return material;
    }

    // Connects voice files from the Audio folder by name, unless a clip was already set by hand.
    private static readonly string[] PortraitAudio =
    {
        "Portrait1_Nzinga", "Portrait2_MansaMusa", "Portrait3_Sankara", "Portrait4_Nkrumah", "Portrait5_Winnie",
    };

    private static void AssignAudio(LeadersMural mural)
    {
        for (int i = 0; i < mural.portraits.Length && i < PortraitAudio.Length; i++)
        {
            if (mural.portraits[i].narration == null)
            {
                mural.portraits[i].narration = LoadVoice(PortraitAudio[i]);
            }
        }

        if (mural.guideLines == null)
        {
            return;
        }
        for (int i = 0; i < mural.guideLines.Length; i++)
        {
            if (mural.guideLines[i] == null)
            {
                mural.guideLines[i] = LoadVoice("Guide_Line" + (i + 1));
            }
        }
    }

    // Voice settings for mobile: mono, compressed in memory.
    private static AudioClip LoadVoice(string fileName)
    {
        string path = Folder + "Audio/" + fileName + ".mp3";
        AudioImporter importer = AssetImporter.GetAtPath(path) as AudioImporter;
        if (importer == null)
        {
            return null;
        }

        importer.forceToMono = true;
        AudioImporterSampleSettings settings = importer.defaultSampleSettings;
        settings.loadType = AudioClipLoadType.CompressedInMemory;
        settings.compressionFormat = AudioCompressionFormat.Vorbis;
        settings.quality = 0.7f;
        importer.defaultSampleSettings = settings;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }

    // Copies titles, stories and audio clips from the old prefab so a rebuild does not wipe them.
    private static void KeepFilledInFields(string prefabPath, LeadersMural mural)
    {
        GameObject oldPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (oldPrefab == null)
        {
            return;
        }

        LeadersMural old = oldPrefab.GetComponent<LeadersMural>();
        if (old == null)
        {
            return;
        }

        mural.ambientLoop = old.ambientLoop;
        mural.riseClip = old.riseClip;
        mural.selectClip = old.selectClip;
        mural.maskClip = old.maskClip;
        mural.completeClip = old.completeClip;
        if (old.guideLines != null && old.guideLines.Length > 0)
        {
            mural.guideLines = old.guideLines;
        }
        if (old.guideCaptions != null && old.guideCaptions.Length > 0)
        {
            mural.guideCaptions = old.guideCaptions;
        }

        int count = Mathf.Min(old.portraits.Length, mural.portraits.Length);
        for (int i = 0; i < count; i++)
        {
            if (old.portraits[i].title != Placeholder)
            {
                mural.portraits[i].title = old.portraits[i].title;
            }
            if (old.portraits[i].story != Placeholder)
            {
                mural.portraits[i].story = old.portraits[i].story;
            }
            mural.portraits[i].narration = old.portraits[i].narration;
        }
    }
}

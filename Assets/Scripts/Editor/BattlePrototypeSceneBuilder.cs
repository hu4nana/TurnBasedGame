using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BattlePrototypeSceneBuilder
{
    private const string PrototypeRootName = "SRPG Battle Prototype";
    private const string PlaceholderAssetPath = "Assets/Prototype/PlaceholderCharacter.asset";

    [MenuItem("Tools/Turn Based Game/Build HD-2D Grid Prototype")]
    public static void Build()
    {
        GameObject previous = GameObject.Find(PrototypeRootName);
        if (previous != null) Object.DestroyImmediate(previous);

        Sprite placeholderSprite = GetOrCreatePlaceholderSprite();
        GameObject root = new(PrototypeRootName);
        GameObject boardObject = new("Battle Field");
        boardObject.transform.SetParent(root.transform, false);
        boardObject.transform.position = new Vector3(0f, 0.2f, 0f);

        BattleField board = boardObject.AddComponent<BattleField>();
        BattleFieldView view = boardObject.AddComponent<BattleFieldView>();
        TacticalStateController stateController = boardObject.AddComponent<TacticalStateController>();
        view.Rebuild();

        Camera camera = ConfigureCamera();
        BattleCameraController cameraController = camera.GetComponent<BattleCameraController>();
        if (cameraController == null) cameraController = camera.gameObject.AddComponent<BattleCameraController>();
        cameraController.Configure(new Vector3(0f, 0.5f, 0f));

        TimingInputController timingInput = CreateTimingHud(root.transform);
        CombatFocusDirector focusDirector = boardObject.AddComponent<CombatFocusDirector>();
        focusDirector.Configure(view, stateController, timingInput, cameraController);
        boardObject.AddComponent<BattleFieldInput>();

        CreateCharacter(boardObject.transform, board, placeholderSprite,
            "Player Vanguard", BattleFaction.Player, 3, 2, new GridCoordinate(3, 3));
        CreateCharacter(boardObject.transform, board, placeholderSprite,
            "Enemy Vanguard", BattleFaction.Enemy, 3, 2, new GridCoordinate(5, 3));
        CreateCharacter(boardObject.transform, board, placeholderSprite,
            "Player Scout", BattleFaction.Player, 4, 1, new GridCoordinate(1, 1));
        CreateCharacter(boardObject.transform, board, placeholderSprite,
            "Enemy Scout", BattleFaction.Enemy, 4, 1, new GridCoordinate(8, 6));

        stateController.RecalculateAll();
        ConfigureLight();

        Selection.activeGameObject = boardObject;
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
    }

    private static void CreateCharacter(
        Transform parent,
        BattleField board,
        Sprite sprite,
        string characterName,
        BattleFaction faction,
        int moveRange,
        int threatRange,
        GridCoordinate coordinate)
    {
        GameObject character = new(characterName);
        character.transform.SetParent(parent, true);

        GridOccupant occupant = character.AddComponent<GridOccupant>();
        occupant.Configure(faction, moveRange, threatRange);

        GameObject visualRoot = new("Visual Root");
        visualRoot.transform.SetParent(character.transform, false);

        GameObject spriteObject = new("Character Sprite");
        spriteObject.transform.SetParent(visualRoot.transform, false);
        spriteObject.transform.localPosition = new Vector3(0f, 0.08f, 0f);
        SpriteRenderer spriteRenderer = spriteObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = sprite;

        GameObject shadow = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        shadow.name = "Blob Shadow";
        shadow.transform.SetParent(character.transform, false);
        shadow.transform.localPosition = new Vector3(0f, 0.015f, 0f);
        shadow.transform.localScale = new Vector3(0.6f, 0.018f, 0.35f);
        Object.DestroyImmediate(shadow.GetComponent<Collider>());

        GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ring.name = "Tactical State Ring";
        ring.transform.SetParent(character.transform, false);
        ring.transform.localPosition = new Vector3(0f, 0.025f, 0f);
        ring.transform.localScale = new Vector3(0.72f, 0.012f, 0.72f);
        Object.DestroyImmediate(ring.GetComponent<Collider>());

        GridOccupantVisualizer visualizer = character.AddComponent<GridOccupantVisualizer>();
        visualizer.Configure(
            visualRoot.transform,
            spriteRenderer,
            shadow.GetComponent<Renderer>(),
            ring.GetComponent<Renderer>());

        if (!board.TryPlace(occupant, coordinate))
            Debug.LogError($"Could not place {characterName} at {coordinate}.");
    }

    private static TimingInputController CreateTimingHud(Transform parent)
    {
        GameObject canvasObject = new("Battle HUD");
        canvasObject.transform.SetParent(parent, false);
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        UnityEngine.UI.CanvasScaler scaler = canvasObject.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        GameObject panelObject = CreateUiImage("Timing Panel", canvasObject.transform,
            new Color(0.025f, 0.03f, 0.04f, 0.94f));
        RectTransform panel = panelObject.GetComponent<RectTransform>();
        panel.anchorMin = new Vector2(0.5f, 0f);
        panel.anchorMax = new Vector2(0.5f, 0f);
        panel.pivot = new Vector2(0.5f, 0f);
        panel.anchoredPosition = new Vector2(0f, 80f);
        panel.sizeDelta = new Vector2(640f, 92f);
        CanvasGroup panelGroup = panelObject.AddComponent<CanvasGroup>();

        GameObject trackObject = CreateUiImage("Timing Track", panelObject.transform,
            new Color(0.14f, 0.16f, 0.2f, 1f));
        RectTransform track = trackObject.GetComponent<RectTransform>();
        SetCentered(track, new Vector2(540f, 22f));

        GameObject goodZoneObject = CreateUiImage("Good Zone", trackObject.transform,
            new Color(0.2f, 0.9f, 0.45f, 0.72f));
        RectTransform goodZone = goodZoneObject.GetComponent<RectTransform>();
        SetCentered(goodZone, new Vector2(173f, 22f));

        GameObject perfectZoneObject = CreateUiImage("Perfect Zone", trackObject.transform,
            new Color(1f, 0.82f, 0.18f, 0.95f));
        RectTransform perfectZone = perfectZoneObject.GetComponent<RectTransform>();
        SetCentered(perfectZone, new Vector2(59f, 30f));

        GameObject markerObject = CreateUiImage("Timing Marker", trackObject.transform, Color.white);
        RectTransform marker = markerObject.GetComponent<RectTransform>();
        SetCentered(marker, new Vector2(10f, 46f));

        GameObject flashObject = CreateUiImage("Result Flash", panelObject.transform, Color.clear);
        RectTransform flash = flashObject.GetComponent<RectTransform>();
        flash.anchorMin = Vector2.zero;
        flash.anchorMax = Vector2.one;
        flash.offsetMin = Vector2.zero;
        flash.offsetMax = Vector2.zero;
        flashObject.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;

        TimingInputController controller = panelObject.AddComponent<TimingInputController>();
        controller.Configure(panelGroup, track, marker, flashObject.GetComponent<UnityEngine.UI.Image>());
        return controller;
    }

    private static GameObject CreateUiImage(string name, Transform parent, Color color)
    {
        GameObject target = new(name, typeof(RectTransform));
        target.transform.SetParent(parent, false);
        UnityEngine.UI.Image image = target.AddComponent<UnityEngine.UI.Image>();
        image.color = color;
        image.raycastTarget = false;
        return target;
    }

    private static void SetCentered(RectTransform target, Vector2 size)
    {
        target.anchorMin = new Vector2(0.5f, 0.5f);
        target.anchorMax = new Vector2(0.5f, 0.5f);
        target.pivot = new Vector2(0.5f, 0.5f);
        target.anchoredPosition = Vector2.zero;
        target.sizeDelta = size;
    }

    private static Sprite GetOrCreatePlaceholderSprite()
    {
        Object[] existing = AssetDatabase.LoadAllAssetsAtPath(PlaceholderAssetPath);
        foreach (Object asset in existing)
            if (asset is Sprite sprite) return sprite;

        if (!AssetDatabase.IsValidFolder("Assets/Prototype"))
            AssetDatabase.CreateFolder("Assets", "Prototype");

        Texture2D texture = new(16, 24, TextureFormat.RGBA32, false)
        {
            name = "PlaceholderCharacterTexture",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        Color[] pixels = new Color[16 * 24];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.clear;

        PaintRect(pixels, 16, 5, 16, 10, 22, Color.white);
        PaintRect(pixels, 16, 4, 6, 11, 16, Color.white);
        PaintRect(pixels, 16, 2, 8, 13, 13, Color.white);
        PaintRect(pixels, 16, 5, 1, 7, 8, Color.white);
        PaintRect(pixels, 16, 9, 1, 11, 8, Color.white);
        texture.SetPixels(pixels);
        texture.Apply();

        AssetDatabase.CreateAsset(texture, PlaceholderAssetPath);
        Sprite created = Sprite.Create(texture, new Rect(0f, 0f, 16f, 24f),
            new Vector2(0.5f, 0f), 16f, 0, SpriteMeshType.FullRect);
        created.name = "PlaceholderCharacterSprite";
        AssetDatabase.AddObjectToAsset(created, texture);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(PlaceholderAssetPath, ImportAssetOptions.ForceUpdate);
        return created;
    }

    private static void PaintRect(
        Color[] pixels,
        int textureWidth,
        int xMin,
        int yMin,
        int xMax,
        int yMax,
        Color color)
    {
        for (int y = yMin; y < yMax; y++)
        for (int x = xMin; x < xMax; x++)
            pixels[y * textureWidth + x] = color;
    }

    private static Camera ConfigureCamera()
    {
        Camera camera = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
        if (camera == null)
        {
            GameObject cameraObject = new("Main Camera");
            cameraObject.tag = "MainCamera";
            camera = cameraObject.AddComponent<Camera>();
        }

        camera.orthographic = false;
        camera.fieldOfView = 48f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.055f, 0.065f, 0.08f);
        return camera;
    }

    private static void ConfigureLight()
    {
        Light light = Object.FindFirstObjectByType<Light>();
        if (light == null)
        {
            GameObject lightObject = new("Directional Light");
            light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
        }
        light.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
        light.intensity = 1.25f;
    }
}

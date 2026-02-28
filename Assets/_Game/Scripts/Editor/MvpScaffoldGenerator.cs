#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class MvpScaffoldGenerator
{
    private const string RootPath = "Assets/_Game";
    private const string ScriptsPath = RootPath + "/Scripts";
    private const string PrefabsPath = RootPath + "/Prefabs";
    private const string ScenesPath = RootPath + "/Scenes";
    private const string BootScenePath = ScenesPath + "/Boot.unity";
    private const string GameScenePath = ScenesPath + "/Game.unity";

    [MenuItem("Tools/One Second Left/Generate MVP Scaffold")]
    public static void GenerateScaffold()
    {
        EnsureFolders();

        bool createdBoot = CreateBootSceneIfMissing();
        bool createdGame = CreateGameSceneIfMissing();

        UpdateBuildSettings();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (createdGame)
        {
            EditorSceneManager.OpenScene(GameScenePath);
        }

        if (createdBoot || createdGame)
        {
            Debug.Log("One Second Left scaffold generated.");
        }
    }

    private static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder(RootPath))
        {
            AssetDatabase.CreateFolder("Assets", "_Game");
        }

        if (!AssetDatabase.IsValidFolder(ScriptsPath))
        {
            AssetDatabase.CreateFolder(RootPath, "Scripts");
        }

        if (!AssetDatabase.IsValidFolder(PrefabsPath))
        {
            AssetDatabase.CreateFolder(RootPath, "Prefabs");
        }

        if (!AssetDatabase.IsValidFolder(ScenesPath))
        {
            AssetDatabase.CreateFolder(RootPath, "Scenes");
        }
    }

    private static bool CreateBootSceneIfMissing()
    {
        if (File.Exists(BootScenePath))
        {
            return false;
        }

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var bootLoaderObject = new GameObject("BootLoader");
        bootLoaderObject.AddComponent<BootLoader>();

        EditorSceneManager.SaveScene(scene, BootScenePath);
        return true;
    }

    private static bool CreateGameSceneIfMissing()
    {
        if (File.Exists(GameScenePath))
        {
            return false;
        }

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        var camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 5.5f;
        camera.backgroundColor = new Color(0.03f, 0.04f, 0.09f, 1f);
        camera.clearFlags = CameraClearFlags.SolidColor;
        cameraObject.transform.position = new Vector3(0f, 0f, -10f);

        EditorSceneManager.SaveScene(scene, GameScenePath);
        return true;
    }

    private static UIHud CreateHud(TimeAbility timeAbility)
    {
        var canvasObject = new GameObject("Canvas");
        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObject.AddComponent<GraphicRaycaster>();
        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 1f;
        var hud = canvasObject.AddComponent<UIHud>();

        var meterBackground = CreateImage("MeterBackground", canvasObject.transform, new Color(1f, 1f, 1f, 0.15f));
        RectTransform bgRect = meterBackground.rectTransform;
        bgRect.anchorMin = new Vector2(0.5f, 1f);
        bgRect.anchorMax = new Vector2(0.5f, 1f);
        bgRect.sizeDelta = new Vector2(420f, 28f);
        bgRect.anchoredPosition = new Vector2(0f, -88f);

        var meterFill = CreateImage("MeterFill", meterBackground.transform, new Color(0.2f, 0.92f, 1f, 1f));
        meterFill.type = Image.Type.Filled;
        meterFill.fillMethod = Image.FillMethod.Horizontal;
        meterFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        RectTransform fillRect = meterFill.rectTransform;
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(1f, 1f);
        fillRect.offsetMin = new Vector2(3f, 3f);
        fillRect.offsetMax = new Vector2(-3f, -3f);

        Text meterText = CreateText(
            "MeterText",
            canvasObject.transform,
            "1.00s",
            32,
            TextAnchor.UpperCenter,
            new Vector2(0.5f, 1f),
            new Vector2(0f, -36f),
            new Vector2(220f, 46f));

        Text stateText = CreateText(
            "StateText",
            canvasObject.transform,
            "RUN",
            36,
            TextAnchor.UpperLeft,
            new Vector2(0f, 1f),
            new Vector2(80f, -80f),
            new Vector2(220f, 50f));

        hud.Configure(timeAbility, meterText, meterFill, stateText);
        return hud;
    }

    private static Image CreateImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var image = go.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private static Text CreateText(
        string name,
        Transform parent,
        string value,
        int fontSize,
        TextAnchor alignment,
        Vector2 anchor,
        Vector2 anchoredPosition,
        Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var text = go.AddComponent<Text>();
        text.text = value;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;

        RectTransform rect = text.rectTransform;
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(anchor.x, anchor.y);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        return text;
    }

    private static void UpdateBuildSettings()
    {
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(BootScenePath, true),
            new EditorBuildSettingsScene(GameScenePath, true)
        };
    }
}
#endif

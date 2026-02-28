using UnityEngine;
using UnityEngine.UI;

public static class HudFactory
{
    public static UIHud Create(TimeAbility timeAbility, ScoreManager scoreManager)
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

        Image meterBackground = CreateImage("MeterBackground", canvasObject.transform, new Color(1f, 1f, 1f, 0.15f));
        RectTransform bgRect = meterBackground.rectTransform;
        bgRect.anchorMin = new Vector2(0.5f, 1f);
        bgRect.anchorMax = new Vector2(0.5f, 1f);
        bgRect.sizeDelta = new Vector2(420f, 28f);
        bgRect.anchoredPosition = new Vector2(0f, -88f);

        Image meterFill = CreateImage("MeterFill", meterBackground.transform, new Color(0.2f, 0.92f, 1f, 1f));
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
            30,
            TextAnchor.UpperLeft,
            new Vector2(0f, 1f),
            new Vector2(64f, -68f),
            new Vector2(180f, 44f));

        CreateText(
            "ScoreTitle",
            canvasObject.transform,
            "SCORE",
            22,
            TextAnchor.UpperCenter,
            new Vector2(0.5f, 1f),
            new Vector2(0f, -130f),
            new Vector2(240f, 30f));

        Text scoreText = CreateText(
            "ScoreText",
            canvasObject.transform,
            "0",
            52,
            TextAnchor.UpperCenter,
            new Vector2(0.5f, 1f),
            new Vector2(0f, -172f),
            new Vector2(280f, 60f));

        CreateText(
            "BestTitle",
            canvasObject.transform,
            "BEST",
            20,
            TextAnchor.UpperRight,
            new Vector2(1f, 1f),
            new Vector2(-70f, -66f),
            new Vector2(120f, 26f));

        Text bestText = CreateText(
            "BestText",
            canvasObject.transform,
            "0",
            34,
            TextAnchor.UpperRight,
            new Vector2(1f, 1f),
            new Vector2(-70f, -102f),
            new Vector2(180f, 40f));

        Text newBestText = CreateText(
            "NewBestText",
            canvasObject.transform,
            string.Empty,
            26,
            TextAnchor.UpperCenter,
            new Vector2(0.5f, 1f),
            new Vector2(0f, -228f),
            new Vector2(320f, 36f));
        newBestText.color = new Color(1f, 0.85f, 0.2f, 1f);

        GameObject deathOverlay = CreateDeathOverlay(canvasObject.transform);
        hud.Configure(
            timeAbility,
            scoreManager,
            meterText,
            meterFill,
            stateText,
            scoreText,
            bestText,
            newBestText,
            deathOverlay);
        return hud;
    }

    private static GameObject CreateDeathOverlay(Transform parent)
    {
        Image overlayImage = CreateImage("DeathOverlay", parent, new Color(0f, 0f, 0f, 0.62f));
        RectTransform rect = overlayImage.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        CreateText(
            "CrashLabel",
            overlayImage.transform,
            "CRASH",
            88,
            TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f),
            new Vector2(0f, 38f),
            new Vector2(460f, 100f));

        Text restartLabel = CreateText(
            "RestartLabel",
            overlayImage.transform,
            "RESETTING",
            32,
            TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f),
            new Vector2(0f, -38f),
            new Vector2(420f, 60f));
        restartLabel.color = new Color(0.8f, 0.86f, 0.95f, 1f);

        overlayImage.gameObject.SetActive(false);
        return overlayImage.gameObject;
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
}

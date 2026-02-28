using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

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

        RectTransform hudRoot = CreateSafeAreaRoot(canvasObject.transform);
        var hud = canvasObject.AddComponent<UIHud>();

        CreateHudBackdrop(hudRoot);
        Image nearMissPulse = CreateImage("NearMissPulse", hudRoot, new Color(0.35f, 0.95f, 1f, 0f));
        RectTransform pulseRect = nearMissPulse.rectTransform;
        pulseRect.anchorMin = Vector2.zero;
        pulseRect.anchorMax = Vector2.one;
        pulseRect.offsetMin = Vector2.zero;
        pulseRect.offsetMax = Vector2.zero;
        nearMissPulse.raycastTarget = false;

        Image meterBackground = CreateImage("MeterBackground", hudRoot, new Color(1f, 1f, 1f, 0.15f));
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
            hudRoot,
            "1.00s",
            32,
            TextAnchor.UpperCenter,
            new Vector2(0.5f, 1f),
            new Vector2(0f, -36f),
            new Vector2(220f, 46f));

        Text stateText = CreateText(
            "StateText",
            hudRoot,
            "RUN",
            30,
            TextAnchor.UpperLeft,
            new Vector2(0f, 1f),
            new Vector2(64f, -68f),
            new Vector2(180f, 44f));
        stateText.color = new Color(0.88f, 0.93f, 1f, 1f);

        Text speedText = CreateText(
            "SpeedText",
            hudRoot,
            "FLOW 0.0",
            24,
            TextAnchor.UpperLeft,
            new Vector2(0f, 1f),
            new Vector2(64f, -112f),
            new Vector2(220f, 34f));
        speedText.color = new Color(0.7f, 0.9f, 1f, 1f);

        Text runSeedText = CreateText(
            "RunSeedText",
            hudRoot,
            "RANDOM",
            17,
            TextAnchor.UpperLeft,
            new Vector2(0f, 1f),
            new Vector2(64f, -146f),
            new Vector2(460f, 28f));
        runSeedText.color = new Color(0.62f, 0.8f, 0.95f, 1f);

        CreateText(
            "ScoreTitle",
            hudRoot,
            "SCORE",
            22,
            TextAnchor.UpperCenter,
            new Vector2(0.5f, 1f),
            new Vector2(0f, -130f),
            new Vector2(240f, 30f));

        Text scoreText = CreateText(
            "ScoreText",
            hudRoot,
            "0.0",
            52,
            TextAnchor.UpperCenter,
            new Vector2(0.5f, 1f),
            new Vector2(0f, -172f),
            new Vector2(280f, 60f));

        CreateText(
            "BestTitle",
            hudRoot,
            "BEST",
            20,
            TextAnchor.UpperRight,
            new Vector2(1f, 1f),
            new Vector2(-70f, -66f),
            new Vector2(120f, 26f));

        Text bestText = CreateText(
            "BestText",
            hudRoot,
            "0.0",
            34,
            TextAnchor.UpperRight,
            new Vector2(1f, 1f),
            new Vector2(-70f, -102f),
            new Vector2(180f, 40f));

        Text newBestText = CreateText(
            "NewBestText",
            hudRoot,
            string.Empty,
            26,
            TextAnchor.UpperCenter,
            new Vector2(0.5f, 1f),
            new Vector2(0f, -228f),
            new Vector2(320f, 36f));
        newBestText.color = new Color(1f, 0.85f, 0.2f, 1f);

        Text comboText = CreateText(
            "ComboText",
            hudRoot,
            "COMBO READY",
            24,
            TextAnchor.UpperCenter,
            new Vector2(0.5f, 1f),
            new Vector2(0f, -268f),
            new Vector2(360f, 36f));
        comboText.color = new Color(0.8f, 0.86f, 0.95f, 1f);

        TitleOverlayElements titleOverlay = CreateTitleOverlay(hudRoot);

        EnsureEventSystemExists();
        DeathOverlayElements deathOverlay = CreateDeathOverlay(canvasObject.transform);

        hud.Configure(
            timeAbility,
            scoreManager,
            meterText,
            meterFill,
            stateText,
            scoreText,
            bestText,
            newBestText,
            deathOverlay.Root,
            comboText,
            speedText,
            nearMissPulse);

        hud.ConfigureDeathSummary(
            deathOverlay.ScoreValue,
            deathOverlay.BestValue,
            deathOverlay.PeakNearMissValue,
            deathOverlay.PeakSpeedValue,
            deathOverlay.DailySeedValue,
            deathOverlay.ShareHintValue,
            deathOverlay.ShareStatusValue,
            deathOverlay.ShareButton,
            deathOverlay.RestartButton);

        hud.ConfigureRunContextLabel(runSeedText);
        hud.ConfigureTitleOverlay(
            titleOverlay.Root,
            titleOverlay.GameNameValue,
            titleOverlay.BestScoreValue,
            titleOverlay.PromptValue,
            titleOverlay.ModeValue,
            titleOverlay.RandomModeButton,
            titleOverlay.DailyModeButton);

        return hud;
    }

    private static RectTransform CreateSafeAreaRoot(Transform parent)
    {
        var rootObject = new GameObject("HudRoot", typeof(RectTransform), typeof(SafeAreaAnchors));
        rootObject.transform.SetParent(parent, false);
        RectTransform rect = rootObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return rect;
    }

    private static void CreateHudBackdrop(RectTransform parent)
    {
        Image topFade = CreateImage("TopFade", parent, new Color(0f, 0f, 0f, 0.24f));
        RectTransform topRect = topFade.rectTransform;
        topRect.anchorMin = new Vector2(0f, 1f);
        topRect.anchorMax = new Vector2(1f, 1f);
        topRect.pivot = new Vector2(0.5f, 1f);
        topRect.sizeDelta = new Vector2(0f, 320f);
        topRect.anchoredPosition = Vector2.zero;
    }

    private static TitleOverlayElements CreateTitleOverlay(Transform parent)
    {
        TitleOverlayElements elements = new TitleOverlayElements();

        Image overlayImage = CreateImage("TitleOverlay", parent, new Color(0.01f, 0.02f, 0.03f, 0.48f));
        RectTransform overlayRect = overlayImage.rectTransform;
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;
        overlayImage.raycastTarget = false;
        elements.Root = overlayImage.gameObject;

        Image cardImage = CreateImage("TitleCard", overlayImage.transform, new Color(0.06f, 0.09f, 0.13f, 0.95f));
        RectTransform cardRect = cardImage.rectTransform;
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.anchoredPosition = new Vector2(0f, -20f);
        cardRect.sizeDelta = new Vector2(780f, 760f);

        Outline cardOutline = cardImage.gameObject.AddComponent<Outline>();
        cardOutline.effectColor = new Color(0.18f, 0.88f, 1f, 0.35f);
        cardOutline.effectDistance = new Vector2(2f, -2f);

        elements.GameNameValue = CreateText(
            "TitleGameName",
            cardImage.transform,
            "ONE SECOND LEFT",
            82,
            TextAnchor.UpperCenter,
            new Vector2(0.5f, 1f),
            new Vector2(0f, -78f),
            new Vector2(720f, 170f));

        elements.BestScoreValue = CreateText(
            "TitleBestScore",
            cardImage.transform,
            "BEST 0.0",
            38,
            TextAnchor.UpperCenter,
            new Vector2(0.5f, 1f),
            new Vector2(0f, -250f),
            new Vector2(560f, 70f));
        elements.BestScoreValue.color = new Color(0.82f, 0.92f, 1f, 1f);

        elements.PromptValue = CreateText(
            "TitlePrompt",
            cardImage.transform,
            "TAP TO START",
            30,
            TextAnchor.UpperCenter,
            new Vector2(0.5f, 1f),
            new Vector2(0f, -330f),
            new Vector2(580f, 58f));
        elements.PromptValue.color = new Color(0.82f, 0.95f, 1f, 1f);

        elements.ModeValue = CreateText(
            "RunModeValue",
            cardImage.transform,
            "MODE: RANDOM",
            24,
            TextAnchor.UpperCenter,
            new Vector2(0.5f, 1f),
            new Vector2(0f, -410f),
            new Vector2(520f, 46f));
        elements.ModeValue.color = new Color(0.72f, 0.88f, 0.98f, 1f);

        elements.RandomModeButton = CreateButton(
            "RandomModeButton",
            cardImage.transform,
            "RANDOM",
            new Vector2(0.5f, 1f),
            new Vector2(-130f, -486f),
            new Vector2(240f, 78f));

        elements.DailyModeButton = CreateButton(
            "DailyModeButton",
            cardImage.transform,
            "DAILY",
            new Vector2(0.5f, 1f),
            new Vector2(130f, -486f),
            new Vector2(240f, 78f));

        return elements;
    }

    private static DeathOverlayElements CreateDeathOverlay(Transform parent)
    {
        DeathOverlayElements elements = new DeathOverlayElements();

        Image overlayImage = CreateImage("DeathOverlay", parent, new Color(0f, 0f, 0f, 0.62f));
        RectTransform overlayRect = overlayImage.rectTransform;
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        elements.Root = overlayImage.gameObject;

        Image cardImage = CreateImage("SummaryCard", overlayImage.transform, new Color(0.06f, 0.09f, 0.13f, 0.96f));
        RectTransform cardRect = cardImage.rectTransform;
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.anchoredPosition = new Vector2(0f, -24f);
        cardRect.sizeDelta = new Vector2(760f, 940f);

        Outline cardOutline = cardImage.gameObject.AddComponent<Outline>();
        cardOutline.effectColor = new Color(0.18f, 0.88f, 1f, 0.4f);
        cardOutline.effectDistance = new Vector2(2f, -2f);

        CreateText(
            "CrashLabel",
            cardImage.transform,
            "CRASH",
            72,
            TextAnchor.UpperCenter,
            new Vector2(0.5f, 1f),
            new Vector2(0f, -52f),
            new Vector2(520f, 94f));

        Text summaryLabel = CreateText(
            "SummaryLabel",
            cardImage.transform,
            "RUN SUMMARY",
            34,
            TextAnchor.UpperCenter,
            new Vector2(0.5f, 1f),
            new Vector2(0f, -132f),
            new Vector2(560f, 54f));
        summaryLabel.color = new Color(0.62f, 0.88f, 1f, 1f);

        elements.ScoreValue = CreateSummaryRow(cardImage.transform, "SummaryScore", "SCORE", -230f);
        elements.BestValue = CreateSummaryRow(cardImage.transform, "SummaryBest", "BEST", -300f);
        elements.PeakNearMissValue = CreateSummaryRow(cardImage.transform, "SummaryNearMiss", "PEAK COMBO", -370f);
        elements.PeakSpeedValue = CreateSummaryRow(cardImage.transform, "SummaryTopSpeed", "TOP SPEED", -440f);
        elements.DailySeedValue = CreateSummaryRow(cardImage.transform, "SummarySeed", "DAILY SEED", -510f);

        elements.ShareButton = CreateButton(
            "ShareActionButton",
            cardImage.transform,
            "SAVE SHARE CARD",
            new Vector2(0.5f, 1f),
            new Vector2(0f, -620f),
            new Vector2(500f, 84f));

        elements.RestartButton = CreateButton(
            "RestartActionButton",
            cardImage.transform,
            "PLAY AGAIN",
            new Vector2(0.5f, 1f),
            new Vector2(0f, -716f),
            new Vector2(500f, 84f));

        elements.ShareHintValue = CreateText(
            "ShareHintLabel",
            cardImage.transform,
            "Tap button or press S",
            24,
            TextAnchor.UpperCenter,
            new Vector2(0.5f, 1f),
            new Vector2(0f, -820f),
            new Vector2(560f, 40f));
        elements.ShareHintValue.color = new Color(0.72f, 0.86f, 0.95f, 1f);

        elements.ShareStatusValue = CreateText(
            "ShareStatusLabel",
            cardImage.transform,
            " ",
            20,
            TextAnchor.UpperCenter,
            new Vector2(0.5f, 1f),
            new Vector2(0f, -868f),
            new Vector2(640f, 88f));
        elements.ShareStatusValue.color = new Color(0.58f, 0.95f, 0.78f, 1f);

        overlayImage.gameObject.SetActive(false);
        return elements;
    }

    private static Text CreateSummaryRow(Transform parent, string name, string label, float y)
    {
        Text labelText = CreateText(
            $"{name}Label",
            parent,
            label,
            25,
            TextAnchor.MiddleLeft,
            new Vector2(0f, 1f),
            new Vector2(48f, y),
            new Vector2(320f, 48f));
        labelText.color = new Color(0.64f, 0.75f, 0.84f, 1f);

        return CreateText(
            $"{name}Value",
            parent,
            "--",
            30,
            TextAnchor.MiddleRight,
            new Vector2(1f, 1f),
            new Vector2(-48f, y),
            new Vector2(340f, 48f));
    }

    private static Button CreateButton(
        string name,
        Transform parent,
        string label,
        Vector2 anchor,
        Vector2 anchoredPosition,
        Vector2 size)
    {
        Image buttonImage = CreateImage(name, parent, new Color(0.13f, 0.62f, 0.75f, 0.95f));
        RectTransform buttonRect = buttonImage.rectTransform;
        buttonRect.anchorMin = anchor;
        buttonRect.anchorMax = anchor;
        buttonRect.pivot = new Vector2(anchor.x, anchor.y);
        buttonRect.anchoredPosition = anchoredPosition;
        buttonRect.sizeDelta = size;

        Button button = buttonImage.gameObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = buttonImage.color;
        colors.highlightedColor = new Color(0.18f, 0.73f, 0.88f, 1f);
        colors.pressedColor = new Color(0.1f, 0.5f, 0.62f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.22f, 0.28f, 0.32f, 0.7f);
        button.colors = colors;

        CreateText(
            $"{name}Label",
            buttonImage.transform,
            label,
            30,
            TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(size.x - 24f, size.y - 16f));

        return button;
    }

    private static void EnsureEventSystemExists()
    {
        if (Object.FindFirstObjectByType<EventSystem>() != null)
        {
            return;
        }

        GameObject eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
        eventSystemObject.AddComponent<InputSystemUIInputModule>();
#else
        eventSystemObject.AddComponent<StandaloneInputModule>();
#endif
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
        text.fontStyle = FontStyle.Bold;
        text.alignment = alignment;
        text.color = Color.white;

        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.58f);
        outline.effectDistance = new Vector2(1.2f, -1.2f);

        var shadow = go.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.3f);
        shadow.effectDistance = new Vector2(0f, -1.5f);

        RectTransform rect = text.rectTransform;
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(anchor.x, anchor.y);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        return text;
    }

    private sealed class DeathOverlayElements
    {
        public GameObject Root;
        public Text ScoreValue;
        public Text BestValue;
        public Text PeakNearMissValue;
        public Text PeakSpeedValue;
        public Text DailySeedValue;
        public Text ShareHintValue;
        public Text ShareStatusValue;
        public Button ShareButton;
        public Button RestartButton;
    }

    private sealed class TitleOverlayElements
    {
        public GameObject Root;
        public Text GameNameValue;
        public Text BestScoreValue;
        public Text PromptValue;
        public Text ModeValue;
        public Button RandomModeButton;
        public Button DailyModeButton;
    }
}

internal sealed class SafeAreaAnchors : MonoBehaviour
{
    private Rect lastSafeArea;
    private Vector2Int lastScreenSize;
    private ScreenOrientation lastOrientation;
    private RectTransform rectTransform;

    private void Awake()
    {
        rectTransform = transform as RectTransform;
    }

    private void OnEnable()
    {
        ApplySafeArea(force: true);
    }

    private void Update()
    {
        ApplySafeArea(force: false);
    }

    private void ApplySafeArea(bool force)
    {
        if (rectTransform == null)
        {
            rectTransform = transform as RectTransform;
            if (rectTransform == null)
            {
                return;
            }
        }

        Vector2Int screenSize = new Vector2Int(Screen.width, Screen.height);
        if (screenSize.x <= 0 || screenSize.y <= 0)
        {
            return;
        }

        Rect safeArea = Screen.safeArea;
        ScreenOrientation orientation = Screen.orientation;
        if (!force &&
            safeArea == lastSafeArea &&
            screenSize == lastScreenSize &&
            orientation == lastOrientation)
        {
            return;
        }

        Vector2 anchorMin = safeArea.position;
        Vector2 anchorMax = safeArea.position + safeArea.size;
        anchorMin.x /= screenSize.x;
        anchorMin.y /= screenSize.y;
        anchorMax.x /= screenSize.x;
        anchorMax.y /= screenSize.y;

        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;

        lastSafeArea = safeArea;
        lastScreenSize = screenSize;
        lastOrientation = orientation;
    }
}

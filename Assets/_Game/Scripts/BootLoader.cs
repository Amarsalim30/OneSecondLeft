using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class BootLoader : MonoBehaviour
{
    [SerializeField] private string gameSceneName = "Game";
    [Header("Loading Overlay")]
    [SerializeField] private bool showLoadingOverlay = true;
    [SerializeField, Min(0f)] private float minimumLoadingOverlaySeconds = 0.35f;
    [SerializeField] private Color loadingBackdropColor = new Color(0.02f, 0.03f, 0.08f, 1f);
    [SerializeField] private Color loadingAccentColor = new Color(0.18f, 0.86f, 1f, 1f);

    private IEnumerator Start()
    {
        string targetScene = gameSceneName == null ? string.Empty : gameSceneName.Trim();
        float loadStartedAt = Time.unscaledTime;
        GameplayAnalytics.Track("app_boot_started", new Dictionary<string, object>(4)
        {
            ["scene_target"] = targetScene
        });

        if (string.IsNullOrEmpty(targetScene))
        {
            Debug.LogError("BootLoader requires a non-empty game scene name.");
            yield break;
        }

        if (SceneManager.GetActiveScene().name == targetScene || SceneManager.GetSceneByName(targetScene).isLoaded)
        {
            yield break;
        }

        if (!Application.CanStreamedLevelBeLoaded(targetScene))
        {
            Debug.LogError($"BootLoader cannot load scene '{targetScene}' because it is not in build settings.");
            yield break;
        }

        LoadingOverlay overlay = null;
        if (showLoadingOverlay)
        {
            overlay = LoadingOverlay.Create(loadingBackdropColor, loadingAccentColor);
        }

        AsyncOperation loadOperation = SceneManager.LoadSceneAsync(targetScene, LoadSceneMode.Single);
        if (loadOperation == null)
        {
            Debug.LogError($"BootLoader failed to start async load for scene '{targetScene}'.");
            overlay?.Dispose();
            yield break;
        }

        loadOperation.allowSceneActivation = false;
        float overlayStartedAt = Time.unscaledTime;
        float visualProgress = 0f;
        while (loadOperation.progress < 0.9f)
        {
            float raw = Mathf.Clamp01(loadOperation.progress / 0.9f);
            visualProgress = Mathf.Max(visualProgress, raw);
            overlay?.SetProgress(visualProgress);
            yield return null;
        }

        float minDisplay = Mathf.Max(0f, minimumLoadingOverlaySeconds);
        while (Time.unscaledTime - overlayStartedAt < minDisplay)
        {
            float t = minDisplay <= 0f ? 1f : Mathf.Clamp01((Time.unscaledTime - overlayStartedAt) / minDisplay);
            overlay?.SetProgress(Mathf.Lerp(visualProgress, 1f, t));
            yield return null;
        }

        overlay?.SetProgress(1f);
        loadOperation.allowSceneActivation = true;
        while (!loadOperation.isDone)
        {
            yield return null;
        }

        overlay?.Dispose();

        GameplayAnalytics.Track("game_scene_loaded", new Dictionary<string, object>(4)
        {
            ["scene_target"] = targetScene,
            ["load_duration_ms"] = Mathf.RoundToInt(Mathf.Max(0f, Time.unscaledTime - loadStartedAt) * 1000f)
        });
    }

    private sealed class LoadingOverlay
    {
        private readonly GameObject root;
        private readonly Image fillImage;
        private readonly Text statusLabel;

        private LoadingOverlay(GameObject root, Image fillImage, Text statusLabel)
        {
            this.root = root;
            this.fillImage = fillImage;
            this.statusLabel = statusLabel;
        }

        public static LoadingOverlay Create(Color backdropColor, Color accentColor)
        {
            var rootObject = new GameObject("BootLoadingOverlay");

            var canvas = rootObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;
            rootObject.AddComponent<GraphicRaycaster>();
            var scaler = rootObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 1f;

            Image backdrop = CreateImage("Backdrop", rootObject.transform, backdropColor);
            RectTransform backdropRect = backdrop.rectTransform;
            backdropRect.anchorMin = Vector2.zero;
            backdropRect.anchorMax = Vector2.one;
            backdropRect.offsetMin = Vector2.zero;
            backdropRect.offsetMax = Vector2.zero;

            Image frame = CreateImage("ProgressFrame", rootObject.transform, new Color(1f, 1f, 1f, 0.14f));
            RectTransform frameRect = frame.rectTransform;
            frameRect.anchorMin = new Vector2(0.5f, 0.5f);
            frameRect.anchorMax = new Vector2(0.5f, 0.5f);
            frameRect.sizeDelta = new Vector2(560f, 26f);
            frameRect.anchoredPosition = new Vector2(0f, -6f);

            Image fill = CreateImage("ProgressFill", frame.transform, accentColor);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 0f;
            RectTransform fillRect = fill.rectTransform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(2f, 2f);
            fillRect.offsetMax = new Vector2(-2f, -2f);

            Text label = CreateText(rootObject.transform, "LOADING");
            RectTransform labelRect = label.rectTransform;
            labelRect.anchorMin = new Vector2(0.5f, 0.5f);
            labelRect.anchorMax = new Vector2(0.5f, 0.5f);
            labelRect.sizeDelta = new Vector2(360f, 56f);
            labelRect.anchoredPosition = new Vector2(0f, 54f);

            return new LoadingOverlay(rootObject, fill, label);
        }

        public void SetProgress(float progress01)
        {
            float clamped = Mathf.Clamp01(progress01);
            if (fillImage != null)
            {
                fillImage.fillAmount = clamped;
            }

            if (statusLabel != null)
            {
                statusLabel.text = $"LOADING {Mathf.RoundToInt(clamped * 100f)}%";
            }
        }

        public void Dispose()
        {
            if (root != null)
            {
                Object.Destroy(root);
            }
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = color;
            return image;
        }

        private static Text CreateText(Transform parent, string value)
        {
            var go = new GameObject("StatusLabel");
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.text = value;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 36;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(0.88f, 0.93f, 1f, 1f);
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.55f);
            outline.effectDistance = new Vector2(1.25f, -1.25f);
            return text;
        }
    }
}

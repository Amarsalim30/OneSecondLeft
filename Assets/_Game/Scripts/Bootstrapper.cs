using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public static class Bootstrapper
{
    private static Sprite squareSprite;
    private static bool isRegistered;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneHook()
    {
        if (isRegistered)
        {
            return;
        }

        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        isRegistered = true;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode _)
    {
        if (scene.name != "Game")
        {
            return;
        }

        List<string> repairs = new List<string>();
        EnsureRuntimeGraph(repairs);
        if (repairs.Count > 0)
        {
            Debug.LogWarning($"Bootstrapper repaired runtime graph: {string.Join(", ", repairs)}");
        }
    }

    private static void EnsureRuntimeGraph(List<string> repairs)
    {
        ConfigureMainCamera(repairs);

        GameManager manager = Object.FindFirstObjectByType<GameManager>();
        GameObject root = manager != null ? manager.gameObject : GameObject.Find("GameRoot");
        if (root == null)
        {
            root = new GameObject("GameRoot");
            repairs.Add("created GameRoot");
        }

        if (manager == null)
        {
            manager = root.AddComponent<GameManager>();
            repairs.Add("created GameManager");
        }

        TimeAbility timeAbility = EnsureService<TimeAbility>(root, "TimeAbility", repairs);
        ScoreManager scoreManager = EnsureService<ScoreManager>(root, "ScoreManager", repairs);
        AudioManager audioManager = EnsureService<AudioManager>(root, "AudioManager", repairs);
        ObstacleSpawner spawner = EnsureService<ObstacleSpawner>(root, "ObstacleSpawner", repairs);

        Transform obstacleContainer = FindOrCreateChild(root.transform, "Obstacles", repairs);
        PlayerController playerController = EnsurePlayer(root.transform, repairs);
        ObstacleWall obstacleTemplate = EnsureObstacleTemplate(root.transform, repairs);
        spawner.SetObstaclePrefab(obstacleTemplate, obstacleContainer);
        spawner.SetSystems(playerController, scoreManager, audioManager);

        UIHud hud = Object.FindFirstObjectByType<UIHud>();
        if (hud == null)
        {
            hud = HudFactory.Create(timeAbility, scoreManager);
            repairs.Add("created UIHud");
        }

        manager.Configure(playerController, timeAbility, spawner, scoreManager, audioManager, hud);
    }

    private static T EnsureService<T>(GameObject root, string label, List<string> repairs) where T : Component
    {
        T existing = Object.FindFirstObjectByType<T>();
        if (existing != null)
        {
            return existing;
        }

        if (root == null)
        {
            root = new GameObject("GameRoot");
            repairs.Add("created GameRoot");
        }

        repairs.Add($"created {label}");
        return root.AddComponent<T>();
    }

    private static Transform FindOrCreateChild(Transform parent, string childName, List<string> repairs)
    {
        Transform child = parent.Find(childName);
        if (child != null)
        {
            return child;
        }

        GameObject childObject = new GameObject(childName);
        childObject.transform.SetParent(parent, false);
        repairs.Add($"created {childName}");
        return childObject.transform;
    }

    private static PlayerController EnsurePlayer(Transform parent, List<string> repairs)
    {
        PlayerController player = Object.FindFirstObjectByType<PlayerController>();
        if (player != null)
        {
            return player;
        }

        repairs.Add("created Player");
        return CreatePlayer(parent);
    }

    private static ObstacleWall EnsureObstacleTemplate(Transform parent, List<string> repairs)
    {
        Transform existing = parent.Find("ObstacleWallTemplate");
        if (existing != null)
        {
            ObstacleWall wall = existing.GetComponent<ObstacleWall>();
            if (wall == null)
            {
                wall = existing.gameObject.AddComponent<ObstacleWall>();
                repairs.Add("added ObstacleWall component to ObstacleWallTemplate");
            }

            PrepareTemplateVisuals(existing.gameObject);
            return wall;
        }

        repairs.Add("created ObstacleWallTemplate");
        return CreateObstacleTemplate(parent);
    }

    private static void ConfigureMainCamera(List<string> repairs)
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            camera = Object.FindFirstObjectByType<Camera>();
        }

        if (camera != null)
        {
            if (!camera.CompareTag("MainCamera"))
            {
                camera.tag = "MainCamera";
                repairs.Add("tagged existing camera as MainCamera");
            }

            return;
        }

        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        camera = cameraObject.AddComponent<Camera>();
        camera.transform.position = new Vector3(0f, 0f, -10f);
        camera.orthographic = true;
        camera.orthographicSize = 6f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.03f, 0.04f, 0.09f, 1f);
        repairs.Add("created Main Camera");
    }

    private static PlayerController CreatePlayer(Transform parent)
    {
        GameObject player = new GameObject("Player");
        player.transform.SetParent(parent, false);
        player.transform.position = new Vector3(0f, -3.25f, 0f);
        player.transform.localScale = new Vector3(0.42f, 0.42f, 1f);

        SpriteRenderer renderer = player.AddComponent<SpriteRenderer>();
        renderer.sprite = GetSquareSprite();
        renderer.color = new Color(0.19f, 0.94f, 1f, 1f);
        renderer.sortingOrder = 10;

        player.AddComponent<CircleCollider2D>();
        Rigidbody2D rb2d = player.AddComponent<Rigidbody2D>();
        rb2d.bodyType = RigidbodyType2D.Kinematic;
        rb2d.gravityScale = 0f;
        rb2d.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb2d.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb2d.freezeRotation = true;

        return player.AddComponent<PlayerController>();
    }

    private static ObstacleWall CreateObstacleTemplate(Transform parent)
    {
        GameObject wall = new GameObject("ObstacleWallTemplate");
        wall.transform.SetParent(parent, false);
        ObstacleWall template = wall.AddComponent<ObstacleWall>();
        PrepareTemplateVisuals(wall);
        return template;
    }

    private static void PrepareTemplateVisuals(GameObject wall)
    {
        Sprite sprite = GetSquareSprite();
        SpriteRenderer[] renderers = wall.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].sprite = sprite;
            renderers[i].color = new Color(0.95f, 0.95f, 1f, 1f);
            renderers[i].sortingOrder = 2;
        }

        wall.SetActive(false);
    }

    private static Sprite GetSquareSprite()
    {
        if (squareSprite != null)
        {
            return squareSprite;
        }

        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        squareSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        return squareSprite;
    }
}

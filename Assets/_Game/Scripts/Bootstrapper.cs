using UnityEngine;
using UnityEngine.SceneManagement;

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

        if (Object.FindFirstObjectByType<GameManager>() != null)
        {
            return;
        }

        ConfigureMainCamera();

        GameObject root = new GameObject("GameRoot");
        GameObject obstacleContainer = new GameObject("Obstacles");
        obstacleContainer.transform.SetParent(root.transform, false);

        GameManager manager = root.AddComponent<GameManager>();
        TimeAbility timeAbility = root.AddComponent<TimeAbility>();
        ScoreManager scoreManager = root.AddComponent<ScoreManager>();
        AudioManager audioManager = root.AddComponent<AudioManager>();
        ObstacleSpawner spawner = root.AddComponent<ObstacleSpawner>();

        PlayerController playerController = CreatePlayer(root.transform);
        ObstacleWall obstacleTemplate = CreateObstacleTemplate(root.transform);
        spawner.SetObstaclePrefab(obstacleTemplate, obstacleContainer.transform);
        spawner.SetSystems(playerController, scoreManager, audioManager);

        UIHud hud = HudFactory.Create(timeAbility, scoreManager);
        manager.Configure(playerController, timeAbility, spawner, scoreManager, audioManager, hud);
    }

    private static void ConfigureMainCamera()
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            camera = cameraObject.AddComponent<Camera>();
        }

        camera.transform.position = new Vector3(0f, 0f, -10f);
        camera.orthographic = true;
        camera.orthographicSize = 6f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.03f, 0.04f, 0.09f, 1f);
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

        Sprite sprite = GetSquareSprite();
        SpriteRenderer[] renderers = wall.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].sprite = sprite;
            renderers[i].color = new Color(0.95f, 0.95f, 1f, 1f);
            renderers[i].sortingOrder = 2;
        }

        wall.SetActive(false);
        return template;
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

using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [SerializeField] private float restartDelaySeconds = 0.5f;
    [SerializeField] private PlayerController playerController;
    [SerializeField] private TimeAbility timeAbility;
    [SerializeField] private ObstacleSpawner obstacleSpawner;
    [SerializeField] private ScoreManager scoreManager;
    [SerializeField] private AudioManager audioManager;
    [SerializeField] private UIHud uiHud;

    private float restartTimer;
    private bool restartQueued;

    public bool IsPlaying { get; private set; }

    public void Configure(
        PlayerController player,
        TimeAbility ability,
        ObstacleSpawner spawner,
        ScoreManager score,
        AudioManager audio,
        UIHud hud)
    {
        playerController = player;
        timeAbility = ability;
        obstacleSpawner = spawner;
        scoreManager = score;
        audioManager = audio;
        uiHud = hud;
    }

    public void Configure(PlayerController player, TimeAbility ability, UIHud hud)
    {
        Configure(player, ability, obstacleSpawner, scoreManager, audioManager, hud);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
    }

    private void Start()
    {
        ResolveMissingReferences();
        obstacleSpawner?.SetSystems(playerController, scoreManager, audioManager);
        timeAbility?.Configure(audioManager);

        StartRun();
    }

    private void OnDisable()
    {
        timeAbility?.ForceNormalTime();
        audioManager?.SetSlowPitch(false);
    }

    private void Update()
    {
        if (!restartQueued)
        {
            return;
        }

        restartTimer -= Time.unscaledDeltaTime;
        if (restartTimer > 0f)
        {
            return;
        }

        StartRun();
    }

    public void KillPlayer()
    {
        if (!IsPlaying)
        {
            return;
        }

        IsPlaying = false;
        restartQueued = true;
        restartTimer = restartDelaySeconds;

        scoreManager?.CommitRunIfBest();
        audioManager?.PlayCrash();
        timeAbility?.ForceNormalTime();
        uiHud?.ShowDeath();
    }

    public void StartRun()
    {
        ResolveMissingReferences();

        restartQueued = false;
        IsPlaying = true;

        scoreManager?.ResetRun();
        obstacleSpawner?.ResetRun();
        playerController?.ResetRunPosition();
        timeAbility?.ResetMeter();
        audioManager?.SetSlowPitch(false);
        uiHud?.HideDeath();
    }

    private void ResolveMissingReferences()
    {
        if (playerController == null)
        {
            playerController = FindFirstObjectByType<PlayerController>();
        }

        if (timeAbility == null)
        {
            timeAbility = FindFirstObjectByType<TimeAbility>();
        }

        if (obstacleSpawner == null)
        {
            obstacleSpawner = FindFirstObjectByType<ObstacleSpawner>();
        }

        if (scoreManager == null)
        {
            scoreManager = FindFirstObjectByType<ScoreManager>();
        }

        if (audioManager == null)
        {
            audioManager = FindFirstObjectByType<AudioManager>();
        }

        if (uiHud == null)
        {
            uiHud = FindFirstObjectByType<UIHud>();
        }
    }
}

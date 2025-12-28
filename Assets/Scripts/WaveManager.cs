using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Handles wave spawning, scaling, and tracking wave/enemy state for UI.
/// </summary>
public class WaveManager : MonoBehaviour
{
    [System.Serializable]
    public class Wave
    {
        public GameObject[] enemyPrefabs;
        public int enemyCount;
        public float spawnInterval;
        public float waveDuration;
    }

    [Header("Wave Settings")]
    public Wave[] waves;
    public Transform[] spawnPoints;
    public float timeBetweenWaves = 5f;

    [Header("Boss Settings")]
    [Tooltip("Boss prefab to spawn every 10th wave")]
    public GameObject bossPrefab;
    [Tooltip("When true the boss will spawn on wave 1 (use for testing)")]
    public bool forceBossSpawn = false;

    [Header("Special Events")]
    [Range(0f,1f)] public float batEventChance = 0.4f; // 40% chance per wave
    public GameObject batEventPrefab; // prefab to spawn for the bat event
    public GameObject batEventUIText; // UI GameObject (e.g. Text on Canvas) to enable while event active
    public float batEventDuration = 30f; // seconds the event lasts
    [Tooltip("When true the bat event will run every wave (use for testing)")]
    public bool forceBatEvent = false;
    // ensure only one bat event can run at a time
    private bool isBatEventActive = false;

    [Header("Difficulty Scaling")]
    public float enemyHealthMultiplier = 1.1f;
    public float enemyDamageMultiplier = 1.05f;
    public float spawnRateMultiplier = 0.95f;

    private int currentWave = 0;
    private int enemiesRemaining;
    private bool waveInProgress = false;
    private float waveTimer;

    public static WaveManager Instance;
    
    // Event declarations for wave notifications
    public delegate void WaveEvent(int waveNumber);
    public event WaveEvent OnWaveStarted;
    public event WaveEvent OnWaveCompleted;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        StartNextWave();
    }

    void Update()
    {
        if (waveInProgress)
        {
            waveTimer -= Time.deltaTime;

            if (enemiesRemaining <= 0 || waveTimer <= 0)
            {
                CompleteWave();
            }
        }
    }

    void StartNextWave()
    {
        if (currentWave >= waves.Length)
        {
            // Create endless wave based on last wave
            CreateEndlessWave();
        }

        Wave wave = waves[currentWave];
        enemiesRemaining = wave.enemyCount;
        waveTimer = wave.waveDuration;
        waveInProgress = true;

        StartCoroutine(SpawnWave(wave));

        // spawn boss every 10th wave (or on wave 1 if forced for testing)
        int waveNumber = currentWave + 1;
        bool shouldSpawnBoss = (waveNumber % 10 == 0) || (forceBossSpawn && waveNumber == 1);
        if (shouldSpawnBoss && bossPrefab != null && spawnPoints != null && spawnPoints.Length > 0)
        {
            Transform bossSpawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];
            GameObject boss = Instantiate(bossPrefab, bossSpawnPoint.position, Quaternion.identity);
            
            // Flip the boss scale on spawn (boss prefab appears to have inverted default facing)
            if (boss != null)
            {
                Vector3 scale = boss.transform.localScale;
                scale.x *= -1f;
                boss.transform.localScale = scale;
            }
            
            Debug.Log($"Boss spawned at wave {waveNumber} at position {bossSpawnPoint.position}");
        }

        // roll for bat event for this wave (or force it for testing)
        if (!isBatEventActive && batEventPrefab != null && spawnPoints != null && spawnPoints.Length > 0)
        {
            float roll = Random.value;
            Debug.Log($"BatEvent roll: chance={batEventChance}, roll={roll}, force={forceBatEvent}, isActive={isBatEventActive}");
            if (forceBatEvent || roll <= batEventChance)
            {
                Debug.Log("BatEvent triggered this wave.");
                StartCoroutine(RunBatEvent());
            }
            else
            {
                Debug.Log("BatEvent NOT triggered this wave.");
            }
        }
        else if (isBatEventActive)
        {
            Debug.Log("BatEvent skipped because another bat event is already active.");
        }

        // Trigger wave started event
        OnWaveStarted?.Invoke(currentWave + 1);
        Debug.Log($"Wave {currentWave + 1} started!");
    }

    IEnumerator RunBatEvent()
    {
        // mark event active so another won't be started while this one runs
        isBatEventActive = true;

        // enable UI text if assigned
        if (batEventUIText != null)
            batEventUIText.SetActive(true);

        // spawn the bat at a random spawn point
        Transform spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];
        GameObject bat = Instantiate(batEventPrefab, spawnPoint.position, spawnPoint.rotation);

        // Make the spawned bat effectively invulnerable / not part of spawner enemies by
        // removing any EnemyHealth component so it cannot be killed by normal damage flow.
        var enemyHealth = bat.GetComponent("EnemyHealth");
        if (enemyHealth != null)
        {
            Debug.Log("Bat event: removing EnemyHealth component to make bat unkillable.");
            Destroy(enemyHealth);
        }

        Debug.Log("Bat event started: spawned bat and enabled UI text.");

        // wait for event duration
        float t = 0f;
        while (t < batEventDuration)
        {
            t += Time.deltaTime;
            yield return null;
        }

        // disable UI and destroy spawned bat if it still exists
        if (batEventUIText != null)
            batEventUIText.SetActive(false);

        if (bat != null)
            Destroy(bat);

        // mark event finished so a new one can be rolled in future waves
        isBatEventActive = false;

        Debug.Log("Bat event ended: UI hidden and bat destroyed. Event inactive.");
    }

    IEnumerator SpawnWave(Wave wave)
    {
        int enemiesSpawned = 0;

        while (enemiesSpawned < wave.enemyCount)
        {
            Transform spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];
            GameObject enemyPrefab = wave.enemyPrefabs[Random.Range(0, wave.enemyPrefabs.Length)];

            GameObject enemy = Instantiate(enemyPrefab, spawnPoint.position, spawnPoint.rotation);

            // Scale enemy stats for endless mode
            if (currentWave >= waves.Length)
            {
                ScaleEnemyForEndless(enemy, currentWave - waves.Length + 1);
            }

            enemiesSpawned++;
            yield return new WaitForSeconds(wave.spawnInterval);
        }
    }

    void ScaleEnemyForEndless(GameObject enemy, int endlessWaveIndex)
    {
        EnemyHealth health = enemy.GetComponent<EnemyHealth>();
        EnemyAttack attack = enemy.GetComponent<EnemyAttack>();

        if (health != null)
        {
            health.maxHealth *= Mathf.Pow(enemyHealthMultiplier, endlessWaveIndex);
            health.currentHealth = health.maxHealth;
        }

        if (attack != null)
        {
            attack.damage *= Mathf.Pow(enemyDamageMultiplier, endlessWaveIndex);
        }
    }

    void CreateEndlessWave()
    {
        Wave lastWave = waves[waves.Length - 1];
        Wave newWave = new Wave()
        {
            enemyPrefabs = lastWave.enemyPrefabs,
            enemyCount = Mathf.RoundToInt(lastWave.enemyCount * 1.2f),
            spawnInterval = lastWave.spawnInterval * spawnRateMultiplier,
            waveDuration = lastWave.waveDuration * 1.1f
        };

        // Add new wave to array or use a list for dynamic expansion
        System.Array.Resize(ref waves, waves.Length + 1);
        waves[waves.Length - 1] = newWave;
    }

    public void EnemyDefeated()
    {
        enemiesRemaining--;
    }

    void CompleteWave()
    {
        waveInProgress = false;
        
        // Trigger wave completed event BEFORE incrementing currentWave
        OnWaveCompleted?.Invoke(currentWave + 1);
        
        currentWave++;
        Debug.Log($"Wave {currentWave} completed!");

        StartCoroutine(StartNextWaveAfterDelay());
    }

    IEnumerator StartNextWaveAfterDelay()
    {
        yield return new WaitForSeconds(timeBetweenWaves);
        StartNextWave();
    }

    // --- UI/Public Getters ---
    public int GetCurrentWave() => currentWave + 1;
    public int GetEnemiesRemaining() => enemiesRemaining;
    public float GetWaveTimeLeft() => waveTimer;
    public bool IsWaveInProgress() => waveInProgress;
}
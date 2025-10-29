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

        // Trigger wave started event
        OnWaveStarted?.Invoke(currentWave + 1);
        Debug.Log($"Wave {currentWave + 1} started!");
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
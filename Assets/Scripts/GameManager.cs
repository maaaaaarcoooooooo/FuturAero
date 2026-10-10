using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace GasHunter
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Level")]
        [SerializeField] float killPlaneY = -12f;
        [SerializeField] int totalEnemies = 0;
        [SerializeField] int livesOnStart = 3;

        [Header("Audio")]
        [SerializeField] AudioSource sfxSource;
        [SerializeField] AudioClip shootSound;
        [SerializeField] AudioClip emptyShotSound;
        [SerializeField] AudioClip absorbSound;
        [SerializeField] AudioClip jumpSound;
        [SerializeField] AudioClip landSound;
        [SerializeField] AudioClip hitSound;
        [SerializeField] AudioClip deathSound;
        [SerializeField] AudioClip enemyDeathSound;
        [SerializeField] AudioClip victorySound;
        [SerializeField] AudioClip respawnSound;
        [SerializeField] AudioClip fallSound;
        [SerializeField] float minPitch = 0.92f;
        [SerializeField] float maxPitch = 1.1f;

        [Header("HUD")]
        [SerializeField] GameHud hud;

        [Header("Boss")]
        [SerializeField] GameObject finalBossPrefab;
        [SerializeField] Vector3 bossSpawnPosition = new Vector3(156f, 4.9f, 0f);
        [SerializeField] string bossSpawnMessage = "¡EL JEFE HA APARECIDO!";

        public float KillPlaneY => killPlaneY;
        public int TotalEnemies => totalEnemies;
        public int EnemiesKilled => enemiesKilled;
        public int OrbsAbsorbed => orbsAbsorbed;
        public int ShotsFired => shotsFired;

        int enemiesKilled;
        int orbsAbsorbed;
        int shotsFired;
        bool levelComplete;
        bool bossSpawned;
        bool bossDefeated;

        void Awake()
        {
            Instance = this;

            if (sfxSource == null)
            {
                sfxSource = gameObject.AddComponent<AudioSource>();
                sfxSource.playOnAwake = false;
                sfxSource.spatialBlend = 0f;
            }

            enemiesKilled = 0;
            orbsAbsorbed = 0;
            shotsFired = 0;
            levelComplete = false;
            bossSpawned = false;
            bossDefeated = false;
        }

        void Start()
        {
            PlayerHealth health = PlayerController.Instance != null
                ? PlayerController.Instance.GetComponent<PlayerHealth>()
                : null;

            if (health != null)
            {
                health.Damaged += OnPlayerDamaged;
                health.Died += OnPlayerDied;
                health.LivesChanged += OnLivesChanged;
            }

            if (hud != null)
            {
                hud.Bind(this, health, PlayerController.Instance != null ? PlayerController.Instance.GetComponent<GasTank>() : null);
                OnLivesChanged(health != null ? health.Lives : livesOnStart);
            }
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void RegisterShot()
        {
            shotsFired++;
            Play(shootSound, 0.55f, Random.Range(0.95f, 1.12f));
        }

        public void RegisterEmptyShot()
        {
            Play(emptyShotSound, 0.35f, Random.Range(1.3f, 1.45f));
        }

        public void RegisterShotHit()
        {
            Play(enemyDeathSound, 0.5f, Random.Range(0.95f, 1.08f));
        }

        public void RegisterOrbAbsorbed()
        {
            orbsAbsorbed++;
            Play(absorbSound, 0.5f, Random.Range(0.98f, 1.22f));
        }

        public void RegisterJump()
        {
            Play(jumpSound, 0.4f, Random.Range(minPitch, maxPitch));
        }

        public void RegisterLanding()
        {
            Play(landSound, 0.3f, Random.Range(0.9f, 1f));
        }

        public void RegisterEnemyKilled()
        {
            enemiesKilled++;

            if (hud != null)
            {
                hud.SetEnemyCounter(enemiesKilled, totalEnemies);
            }

            CheckLevelComplete();
        }

        public void SetEnemyTotal(int total)
        {
            totalEnemies = total;

            if (hud != null)
            {
                hud.SetEnemyCounter(enemiesKilled, totalEnemies);
            }
        }

        public void PlayFallSound()
        {
            Play(fallSound, 0.6f, 1f);
        }

        void OnPlayerDamaged()
        {
            Play(hitSound, 0.65f, 1f);
        }

        void OnLivesChanged(int lives)
        {
            if (hud != null)
            {
                hud.SetLives(lives);
            }
        }

        void OnPlayerDied()
        {
            Play(deathSound, 0.8f, 1f);
            StopAllCoroutines();
            StartCoroutine(RestartAfterDelay(1.6f));
        }

        public void NotifyPlayerRespawn()
        {
            Play(respawnSound, 0.6f, 1f);
        }

        void CheckLevelComplete()
        {
            if (levelComplete || totalEnemies <= 0 || enemiesKilled < totalEnemies)
            {
                return;
            }

            if (!bossSpawned)
            {
                SpawnBoss();
                return;
            }

            levelComplete = true;
            Play(victorySound, 0.8f, 1f);

            if (hud != null)
            {
                hud.ShowMessage($"¡NIVEL COMPLETADO!\n{enemiesKilled} gases purificados");
            }
        }

        public void RegisterBossDefeated()
        {
            if (bossDefeated)
            {
                return;
            }

            bossDefeated = true;
            CheckLevelComplete();
        }

        void SpawnBoss()
        {
            bossSpawned = true;

            if (finalBossPrefab != null)
            {
                Instantiate(finalBossPrefab, bossSpawnPosition, Quaternion.identity);
            }

            if (hud != null)
            {
                hud.ShowMessage(bossSpawnMessage);
            }
        }

        void Play(AudioClip clip, float volume, float pitch)
        {
            if (clip == null || sfxSource == null)
            {
                return;
            }

            sfxSource.pitch = Mathf.Clamp(pitch, 0.5f, 2f);
            sfxSource.PlayOneShot(clip, volume);
        }

        IEnumerator RestartAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            Scene current = SceneManager.GetActiveScene();
            SceneManager.LoadScene(current.buildIndex >= 0 ? current.buildIndex : 0);
        }

        public void RestartLevel()
        {
            Scene current = SceneManager.GetActiveScene();
            SceneManager.LoadScene(current.buildIndex >= 0 ? current.buildIndex : 0);
        }
    }
}
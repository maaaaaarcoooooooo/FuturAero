using UnityEngine;
using UnityEngine.UI;

namespace GasHunter
{
    public class GameHud : MonoBehaviour
    {
        [SerializeField] Slider gasBar;
        [SerializeField] Image gasFill;
        [SerializeField] Text livesText;
        [SerializeField] Text enemiesText;
        [SerializeField] Text messageText;
        [SerializeField] CanvasGroup messageGroup;
        [SerializeField] Color lowGasColor = new Color(1f, 0.45f, 0.35f, 1f);
        [SerializeField] Color normalGasColor = new Color(0.4f, 0.95f, 0.7f, 1f);
        [SerializeField] float lowGasThreshold = 0.25f;

        GameManager manager;
        PlayerHealth health;
        GasTank tank;
        float messageTimer = -1f;

        public void Bind(GameManager gameManager, PlayerHealth playerHealth, GasTank gasTank)
        {
            manager = gameManager;
            health = playerHealth;
            tank = gasTank;

            if (tank != null)
            {
                tank.GasChanged += OnGasChanged;
                OnGasChanged(tank.CurrentGas, tank.MaxGas);
            }

            if (health != null)
            {
                health.LivesChanged += SetLives;
            }

            UpdateEnemies();
        }

        void OnDestroy()
        {
            if (tank != null)
            {
                tank.GasChanged -= OnGasChanged;
            }

            if (health != null)
            {
                health.LivesChanged -= SetLives;
            }
        }

        void Update()
        {
            if (messageTimer > 0f)
            {
                messageTimer -= Time.deltaTime;
                if (messageTimer <= 0f && messageGroup != null)
                {
                    messageGroup.alpha = 0f;
                    messageGroup.blocksRaycasts = false;
                }
            }
        }

        void OnGasChanged(float current, float max)
        {
            float normalized = max > 0f ? current / max : 0f;

            if (gasBar != null)
            {
                gasBar.SetValueWithoutNotify(normalized);
            }

            if (gasFill != null)
            {
                gasFill.color = normalized <= lowGasThreshold ? lowGasColor : normalGasColor;
            }
        }

        public void SetLives(int lives)
        {
            if (livesText != null)
            {
                livesText.text = $"VIDAS\n{lives}";
            }
        }

        void UpdateEnemies()
        {
            if (enemiesText == null || manager == null)
            {
                return;
            }

            enemiesText.text = $"GASES\n{manager.TotalEnemies}";
        }

        public void ShowMessage(string message, float duration = 3f)
        {
            if (messageText == null)
            {
                return;
            }

            messageText.text = message;
            messageTimer = duration;

            if (messageGroup != null)
            {
                messageGroup.alpha = 1f;
                messageGroup.blocksRaycasts = false;
            }
        }

        public void SetEnemyCounter(int current, int total)
        {
            if (enemiesText != null)
            {
                enemiesText.text = $"GASES\n{current}/{total}";
            }
        }
    }
}
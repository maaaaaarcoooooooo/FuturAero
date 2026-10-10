using System;
using UnityEngine;

namespace GasHunter
{
    public class GasTank : MonoBehaviour
    {
        [SerializeField] float maxGas = 100f;
        [SerializeField] float gasPerShot = 14f;
        [SerializeField] float gasPerOrb = 16f;
        [SerializeField] float passiveRegenPerSecond = 3.5f;
        [SerializeField] float regenDelay = 0.9f;
        [SerializeField] float recoilForce = 3.5f;

        public event Action<float, float> GasChanged;

        public float MaxGas => maxGas;
        public float CurrentGas { get; private set; }
        public float Normalized => maxGas > 0f ? CurrentGas / maxGas : 0f;
        public bool HasEnoughForShot => CurrentGas >= gasPerShot;
        public float GasPerShot => gasPerShot;

        float regenTimer;

        void Awake()
        {
            CurrentGas = gasPerShot;
        }

        public void SetShotCost(float cost)
        {
            gasPerShot = Mathf.Max(1f, cost);
        }

        public bool TryConsumeShot()
        {
            if (CurrentGas < gasPerShot)
            {
                return false;
            }

            CurrentGas -= gasPerShot;
            regenTimer = regenDelay;
            GasChanged?.Invoke(CurrentGas, maxGas);
            return true;
        }

        public void Absorb(float amount)
        {
            CurrentGas = Mathf.Min(maxGas, CurrentGas + amount);
            GasChanged?.Invoke(CurrentGas, maxGas);
        }

        public void AbsorbOrb()
        {
            Absorb(gasPerOrb);
        }

        public float ConsumeRecoil()
        {
            return recoilForce;
        }

        void Update()
        {
            if (regenTimer > 0f)
            {
                regenTimer -= Time.deltaTime;
                return;
            }

            if (CurrentGas < maxGas)
            {
                CurrentGas = Mathf.Min(maxGas, CurrentGas + passiveRegenPerSecond * Time.deltaTime);
                GasChanged?.Invoke(CurrentGas, maxGas);
            }
        }
    }
}
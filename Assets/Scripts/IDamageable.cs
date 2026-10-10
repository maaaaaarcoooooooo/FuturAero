using UnityEngine;

namespace GasHunter
{
    public interface IDamageable
    {
        void TakeDamage(Vector2 knockbackDirection);
    }
}
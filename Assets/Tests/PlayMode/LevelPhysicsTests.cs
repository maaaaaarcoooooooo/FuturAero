using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;

namespace GasHunter.Tests
{
    public class LevelPhysicsTests
    {
        const string SceneName = "SampleScene";

        // Columns verified against the builder map: no one-way platform above them, so a
        // raycast from high up must land on the terrain surface. The terrain top is y=3
        // where row 2 is solid and y=4 where row 3 is solid.
        // Spike columns are listed separately: their trigger spans y 3.0-3.5, so a raycast
        // from above legitimately lands on the spike before the terrain.
        static readonly int[] SurfaceY3 =
        {
            0, 1, 2, 3, 14, 16, 18, 20, 41, 55, 60, 71, 78, 95, 100, 114, 119, 133, 140, 150, 153
        };
        static readonly int[] SurfaceY4 = { 4, 5, 6, 7, 31, 33, 35, 37, 39, 105, 111, 112, 154, 156, 158 };
        static readonly int[] SpikeColumns = { 11, 12, 53, 54, 85, 86, 124, 125 };

        // Verified open ground columns (no terrain tile at row 2). Some of them still carry
        // spikes or a one-way platform higher up, which is intentional, so the pit test only
        // asserts the absence of terrain rather than the absence of any collider.
        static readonly int[] PitColumns =
        {
            50, 51, 52, 53, 84, 85, 86, 87, 88, 89, 90, 91, 128, 129, 130, 131
        };

        static int GroundMask => 1 << LayerMask.NameToLayer(GameLayers.Ground);

        static T[] FindAll<T>() where T : Object
        {
            return Object.FindObjectsByType<T>(FindObjectsInactive.Include);
        }

        static T FindOne<T>() where T : Object
        {
            return Object.FindAnyObjectByType<T>();
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            SceneManager.LoadScene(SceneName, LoadSceneMode.Single);
            yield return null;
            yield return new WaitForFixedUpdate();
        }

        static IEnumerator Settle(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end)
            {
                yield return new WaitForFixedUpdate();
            }
        }

        // ------------------------------------------------------------------ structure

        [UnityTest]
        public IEnumerator Scene_ContainsExpectedEntities()
        {
            Assert.IsNotNull(GameObject.Find("Level"), "Level root missing");
            Assert.IsNotNull(GameObject.Find("HUD"), "HUD missing");
            Assert.IsNotNull(GameObject.Find("GameManager"), "GameManager missing");
            Assert.IsNotNull(Camera.main, "Main camera missing");

            Assert.AreEqual(1, FindAll<PlayerController>().Length, "player count");
            Assert.AreEqual(10, FindAll<EnemyHealth>().Length, "enemy count");
            Assert.AreEqual(9, FindAll<GasOrb>().Length, "orb count");
            Assert.AreEqual(8, FindAll<HazardArea>().Length, "spike trigger count");
            yield break;
        }

        [UnityTest]
        public IEnumerator Ground_CastsHitFromAbove()
        {
            foreach (int x in SurfaceY3)
            {
                AssertSurface(x, 3f);
            }

            foreach (int x in SurfaceY4)
            {
                AssertSurface(x, 4f);
            }

            // Spike columns keep the same terrain underneath, which the raycast cannot see.
            var solids = FindSolids();
            foreach (int x in SpikeColumns)
            {
                Assert.IsTrue(solids.HasTile(new Vector3Int(x, 2, 0)) || solids.HasTile(new Vector3Int(x, 3, 0)),
                    $"spike column {x} should still have terrain below it");
            }

            yield break;
        }

        static Tilemap FindSolids()
        {
            var grid = Object.FindAnyObjectByType<Grid>();
            Assert.IsNotNull(grid, "grid missing");
            var solids = grid.transform.Find("Solids").GetComponent<Tilemap>();
            Assert.IsNotNull(solids, "Solids tilemap missing");
            return solids;
        }

        void AssertSurface(int x, float expectedY)
        {
            RaycastHit2D hit = Physics2D.Raycast(
                new Vector2(x + 0.5f, 18f), Vector2.down, 40f, GroundMask);

            Assert.IsNotNull(hit.collider, $"column {x} has no ground collider");
            Assert.AreEqual("Solids", hit.collider.name, $"column {x} hit unexpected collider");
            Assert.AreEqual(expectedY, hit.point.y, 0.35f, $"column {x} surface height");
        }

        [UnityTest]
        public IEnumerator Pits_AreOpen()
        {
            var solids = FindSolids();

            var blocked = new System.Collections.Generic.List<int>();
            foreach (int x in PitColumns)
            {
                // No terrain tile at the surface row, and nothing solid to land on below it.
                bool hasSurfaceTile = solids.HasTile(new Vector3Int(x, 2, 0));

                RaycastHit2D hit = Physics2D.Raycast(
                    new Vector2(x + 0.5f, 18f), Vector2.down, 40f, GroundMask);

                bool terrainInColumn = hit.collider != null && hit.collider.name == "Solids";

                if (hasSurfaceTile || terrainInColumn)
                {
                    blocked.Add(x);
                }
            }

            Assert.IsEmpty(blocked, "pit columns unexpectedly solid: " + string.Join(",", blocked));

            // A non-pit column must still be solid, so the assertion above has meaning.
            Assert.IsTrue(solids.HasTile(new Vector3Int(20, 2, 0)), "control column 20 should be solid");
            yield break;
        }

        // ------------------------------------------------------------------ player physics

        [UnityTest]
        public IEnumerator Player_LandsOnGroundAndStops()
        {
            PlayerController player = FindOne<PlayerController>();
            Assert.IsNotNull(player);

            yield return Settle(1.0f);

            var body = player.GetComponent<Rigidbody2D>();
            var box = player.GetComponent<BoxCollider2D>();
            float feet = player.transform.position.y + box.offset.y - box.size.y * 0.5f;

            Assert.Less(Mathf.Abs(body.linearVelocity.y), 1.0f,
                $"player still falling (vy={body.linearVelocity.y}, y={player.transform.position.y})");
            // The player spawns at x=2.5 and column 2 has its terrain surface at y=3.
            Assert.AreEqual(3f, feet, 0.25f, "player feet should rest on ground surface");
            Assert.IsTrue(player.IsGrounded, "player should report grounded");
        }

        // The jump cut fires as soon as the key is NOT held, so tests that want a full-height
        // jump must simulate a held key. There is no keyboard in PlayMode tests, so the cleanest
        // equivalent is to invoke Jump() and flag the cut as already consumed.
        static void JumpWithHeldKey(PlayerController player, float powerScale)
        {
            player.SendMessage("Jump", powerScale, SendMessageOptions.DontRequireReceiver);
            typeof(PlayerController)
                .GetField("jumpCutApplied", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(player, true);
        }

        // Regression guard for the jump-cut bug: the velocity cut used to run every frame while
        // the key was held, collapsing the apex to ~0.2 units. Asserts a held jump clears 4 units.
        [UnityTest]
        public IEnumerator Player_FullHeldJump_ClearsFourUnits()
        {
            PlayerController player = FindOne<PlayerController>();
            var body = player.GetComponent<Rigidbody2D>();

            yield return Settle(0.8f);

            float start = player.transform.position.y;
            JumpWithHeldKey(player, 1f);

            float apex = start;
            for (int i = 0; i < 120; i++)
            {
                yield return new WaitForFixedUpdate();
                apex = Mathf.Max(apex, player.transform.position.y);
                if (body.linearVelocity.y <= 0f && apex > start)
                {
                    break;
                }
            }

            Assert.Greater(apex - start, 4f,
                $"held jump should clear 4 units (start={start}, apex={apex})");
            Assert.Less(apex - start, 40f, "jump apex should stay physically plausible");
        }

        // A released jump is cut short. Guards the other half of the change: the cut must still
        // happen when the key is let go, otherwise the variable-height jump is meaningless.
        [UnityTest]
        public IEnumerator Player_ReleasedJump_IsCutShort()
        {
            PlayerController player = FindOne<PlayerController>();
            var body = player.GetComponent<Rigidbody2D>();

            yield return Settle(0.8f);

            float start = player.transform.position.y;
            // Jump() without flagging the cut: no key is held in tests, so the cut applies.
            player.SendMessage("Jump", 1f, SendMessageOptions.DontRequireReceiver);

            float apex = start;
            for (int i = 0; i < 120; i++)
            {
                yield return new WaitForFixedUpdate();
                apex = Mathf.Max(apex, player.transform.position.y);
                if (body.linearVelocity.y <= 0f && apex > start)
                {
                    break;
                }
            }

            float rise = apex - start;
            Assert.Greater(rise, 0.5f, $"released jump should still rise a little (rise={rise})");
            Assert.Less(rise, 8f, $"released jump should be cut below full height (rise={rise})");
        }

        [UnityTest]
        public IEnumerator Player_CanJump()
        {
            PlayerController player = FindOne<PlayerController>();
            yield return Settle(0.8f);

            float before = player.transform.position.y;
            JumpWithHeldKey(player, 1f);

            yield return Settle(0.35f);

            Assert.Greater(player.transform.position.y, before + 0.5f,
                $"jump should raise player (before={before}, after={player.transform.position.y})");
        }

        // ------------------------------------------------------------------ one-way platforms

        [UnityTest]
        public IEnumerator OneWayPlatform_BlocksFromAbove()
        {
            // map row y=12, block 0: "..XXXX.............." -> global x = 2..5, occupying world y 12..13
            var misses = new System.Collections.Generic.List<int>();
            for (int x = 2; x <= 5; x++)
            {
                RaycastHit2D hit = Physics2D.Raycast(
                    new Vector2(x + 0.5f, 16f), Vector2.down, 25f, GroundMask);
                if (hit.collider == null)
                {
                    misses.Add(x);
                }
            }

            Assert.IsEmpty(misses, "one-way platform missing collider at x: " + string.Join(",", misses));
            yield break;
        }

        // ------------------------------------------------------------------ combat wiring

        [UnityTest]
        public IEnumerator Shot_CostsGasAndRegistersWithGameManager()
        {
            PlayerController player = FindOne<PlayerController>();
            GasTank tank = player.GetComponent<GasTank>();
            GameManager gm = GameManager.Instance;

            Assert.IsNotNull(tank, "player needs GasTank");
            Assert.IsNotNull(gm, "GameManager.Instance not set");
            yield return Settle(0.4f);

            // Refill rather than drain: firing with an empty tank takes the
            // RegisterEmptyShot path and never spawns a projectile.
            tank.Absorb(tank.MaxGas);
            yield return null;

            Assert.IsTrue(tank.HasEnoughForShot, "tank should be full before firing");

            float gasBefore = tank.CurrentGas;
            int shotsBefore = gm.ShotsFired;

            player.SendMessage("TryFire", SendMessageOptions.DontRequireReceiver);

            Assert.AreEqual(1, gm.ShotsFired - shotsBefore, "one shot should be registered");
            Assert.Less(tank.CurrentGas, gasBefore, "firing should consume gas");

            yield return Settle(0.2f);
        }

        [UnityTest]
        public IEnumerator Enemy_TakesDamageAndDies()
        {
            EnemyHealth enemy = FindAll<EnemyHealth>()[0];
            GameManager gm = GameManager.Instance;
            int total = gm.TotalEnemies;
            int killed = gm.EnemiesKilled;

            for (int i = 0; i < 60 && enemy != null; i++)
            {
                enemy.TakeDamage(Vector2.up);
                yield return null;
            }

            yield return Settle(0.5f);

            Assert.Greater(gm.EnemiesKilled, killed, "killing an enemy should increment counter");
            Assert.AreEqual(total, gm.TotalEnemies, "total should stay stable");
        }

        [UnityTest]
        public IEnumerator Orb_IsAbsorbedByPlayer()
        {
            PlayerController player = FindOne<PlayerController>();
            GasTank tank = player.GetComponent<GasTank>();
            GasOrb orb = FindAll<GasOrb>()[0];

            while (tank.TryConsumeShot())
            {
            }

            float before = tank.CurrentGas;
            orb.Launch(player.transform, Vector2.zero, 16f);
            orb.transform.position = player.transform.position + new Vector3(0.1f, 0f, 0f);

            yield return Settle(0.6f);

            Assert.Greater(tank.CurrentGas, before, "orb next to player should be absorbed");
        }

        [UnityTest]
        public IEnumerator Spikes_DamagePlayer()
        {
            PlayerController player = FindOne<PlayerController>();
            PlayerHealth health = player.GetComponent<PlayerHealth>();
            HazardArea hazard = FindAll<HazardArea>()[0];

            yield return Settle(0.4f);
            int lives = health.Lives;
            player.transform.position = hazard.transform.position + new Vector3(0f, -0.6f, 0f);

            yield return Settle(0.4f);

            Assert.Less(health.Lives, lives, "standing in spikes should cost a life");
        }
    }
}

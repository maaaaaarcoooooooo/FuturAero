using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Tilemaps;
using UnityEngine.Rendering.Universal;
using GasHunter;
using Object = UnityEngine.Object;

namespace GasHunter.EditorTools
{
    public static class LevelBuilder
    {
        const int Cols = 160;
        const int Rows = 20;
        const float KillPlaneY = -9f;

        // tileset indices (r = row from top, c = column) -> 8*r + c
        const int TileGroundTopBlue = 25;
        const int TileGroundFillBlue = 19;
        const int TileGroundTopGreen = 57;
        const int TileGroundFillGreen = 51;
        const int TilePlatformBlue = 11;
        const int TilePlatformGreen = 43;
        const int TileSpikes = 5;
        const int TileDarkBlock = 27;
        const int TileDarkWindow = 28;
        const int TilePillar = 3;
        const int TileGrate = 2;
        const int TileNeon = 7;

        static readonly Dictionary<long, Tile> TileCache = new Dictionary<long, Tile>();

        // Solid and decor tiles of the same index live in different folders and differ in
        // colliderType, so the cache key has to include the solid flag.
        static long TileKey(int index, bool solid) => (long)index * 2 + (solid ? 1 : 0);


        // ------------------------------------------------------------------ map

        // index 0 is the TOP row (y = Rows-1)
        static readonly string[] Map =
        {
            // y = 19
            "....................|....................|....................|......XXXX..........|....................|..........XXXX......|....................|....................",
            // y = 18
            "....................|....................|........bb..........|....................|........bb..........|....................|....................|....................",
            // y = 17
            "....................|......G.............|....................|....................|....................|....................|.........G..........|....................",
            // y = 16
            "....................|......G.............|....................|....................|.....G..............|....................|.........G..........|....................",
            // y = 15
            "....................|....XXXXXX..........|......XXXX..........|....................|....XXXX............|......XXXXX.........|....................|......XXXX..........",
            // y = 14
            "....................|....................|....................|..............bb....|....................|....................|....................|..............bb....",
            // y = 13
            "....................|...G................|....................|.....G..............|....................|....................|....G................|....................",
            // y = 12
            "....................|..XXXX..............|....................|............XXXX....|....................|....................|..XXXXX.............|....................",
            // y = 11
            "........G...........|....................|.............bb.....|....................|....................|..............bb....|....................|....................",
            // y = 10
            "........XXXX........|....................|....XXXXX...........|....................|.......XXXX.........|....................|..............XXXX..|....................",
            // y = 9
            "....................|....................|....................|....................|....................|....................|....................|....................",
            // y = 8
            "....................|........E...........|....................|......E.............|....................|........E...........|....................|.....E..............",
            // y = 7
            "....................|........XXX.........|....................|..XXXXXXXX..........|....................|........XXX.........|....................|....XXX.............",
            // y = 6
            "....................|....................|....oo..............|....................|............oo......|....................|............oo......|....................",
            // y = 5
            "....................|..oo................|....................|..........oo........|....................|........oo..........|....................|....................",
            // y = 4
            "....................|....................|....ooww............|....................|....oowwoo..........|....................|..........ooww......|....................",
            // y = 3
            "..P.####...^^.......|..........##########|..E..........^^......|.................E..|....^^.........E....|....########...E.....|..^^..........E.....|........E...########",
            // y = 2
            "####################|####################|##########....######|####################|####........########|####################|########....########|####################",
            // y = 1
            "####################|####################|##########....######|####################|####........########|####################|########....########|####################",
            // y = 0
            "################....|################....|##########....######|####################|####........########|################....|########....########|################....",
        };

        // ------------------------------------------------------------------ entry

        [MenuItem("GasHunter/Build Level")]
        public static void BuildLevel()
        {
            GasArt.EnsureFolders();
            BuildTiles();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "SampleScene";

            var root = new GameObject("Level").transform;

            BuildParallax(root);
            BuildTilemaps(root);
            var spawnPoint = new GameObject("PlayerSpawn").transform;
            spawnPoint.SetParent(root, true);
            spawnPoint.position = new Vector3(2.5f, 5f, 0f);

            GasProjectile projectilePrefab = BuildProjectilePrefab();
            GasOrb orbPrefab = BuildOrbPrefab();
            EnemyHealth enemyPrefab = BuildEnemyPrefab(orbPrefab);

            var enemies = new List<GameObject>();
            BuildPlayer(root, spawnPoint, projectilePrefab);
            BuildEntities(root, enemyPrefab, enemies);

            Camera cam = BuildCamera(root);
            BuildLights(root);
            GameHud hud = BuildHud();

            var manager = BuildManager(hud);
            manager.SetEnemyTotal(enemies.Count);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/SampleScene.unity");
            AddSceneToBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[GasHunter] Level built. {enemies.Count} enemies, {CountChar('G')} orbs, {CountChar('E')} enemies placed.");
        }

        static int CountChar(char c)
        {
            int total = 0;
            foreach (string row in Map)
            {
                total += row.Count(ch => ch == c);
            }

            return total;
        }

        // ------------------------------------------------------------------ tiles

        static void BuildTiles()
        {
            TileCache.Clear();
            GasArt.EnsureFolder(GasArt.TileFolder, "Solid");
            GasArt.EnsureFolder(GasArt.TileFolder, "Decor");

            foreach (int index in SolidTileIndices())
            {
                GetTile(index, true);
            }

            foreach (int index in DecorTileIndices())
            {
                GetTile(index, false);
            }
        }

        static IEnumerable<int> SolidTileIndices()
        {
            yield return TileGroundTopBlue;
            yield return TileGroundFillBlue;
            yield return TileGroundTopGreen;
            yield return TileGroundFillGreen;
            yield return TilePlatformBlue;
            yield return TilePlatformGreen;
        }

        static IEnumerable<int> DecorTileIndices()
        {
            yield return TileDarkBlock;
            yield return TileDarkWindow;
            yield return TilePillar;
            yield return TileGrate;
            yield return TileNeon;
            yield return TileSpikes;
        }

        static Tile GetTile(int index, bool solid)
        {
            long key = TileKey(index, solid);
            if (TileCache.TryGetValue(key, out Tile cached))

            {
                return cached;
            }

            string spriteName = $"{Path.GetFileNameWithoutExtension(GasArt.TilesetPath)}_{index}";
            Sprite sprite = GasArt.LoadSubSprite(GasArt.TilesetPath, spriteName);

            if (sprite == null)
            {
                Debug.LogError($"[GasHunter] tileset sprite not found: {spriteName}");
                return null;
            }

            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.name = $"Tile_{index}";
            tile.sprite = sprite;
            tile.colliderType = solid ? Tile.ColliderType.Grid : Tile.ColliderType.None;
            tile.color = Color.white;

            string folder = solid ? $"{GasArt.TileFolder}/Solid" : $"{GasArt.TileFolder}/Decor";
            string path = $"{folder}/Tile_{index}.asset";

            // Rebuilding the level must not re-create the tile assets: CreateAsset would throw
            // (or silently leak the previous instance) because the file already exists.
            var existing = AssetDatabase.LoadAssetAtPath<Tile>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(tile, path);
            }
            else
            {
                tile = existing;
                tile.sprite = sprite;
                tile.colliderType = solid ? Tile.ColliderType.Grid : Tile.ColliderType.None;
                tile.color = Color.white;
                EditorUtility.SetDirty(tile);
            }

            TileCache[key] = tile;
            return tile;
        }

        static char CharAt(int x, int y)
        {
            if (x < 0 || x >= Cols || y < 0 || y >= Rows)
            {
                return '.';
            }

            int row = Rows - 1 - y;
            string line = Map[row];
            int block = x / 20;
            int local = x % 20;
            int start = block * 21;
            return line[start + local];
        }

        // ------------------------------------------------------------------ tilemaps

        static void BuildTilemaps(Transform root)
        {
            var gridGo = new GameObject("Grid");
            gridGo.transform.SetParent(root, false);
            var grid = gridGo.AddComponent<Grid>();
            grid.cellSize = new Vector3(1f, 1f, 0f);
            grid.cellSwizzle = GridLayout.CellSwizzle.XYZ;

            var decorGo = new GameObject("BackgroundDecor");
            decorGo.transform.SetParent(gridGo.transform, false);
            Tilemap decor = decorGo.AddComponent<Tilemap>();
            TilemapRenderer decorRenderer = decorGo.AddComponent<TilemapRenderer>();
            decorRenderer.sortingOrder = -60;

            var solidGo = new GameObject("Solids");
            solidGo.transform.SetParent(gridGo.transform, false);
            Tilemap solids = solidGo.AddComponent<Tilemap>();
            TilemapRenderer solidRenderer = solidGo.AddComponent<TilemapRenderer>();
            solidRenderer.sortingOrder = 0;

            var body = solidGo.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Static;
            solidGo.layer = LayerMask.NameToLayer(GameLayers.Ground);

            var oneWayGo = new GameObject("OneWayPlatforms");
            oneWayGo.transform.SetParent(gridGo.transform, false);
            Tilemap oneWay = oneWayGo.AddComponent<Tilemap>();
            TilemapRenderer oneWayRenderer = oneWayGo.AddComponent<TilemapRenderer>();
            oneWayRenderer.sortingOrder = 1;

            var oneWayBody = oneWayGo.AddComponent<Rigidbody2D>();
            oneWayBody.bodyType = RigidbodyType2D.Static;
            oneWayGo.layer = LayerMask.NameToLayer(GameLayers.Ground);

            for (int y = 0; y < Rows; y++)
            {
                for (int x = 0; x < Cols; x++)
                {
                    char c = CharAt(x, y);
                    if (c == '.')
                    {
                        continue;
                    }

                    var cell = new Vector3Int(x, y, 0);

                    switch (c)
                    {
                        case '#':
                            bool top = CharAt(x, y + 1) != '#';
                            bool green = y >= 3 && IsGreenZone(x);
                            solids.SetTile(cell, GetTile(
                                top
                                    ? (green ? TileGroundTopGreen : TileGroundTopBlue)
                                    : (green ? TileGroundFillGreen : TileGroundFillBlue),
                                true));
                            break;

                        case 'X':
                            oneWay.SetTile(cell, GetTile(IsGreenZone(x) ? TilePlatformGreen : TilePlatformBlue, true));
                            break;

                        case '^':
                            solids.SetTile(cell, GetTile(TileSpikes, false));
                            break;

                        case 'o':
                            decor.SetTile(cell, GetTile(TileDarkBlock, false));
                            break;

                        case 'w':
                            decor.SetTile(cell, GetTile(TileDarkWindow, false));
                            break;

                        case 'b':
                            decor.SetTile(cell, GetTile(TilePillar, false));
                            break;

                        case 'g':
                            decor.SetTile(cell, GetTile(TileGrate, false));
                            break;

                        case 'n':
                            decor.SetTile(cell, GetTile(TileNeon, false));
                            break;
                    }
                }
            }

            // Colliders are created only after the tiles exist. In Unity 6 a
            // CompositeCollider2D added to a tilemap before it holds any tiles never
            // picks the TilemapCollider2D up as a source, which leaves the whole
            // tilemap without collision.
            solidGo.AddComponent<TilemapCollider2D>();
            BuildOneWayColliders(root);

            BuildSpikeTriggers(root);
        }

        // One-way platforms use explicit box colliders instead of a TilemapCollider2D:
        // CompositeCollider2D only merges plain colliders in this Unity version, and the
        // PlatformEffector2D needs that composite to get one-way behaviour.
        static void BuildOneWayColliders(Transform root)
        {
            var runs = new List<Vector2Int>();

            for (int y = 0; y < Rows; y++)
            {
                int start = -1;

                for (int x = 0; x <= Cols; x++)
                {
                    bool solid = x < Cols && CharAt(x, y) == 'X';

                    if (solid && start < 0)
                    {
                        start = x;
                    }
                    else if (!solid && start >= 0)
                    {
                        runs.Add(new Vector2Int(start, y));
                        start = -1;
                    }
                }
            }

            if (runs.Count == 0)
            {
                return;
            }

            var go = new GameObject("OneWayColliders");
            go.transform.SetParent(root, false);
            go.layer = LayerMask.NameToLayer(GameLayers.Ground);
            go.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;

            foreach (Vector2Int run in runs)
            {
                int length = 0;
                while (run.x + length < Cols && CharAt(run.x + length, run.y) == 'X')
                {
                    length++;
                }

                var box = go.AddComponent<BoxCollider2D>();
                box.size = new Vector2(length, 1f);
                box.offset = new Vector2(run.x + length * 0.5f, run.y + 0.5f);
                box.isTrigger = true;
                box.compositeOperation = Collider2D.CompositeOperation.Merge;
            }

            var composite = go.AddComponent<CompositeCollider2D>();
            composite.geometryType = CompositeCollider2D.GeometryType.Outlines;
            composite.generationType = CompositeCollider2D.GenerationType.Synchronous;

            var effector = go.AddComponent<PlatformEffector2D>();
            effector.surfaceArc = 150f;
            effector.useOneWay = true;
            effector.useOneWayGrouping = true;
            effector.useSideFriction = false;
            effector.useSideBounce = false;
        }

        static bool IsGreenZone(int x)
        {
            return (x >= 62 && x <= 76) || (x >= 122 && x <= 140) || (x >= 150);
        }

        static void BuildSpikeTriggers(Transform root)
        {
            for (int x = 0; x < Cols; x++)
            {
                for (int y = 0; y < Rows; y++)
                {
                    if (CharAt(x, y) != '^')
                    {
                        continue;
                    }

                    var go = new GameObject($"Spikes_{x}_{y}");
                    go.transform.SetParent(root, false);
                    go.transform.position = new Vector3(x + 0.5f, y + 0.5f, 0f);
                    go.layer = LayerMask.NameToLayer(GameLayers.Ground);

                    var box = go.AddComponent<BoxCollider2D>();
                    box.size = new Vector2(1f, 0.5f);
                    box.offset = new Vector2(0f, -0.25f);
                    box.isTrigger = true;

                    go.AddComponent<HazardArea>();
                }
            }
        }

        // ------------------------------------------------------------------ parallax

        static void BuildParallax(Transform root)
        {
            var parallaxRoot = new GameObject("Parallax").transform;
            parallaxRoot.SetParent(root, false);

            AddParallax(parallaxRoot, "Sky", "Assets/Sprites/parallax/background.png", "background_0",
                new Vector2(0f, 6f), new Vector3(0f, 6f, 0f), 0.06f, 0.03f, -100, new Color(0.11f, 0.16f, 0.34f, 1f), 0.62f);

            AddParallax(parallaxRoot, "Sun", "Assets/Sprites/parallax/sun.png", "sun_0",
                new Vector2(0f, 0.1f), new Vector3(46f, 11f, 0f), 0.12f, 0.05f, -95, new Color(0.95f, 0.55f, 0.75f, 0.85f), 1.1f);

            AddParallax(parallaxRoot, "CityFar", "Assets/Sprites/parallax/city4plan.png", "city4plan_0",
                new Vector2(0f, 2.6f), new Vector3(0f, 2.6f, 0f), 0.18f, 0.04f, -90, new Color(0.30f, 0.36f, 0.62f, 1f), 0.7f);

            AddParallax(parallaxRoot, "CityMid", "Assets/Sprites/parallax/city3plan.png", "city3plan_0",
                new Vector2(0f, 2.2f), new Vector3(0f, 2.2f, 0f), 0.28f, 0.05f, -88, new Color(0.36f, 0.42f, 0.70f, 1f), 0.78f);

            AddParallax(parallaxRoot, "CityNear", "Assets/Sprites/parallax/city2plan.png", "city2plan_0",
                new Vector2(0f, 1.8f), new Vector3(0f, 1.8f, 0f), 0.42f, 0.06f, -86, new Color(0.42f, 0.48f, 0.78f, 1f), 0.86f);

            AddParallax(parallaxRoot, "SmogBack", "Assets/Sprites/parallax/smog2.png", "smog2_0",
                new Vector2(0f, 0.9f), new Vector3(0f, 0.9f, 0f), 0.55f, 0.08f, -84, new Color(0.45f, 0.72f, 0.66f, 0.55f), 0.8f);

            AddParallax(parallaxRoot, "City1", "Assets/Sprites/parallax/city1plan.png", "city1plan_0",
                new Vector2(0f, 1.4f), new Vector3(0f, 1.4f, 0f), 0.7f, 0.1f, -82, new Color(0.30f, 0.42f, 0.58f, 1f), 0.95f);

            AddParallax(parallaxRoot, "SmogFront", "Assets/Sprites/parallax/smog1.png", "smog1_0",
                new Vector2(0f, 0.4f), new Vector3(0f, 0.4f, 0f), 0.85f, 0.14f, 30, new Color(0.5f, 0.95f, 0.8f, 0.4f), 0.75f);
        }

        static void AddParallax(Transform parent, string name, string path, string spriteName,
            Vector2 originOffset, Vector3 position, float hFactor, float vFactor, int order,
            Color color, float scale)
        {
            Sprite sprite = GasArt.LoadSubSprite(path, spriteName);
            if (sprite == null)
            {
                Debug.LogWarning($"[GasHunter] parallax sprite missing: {spriteName}");
                return;
            }

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.localScale = new Vector3(scale, scale, 1f);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            renderer.color = color;

            var parallax = go.AddComponent<ParallaxLayer>();
            var so = new SerializedObject(parallax);
            so.FindProperty("mode").enumValueIndex = 0;
            so.FindProperty("horizontalFactor").floatValue = hFactor;
            so.FindProperty("verticalFactor").floatValue = vFactor;
            so.FindProperty("repeatHorizontally").boolValue = true;
            so.FindProperty("tilePadding").floatValue = 0.5f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------ prefabs

        static GameObject BuildPlayer(Transform root, Transform spawnPoint, GasProjectile projectilePrefab)
        {
            var go = new GameObject("Player");
            go.transform.SetParent(root, false);
            go.transform.position = new Vector3(2.5f, 5f, 0f);
            go.tag = GameTags.Player;
            go.layer = LayerMask.NameToLayer(GameLayers.Player);

            Sprite[] frames = GasArt.PlayerFrames;
            Sprite frame = frames.Length > 0 ? frames[0] : null;

            var visual = new GameObject("Visual");
            visual.transform.SetParent(go.transform, false);
            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = frame;
            renderer.sortingOrder = 20;
            renderer.color = Color.white;

            float height = frame != null ? frame.bounds.size.y : 1.3f;
            float width = frame != null ? frame.bounds.size.x : 0.8f;

            var body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 3.2f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.sharedMaterial = GetPlayerPhysicsMaterial();

            var box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(width * 0.62f, height * 0.92f);
            box.offset = new Vector2(0f, 0f);

            var firePoint = new GameObject("FirePoint");
            firePoint.transform.SetParent(go.transform, false);
            firePoint.transform.localPosition = new Vector3(width * 0.55f, 0.12f, 0f);

            var flash = CreateMuzzleFlash(firePoint.transform, color: new Color(0.6f, 1f, 0.85f, 1f));

            var tank = go.AddComponent<GasTank>();
            var health = go.AddComponent<PlayerHealth>();
            var controller = go.AddComponent<PlayerController>();

            var so = new SerializedObject(controller);
            so.FindProperty("projectilePrefab").objectReferenceValue = projectilePrefab;
            so.FindProperty("firePoint").objectReferenceValue = firePoint.transform;
            so.FindProperty("groundMask").intValue = 1 << LayerMask.NameToLayer(GameLayers.Ground);
            so.FindProperty("spriteRenderer").objectReferenceValue = renderer;
            so.FindProperty("shootFlash").objectReferenceValue = flash;
            so.ApplyModifiedPropertiesWithoutUndo();

            var healthSo = new SerializedObject(health);
            healthSo.FindProperty("spawnPoint").objectReferenceValue = spawnPoint;
            healthSo.FindProperty("spriteRenderer").objectReferenceValue = renderer;
            healthSo.ApplyModifiedPropertiesWithoutUndo();

            var tankSo = new SerializedObject(tank);
            tankSo.ApplyModifiedPropertiesWithoutUndo();

            return go;
        }

        static GameObject CreateMuzzleFlash(Transform parent, Color color)
        {
            var go = new GameObject("MuzzleFlash");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0.2f, 0f, 0f);
            go.transform.localScale = Vector3.one * 0.6f;

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = ps.main;
            main.duration = 0.25f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = 0.22f;
            main.startSpeed = 2.4f;
            main.startSize = 0.18f;
            main.startColor = color;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.None;
            main.maxParticles = 40;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.enabled = false;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 22f;
            shape.radius = 0.02f;
            shape.rotation = new Vector3(0f, 0f, 0f);

            ParticleSystem.ColorOverLifetimeModule colorOverLife = ps.colorOverLifetime;
            colorOverLife.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
            colorOverLife.color = new ParticleSystem.MinMaxGradient(gradient);

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            if (renderer == null)
            {
                renderer = go.AddComponent<ParticleSystemRenderer>();
            }

            renderer.material = GasArt.GetOrCreateTrailMaterial();
            renderer.sortingOrder = 22;
            renderer.alignment = ParticleSystemRenderSpace.View;

            return go;
        }

        static PhysicsMaterial2D GetPlayerPhysicsMaterial()
        {
            const string path = "Assets/Art/Materials/player.physicsMaterial2D.asset";
            var mat = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(path);
            if (mat == null)
            {
                mat = new PhysicsMaterial2D("player");
                mat.friction = 0f;
                mat.bounciness = 0f;
                GasArt.EnsureFolders();
                AssetDatabase.CreateAsset(mat, path);
                AssetDatabase.SaveAssets();
            }

            return mat;
        }

        static GasProjectile BuildProjectilePrefab()
        {
            Sprite blob = GasArt.GetOrCreateBlobSprite();

            var go = new GameObject("GasProjectile");
            go.layer = LayerMask.NameToLayer(GameLayers.Projectile);
            go.tag = GameTags.Projectile;

            var body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.bodyType = RigidbodyType2D.Dynamic;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;

            var circle = go.AddComponent<CircleCollider2D>();
            circle.radius = 0.2f;
            circle.isTrigger = true;

            var visual = new GameObject("Visual");
            visual.transform.SetParent(go.transform, false);
            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = blob;
            renderer.sortingOrder = 24;
            renderer.color = new Color(0.45f, 1f, 0.8f, 1f);

            var trailGo = new GameObject("Trail");
            trailGo.transform.SetParent(go.transform, false);
            var trail = trailGo.AddComponent<TrailRenderer>();
            trail.time = 0.22f;
            trail.startWidth = 0.32f;
            trail.endWidth = 0.02f;
            trail.startColor = new Color(0.5f, 1f, 0.85f, 0.9f);
            trail.endColor = new Color(0.3f, 0.9f, 0.7f, 0f);
            trail.minVertexDistance = 0.05f;
            trail.numCapVertices = 4;
            trail.sortingOrder = 23;
            trail.material = GasArt.GetOrCreateTrailMaterial();
            trail.emitting = false;

            var projectile = go.AddComponent<GasProjectile>();
            var so = new SerializedObject(projectile);
            so.FindProperty("trail").objectReferenceValue = trail;
            so.FindProperty("spriteRenderer").objectReferenceValue = renderer;
            so.ApplyModifiedPropertiesWithoutUndo();

            go.SetActive(false);
            string path = $"{GasArt.PrefabFolder}/GasProjectile.prefab";
            var prefabRoot = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefabRoot.GetComponent<GasProjectile>();
        }

        static GasOrb BuildOrbPrefab()
        {
            Sprite blob = GasArt.GetOrCreateBlobSprite();

            var go = new GameObject("GasOrb");
            go.layer = LayerMask.NameToLayer(GameLayers.Orb);
            go.tag = GameTags.Orb;

            var body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.bodyType = RigidbodyType2D.Kinematic;

            var circle = go.AddComponent<CircleCollider2D>();
            circle.radius = 0.42f;
            circle.isTrigger = true;

            var glow = new GameObject("Glow");
            glow.transform.SetParent(go.transform, false);
            glow.transform.localScale = Vector3.one * 1.9f;
            var glowRenderer = glow.AddComponent<SpriteRenderer>();
            glowRenderer.sprite = blob;
            glowRenderer.sortingOrder = 25;
            glowRenderer.color = new Color(0.4f, 1f, 0.8f, 0.28f);

            var visual = new GameObject("Visual");
            visual.transform.SetParent(go.transform, false);
            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = blob;
            renderer.sortingOrder = 26;
            renderer.color = new Color(0.6f, 1f, 0.85f, 1f);

            var orb = go.AddComponent<GasOrb>();
            var so = new SerializedObject(orb);
            so.FindProperty("spriteRenderer").objectReferenceValue = renderer;
            so.ApplyModifiedPropertiesWithoutUndo();

            go.SetActive(false);
            string path = $"{GasArt.PrefabFolder}/GasOrb.prefab";
            var prefabRoot = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefabRoot.GetComponent<GasOrb>();
        }

        static EnemyHealth BuildEnemyPrefab(GasOrb orbPrefab)
        {
            Sprite[] frames = GasArt.EnemyFrames;
            Sprite frame = frames.Length > 0 ? frames[0] : null;

            var go = new GameObject("Jellyfish");
            go.tag = GameTags.Enemy;
            go.layer = LayerMask.NameToLayer(GameLayers.Enemy);

            var visual = new GameObject("Visual");
            visual.transform.SetParent(go.transform, false);
            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = frame;
            renderer.sortingOrder = 18;
            renderer.color = new Color(0.75f, 0.85f, 1f, 1f);

            float size = frame != null ? frame.bounds.size.y : 1.3f;

            var body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(size * 0.68f, size * 0.68f);
            box.offset = new Vector2(0f, -size * 0.12f);

            var aura = CreateAura(visual.transform, new Color(0.45f, 0.95f, 0.85f, 0.35f), size * 1.4f);

            var health = go.AddComponent<EnemyHealth>();
            var patrol = go.AddComponent<EnemyPatrol>();
            go.AddComponent<EnemyContactDamage>();

            var healthSo = new SerializedObject(health);
            healthSo.FindProperty("orbPrefab").objectReferenceValue = orbPrefab;
            healthSo.ApplyModifiedPropertiesWithoutUndo();

            var patrolSo = new SerializedObject(patrol);
            patrolSo.FindProperty("groundMask").intValue = 1 << LayerMask.NameToLayer(GameLayers.Ground);
            patrolSo.FindProperty("spriteRenderer").objectReferenceValue = renderer;
            patrolSo.ApplyModifiedPropertiesWithoutUndo();

            string path = $"{GasArt.PrefabFolder}/Jellyfish.prefab";
            var prefabRoot = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefabRoot.GetComponent<EnemyHealth>();
        }

        static GameObject CreateAura(Transform parent, Color color, float size)
        {
            var go = new GameObject("Aura");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, -size * 0.12f, 0f);
            go.transform.localScale = Vector3.one * size;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = ps.main;
            main.duration = 1.4f;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = 0.7f;
            main.startSpeed = 0.35f;
            main.startSize = 0.5f;
            main.startColor = color;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 14f;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.35f;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            if (renderer == null)
            {
                renderer = go.AddComponent<ParticleSystemRenderer>();
            }

            renderer.material = GasArt.GetOrCreateTrailMaterial();
            renderer.sortingOrder = 17;
            renderer.alignment = ParticleSystemRenderSpace.View;

            return go;
        }

        // ------------------------------------------------------------------ entities

        static void BuildEntities(Transform root, EnemyHealth enemyPrefab, List<GameObject> enemies)
        {
            var orbs = new GameObject("Pickups").transform;
            orbs.SetParent(root, false);

            for (int y = 0; y < Rows; y++)
            {
                for (int x = 0; x < Cols; x++)
                {
                    char c = CharAt(x, y);
                    var cellPos = new Vector3(x + 0.5f, y + 0.85f, 0f);

                    if (c == 'E')
                    {
                        var instance = (GameObject)PrefabUtility.InstantiatePrefab(enemyPrefab.gameObject);
                        instance.transform.SetParent(root, false);
                        instance.transform.position = cellPos;
                        enemies.Add(instance);

                        var patrol = instance.GetComponent<EnemyPatrol>();
                        if (patrol != null)
                        {
                            patrol.InitializeDirection(UnityEngine.Random.value < 0.5f ? -1 : 1);
                        }
                    }
                    else if (c == 'G')
                    {
                        var orbPrefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>($"{GasArt.PrefabFolder}/GasOrb.prefab");
                        var orbInstance = (GameObject)PrefabUtility.InstantiatePrefab(orbPrefabAsset);
                        orbInstance.transform.SetParent(orbs, false);
                        orbInstance.transform.position = cellPos;
                        orbInstance.SetActive(true);
                        orbInstance.layer = LayerMask.NameToLayer(GameLayers.Pickup);
                        orbInstance.tag = GameTags.Orb;

                        var orb = orbInstance.GetComponent<GasOrb>();
                        if (orb != null)
                        {
                            orb.Launch(null, Vector2.zero, 22f);
                        }
                    }
                }
            }
        }

        // ------------------------------------------------------------------ camera / lights

        static Camera BuildCamera(Transform root)
        {
            var existing = GameObject.Find("Main Camera");
            if (existing != null)
            {
                Object.DestroyImmediate(existing);
            }

            var go = new GameObject("Main Camera");
            go.transform.SetParent(root, false);
            go.transform.position = new Vector3(4f, 6f, -10f);
            go.tag = "MainCamera";

            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5.4f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.07f, 0.16f, 1f);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;

            go.AddComponent<AudioListener>();
            AddUrpCameraData(go);

            var follow = go.AddComponent<CameraFollow>();
            var so = new SerializedObject(follow);
            so.FindProperty("worldMin").vector2Value = new Vector2(-6f, KillPlaneY - 1f);
            so.FindProperty("worldMax").vector2Value = new Vector2(Cols + 2f, 24f);
            so.ApplyModifiedPropertiesWithoutUndo();

            return cam;
        }

        static void AddUrpCameraData(GameObject go)
        {
            var type = System.Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
            if (type == null)
            {
                return;
            }

            var component = go.AddComponent(type);

            // Overlay cameras do not clear and need a base camera to render into, which
            // leaves the game view empty. The main camera must be a Base camera.
            var prop = type.GetProperty("renderType");
            if (prop != null && prop.CanWrite)
            {
                prop.SetValue(component, Enum.ToObject(prop.PropertyType, 0));
            }

            var baseTypeProp = type.GetProperty("cameraStack");
            if (baseTypeProp != null)
            {
                var stack = baseTypeProp.GetValue(component, null) as System.Collections.IList;
                if (stack != null)
                {
                    stack.Clear();
                }
            }
        }

        static void BuildLights(Transform root)
        {
            var lightsRoot = new GameObject("Lights").transform;
            lightsRoot.SetParent(root, false);

            var global = new GameObject("Global Light 2D");
            global.transform.SetParent(lightsRoot, false);
            global.transform.position = new Vector3(Cols * 0.5f, 8f, 0f);
            var light = global.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.color = new Color(0.86f, 0.9f, 1f, 1f);
            light.intensity = 1f;

            for (int i = 0; i < 10; i++)
            {
                float x = 8f + i * (Cols / 10f);
                var go = new GameObject($"Neon {i + 1}");
                go.transform.SetParent(lightsRoot, false);
                go.transform.position = new Vector3(x, 3f, 0f);
                var point = go.AddComponent<Light2D>();
                point.lightType = Light2D.LightType.Point;
                point.color = new Color(0.35f, 1f, 0.8f, 1f);
                point.intensity = 0.9f;
                point.pointLightOuterRadius = 9f;
            }
        }

        // ------------------------------------------------------------------ hud

        static GameHud BuildHud()
        {
            Font font = GetUiFont();

            var canvasGo = new GameObject("HUD");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();

            // gas bar
            var barBg = CreateImage(canvasGo.transform, "GasBarBg", new Color(0.05f, 0.09f, 0.14f, 0.85f));
            SetAnchors(barBg.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(36f, -36f), new Vector2(430f, 44f), new Vector2(0f, 1f));

            var barFillArea = CreateImage(barBg.transform, "FillArea", Color.white);
            StretchFull(barFillArea.rectTransform, new Vector2(4f, 4f), new Vector2(-4f, -4f));

            var sliderGo = new GameObject("GasBar", typeof(RectTransform));
            sliderGo.transform.SetParent(barFillArea.transform, false);
            StretchFull(sliderGo.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);

            var slider = sliderGo.AddComponent<Slider>();
            slider.transition = Selectable.Transition.None;
            slider.interactable = false;
            slider.minValue = 0f;
            slider.maxValue = 1f;

            var fill = CreateImage(sliderGo.transform, "Fill", new Color(0.4f, 0.95f, 0.7f, 1f));
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = new Vector2(1f, 1f);
            fill.rectTransform.offsetMin = Vector2.zero;
            fill.rectTransform.offsetMax = Vector2.zero;
            slider.fillRect = fill.rectTransform;
            slider.targetGraphic = fill;

            CreateText(barBg.transform, "GasLabel", "GAS", font, 22, TextAnchor.UpperLeft,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -30f), new Vector2(300f, 30f));

            // lives
            var livesGo = CreateText(canvasGo.transform, "Lives", "VIDAS\n3", font, 30, TextAnchor.UpperRight,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-36f, -32f), new Vector2(240f, 90f));

            // enemies
            var enemiesGo = CreateText(canvasGo.transform, "Enemies", "GASES\n0/0", font, 30, TextAnchor.UpperRight,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-36f, -122f), new Vector2(240f, 90f));

            // hint
            CreateText(canvasGo.transform, "Hint", "A / D  mover      ESPACIO  saltar      X  disparar", font, 20, TextAnchor.LowerCenter,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(900f, 40f));

            // message
            var messageGo = CreateText(canvasGo.transform, "Message", string.Empty, font, 54, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(1200f, 200f));
            var group = messageGo.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            messageGo.transform.SetParent(canvasGo.transform, true);
            messageGo.transform.localPosition = new Vector3(0f, 120f, 0f);

            var hud = canvasGo.AddComponent<GameHud>();
            var so = new SerializedObject(hud);
            so.FindProperty("gasBar").objectReferenceValue = slider;
            so.FindProperty("gasFill").objectReferenceValue = fill;
            so.FindProperty("livesText").objectReferenceValue = livesGo;
            so.FindProperty("enemiesText").objectReferenceValue = enemiesGo;
            so.FindProperty("messageText").objectReferenceValue = messageGo;
            so.FindProperty("messageGroup").objectReferenceValue = group;
            so.ApplyModifiedPropertiesWithoutUndo();

            return hud;
        }

        static Font GetUiFont()
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
            {
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            return font;
        }

        static Image CreateImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = color;
            return image;
        }

        static Text CreateText(Transform parent, string name, string content, Font font, int size,
            TextAnchor anchor, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.text = content;
            text.font = font;
            text.fontSize = size;
            text.alignment = anchor;
            text.color = new Color(0.85f, 1f, 0.95f, 1f);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;

            RectTransform rt = text.rectTransform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(anchorMin.x, anchorMax.y);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;
            return text;
        }

        static void SetAnchors(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 size, Vector2 pivot)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
        }

        static void StretchFull(RectTransform rt, Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }

        // ------------------------------------------------------------------ manager

        static GameManager BuildManager(GameHud hud)
        {
            var go = new GameObject("GameManager");
            go.transform.position = Vector3.zero;
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;

            var manager = go.AddComponent<GameManager>();
            var so = new SerializedObject(manager);
            so.FindProperty("killPlaneY").floatValue = KillPlaneY;
            so.FindProperty("sfxSource").objectReferenceValue = source;
            so.FindProperty("hud").objectReferenceValue = hud;
            so.ApplyModifiedPropertiesWithoutUndo();
            return manager;
        }

        static void AddSceneToBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            const string path = "Assets/Scenes/SampleScene.unity";

            for (int i = 0; i < scenes.Count; i++)
            {
                if (scenes[i].path == path)
                {
                    scenes[i] = new EditorBuildSettingsScene(path, true);
                    EditorBuildSettings.scenes = scenes.ToArray();
                    return;
                }
            }

            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}

using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using GasHunter;

namespace GasHunter.EditorTools
{
    [InitializeOnLoad]
    public static class SpikeTool
    {
        const int TileSpikes = 5;
        const int SpikeTileIndex = 29;
        const string SpikeTilePath = "Assets/Art/Tiles/tileset_futurista_verde_azul_32px_29.asset";

        static bool enabled;

        static SpikeTool()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }

        [MenuItem("GasHunter/Spike Tools/Paint Spikes (click)")]
        public static void Toggle()
        {
            enabled = !enabled;
            SceneView.RepaintAll();
        }

        [MenuItem("GasHunter/Spike Tools/Paint Spikes (click)", true)]
        static bool ToggleValidate()
        {
            Menu.SetChecked("GasHunter/Spike Tools/Paint Spikes (click)", enabled);
            return true;
        }

        static void OnSceneGUI(SceneView view)
        {
            if (!enabled)
            {
                return;
            }

            Event evt = Event.current;

            if (evt != null)
            {
                Ray worldRay = HandleUtility.GUIPointToWorldRay(evt.mousePosition);
                Vector3 hit = worldRay.origin;
                hit.z = 0f;
                int x = Mathf.FloorToInt(hit.x);
                int y = Mathf.FloorToInt(hit.y);

                Handles.color = Color.yellow;
                Handles.DrawWireCube(new Vector3(x + 0.5f, y + 0.5f, 0f), Vector3.one);

                Handles.BeginGUI();
                GUILayout.BeginArea(new Rect(16f, 16f, 340f, 96f));
                GUILayout.BeginVertical("box");
                GUILayout.Label("Pinchos activos - celda " + x + ", " + y);
                GUILayout.Label("Clic izq: anadir   Shift+Clic izq: quitar");
                GUILayout.EndVertical();
                GUILayout.EndArea();
                Handles.EndGUI();
            }

            if (evt != null && evt.type == EventType.MouseDown && evt.button == 0 && !evt.alt && !evt.control)
            {
                Ray worldRay = HandleUtility.GUIPointToWorldRay(evt.mousePosition);
                Vector3 hit = worldRay.origin;
                hit.z = 0f;
                int x = Mathf.FloorToInt(hit.x);
                int y = Mathf.FloorToInt(hit.y);

                if (evt.shift)
                {
                    RemoveSpike(x, y);
                }
                else
                {
                    AddSpike(x, y);
                }

                evt.Use();
            }
        }

        static Tilemap FindSolids()
        {
            return Object.FindObjectsOfType<Tilemap>().FirstOrDefault(m => m.name == "Solids");
        }

static Tile GetSpikeTile()
        {
            Tile existing = AssetDatabase.LoadAssetAtPath<Tile>(SpikeTilePath);
            if (existing != null)
            {
                return existing;
            }

            GasArt.EnsureFolders();
            string spriteName = $"{Path.GetFileNameWithoutExtension(GasArt.TilesetPath)}_{SpikeTileIndex}";
            Sprite sprite = GasArt.LoadSubSprite(GasArt.TilesetPath, spriteName);
            if (sprite == null)
            {
                return null;
            }

            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.name = $"Tile_{SpikeTileIndex}";
            tile.sprite = sprite;
            tile.colliderType = Tile.ColliderType.None;
            tile.color = Color.white;
            AssetDatabase.CreateAsset(tile, $"{GasArt.TileFolder}/Decor/Tile_{SpikeTileIndex}.asset");
            return tile;
        }

        static void AddSpike(int x, int y)
        {
            Tilemap solids = FindSolids();
            if (solids == null)
            {
                Debug.LogWarning("[GasHunter] No hay tilemap 'Solids' en la escena.");
                return;
            }

            Tile tile = GetSpikeTile();
            if (tile == null)
            {
                Debug.LogWarning("[GasHunter] No se encontro el sprite del tile de pinchos.");
                return;
            }

            var cell = new Vector3Int(x, y, 0);
            if (solids.GetTile(cell) == null)
            {
                Undo.RegisterCompleteObjectUndo(solids, "Add Spike");
                solids.SetTile(cell, tile);
            }

            if (HasSpikeHazard(x, y))
            {
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                return;
            }

            Transform root = null;
            GameObject level = GameObject.Find("Level");
            if (level != null)
            {
                root = level.transform;
            }

            var go = new GameObject($"Spikes_{x}_{y}");
            if (root != null)
            {
                go.transform.SetParent(root, false);
            }

            go.transform.position = new Vector3(x + 0.5f, y + 0.5f, 0f);
            go.layer = LayerMask.NameToLayer(GameLayers.Ground);

            var box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(1f, 0.5f);
            box.offset = new Vector2(0f, -0.25f);
            box.isTrigger = true;

            go.AddComponent<HazardArea>();
            Undo.RegisterCreatedObjectUndo(go, "Add Spike");
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        static void RemoveSpike(int x, int y)
        {
            Tilemap solids = FindSolids();
            var cell = new Vector3Int(x, y, 0);

            if (solids != null && solids.GetTile(cell) != null)
            {
                Undo.RegisterCompleteObjectUndo(solids, "Remove Spike");
                solids.SetTile(cell, null);
            }

            string name = $"Spikes_{x}_{y}";
            foreach (var go in Object.FindObjectsOfType<GameObject>(true))
            {
                if (go.name == name)
                {
                    Undo.DestroyObjectImmediate(go);
                    break;
                }
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        static bool HasSpikeHazard(int x, int y)
        {
            string name = $"Spikes_{x}_{y}";
            foreach (var go in Object.FindObjectsOfType<GameObject>(true))
            {
                if (go.name == name)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GasHunter.EditorTools
{
    public static class GasArt
    {
        public const string ArtFolder = "Assets/Art";
        public const string TileFolder = "Assets/Art/Tiles";
        public const string PrefabFolder = "Assets/Prefabs";
        public const string MaterialFolder = "Assets/Art/Materials";

        public const string TilesetPath = "Assets/Sprites/tileset_futurista_verde_azul_32px.png";
        public const string PlayerPath = "Assets/Sprites/player.png";
        public const string EnemyPath = "Assets/Sprites/Enemy.png";

        static Sprite[] _playerFrames;
        static Sprite[] _enemyFrames;

        public static Sprite[] PlayerFrames
        {
            get
            {
                if (_playerFrames == null)
                {
                    _playerFrames = LoadSorted(PlayerPath);
                }

                return _playerFrames;
            }
        }

        public static Sprite[] EnemyFrames
        {
            get
            {
                if (_enemyFrames == null)
                {
                    _enemyFrames = LoadSorted(EnemyPath);
                }

                return _enemyFrames;
            }
        }

        static Sprite[] LoadSorted(string path)
        {
            var all = AssetDatabase.LoadAllAssetsAtPath(path);
            var list = new List<Sprite>();

            foreach (var obj in all)
            {
                if (obj is Sprite s)
                {
                    list.Add(s);
                }
            }

            list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return list.ToArray();
        }

        public static Sprite LoadSubSprite(string path, string spriteName)
        {
            var all = AssetDatabase.LoadAllAssetsAtPath(path);
            foreach (var obj in all)
            {
                if (obj is Sprite s && s.name == spriteName)
                {
                    return s;
                }
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        public static void EnsureFolders()
        {
            EnsureFolder("Assets", "Art");
            EnsureFolder(ArtFolder, "Tiles");
            EnsureFolder(ArtFolder, "Materials");
            EnsureFolder("Assets", "Prefabs");
        }

        public static void EnsureFolder(string parent, string child)
        {
            string full = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(full))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }

        public static Sprite GetOrCreateBlobSprite(int size = 64, float softness = 0.55f)
        {
            EnsureFolders();
            string path = $"{ArtFolder}/gas_blob.png";

            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing == null)
            {
                existing = BuildBlobTexture(size, softness);
                var bytes = existing.EncodeToPNG();
                var stream = new MemoryStream(bytes);
                using (var file = new FileStream(path, FileMode.Create, FileAccess.Write))
                {
                    stream.CopyTo(file);
                }

                Object.DestroyImmediate(existing);

                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = size;
                importer.filterMode = FilterMode.Bilinear;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static Texture2D BuildBlobTexture(int size, float softness)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            float half = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - half) / half;
                    float dy = (y + 0.5f - half) / half;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);

                    float alpha = 1f - Mathf.SmoothStep(1f - softness, 1f, d);
                    alpha = Mathf.Clamp01(alpha);

                    float core = Mathf.Clamp01(1f - d * 1.15f);
                    Color c = Color.Lerp(new Color(0.35f, 1f, 0.75f, 1f), new Color(0.85f, 1f, 0.95f, 1f), core);

                    float r = (byte)(255 * c.r);
                    float g = (byte)(255 * c.g);
                    float b = (byte)(255 * c.b);
                    float a = (byte)(255 * c.a * alpha);

                    pixels[y * size + x] = new Color32((byte)r, (byte)g, (byte)b, (byte)a);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        public static Material GetOrCreateTrailMaterial()
        {
            EnsureFolders();
            string path = $"{MaterialFolder}/gas_trail.mat";

            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null)
            {
                return mat;
            }

            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            mat = new Material(shader);
            mat.name = "gas_trail";

            if (mat.HasProperty("_BaseColor"))
            {
                mat.SetColor("_BaseColor", new Color(0.4f, 1f, 0.8f, 0.85f));
            }

            if (mat.HasProperty("_Color"))
            {
                mat.SetColor("_Color", new Color(0.4f, 1f, 0.8f, 0.85f));
            }

            AssetDatabase.CreateAsset(mat, path);
            AssetDatabase.SaveAssets();
            return mat;
        }

        public static Material GetOrCreateSpriteMaterial(Color color, string name, bool additive)
        {
            EnsureFolders();
            string path = $"{MaterialFolder}/{name}.mat";

            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null)
            {
                return mat;
            }

            var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            mat = new Material(shader);
            mat.name = name;

            if (mat.HasProperty("_BaseColor"))
            {
                mat.SetColor("_BaseColor", color);
            }

            if (mat.HasProperty("_Color"))
            {
                mat.SetColor("_Color", color);
            }

            AssetDatabase.CreateAsset(mat, path);
            AssetDatabase.SaveAssets();
            return mat;
        }
    }
}
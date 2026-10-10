using UnityEditor;
using UnityEngine;

namespace GasHunter.EditorTools
{
    public static class ProjectSetup
    {
        [MenuItem("GasHunter/Setup Project Tags And Layers")]
        public static void SetupTagsAndLayers()
        {
            var asset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (asset == null || asset.Length == 0)
            {
                Debug.LogError("[GasHunter] TagManager not found.");
                return;
            }

            var tagManager = new SerializedObject(asset[0]);

            AddTags(tagManager, "tags",
                new[] { "Player", "Enemy", "GasProjectile", "GasOrb" });

            AddLayers(tagManager, "layers",
                new[] { "Ground", "Player", "Enemy", "Projectile", "GasOrb", "Pickup" });

            tagManager.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            Debug.Log("[GasHunter] Tags and layers configured.");
        }

        static void AddTags(SerializedObject so, string propertyName, string[] names)
        {
            SerializedProperty tags = so.FindProperty(propertyName);
            if (tags == null)
            {
                return;
            }

            foreach (string name in names)
            {
                bool exists = false;
                for (int i = 0; i < tags.arraySize; i++)
                {
                    if (tags.GetArrayElementAtIndex(i).stringValue == name)
                    {
                        exists = true;
                        break;
                    }
                }

                if (exists)
                {
                    continue;
                }

                tags.InsertArrayElementAtIndex(tags.arraySize);
                tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = name;
            }
        }

        static void AddLayers(SerializedObject so, string propertyName, string[] names)
        {
            SerializedProperty layers = so.FindProperty(propertyName);
            if (layers == null)
            {
                return;
            }

            foreach (string name in names)
            {
                bool exists = false;
                for (int i = 0; i < layers.arraySize; i++)
                {
                    if (layers.GetArrayElementAtIndex(i).stringValue == name)
                    {
                        exists = true;
                        break;
                    }
                }

                if (exists)
                {
                    continue;
                }

                for (int i = 8; i < layers.arraySize; i++)
                {
                    SerializedProperty element = layers.GetArrayElementAtIndex(i);
                    if (string.IsNullOrEmpty(element.stringValue))
                    {
                        element.stringValue = name;
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                {
                    Debug.LogWarning($"[GasHunter] No free layer slot for '{name}'.");
                }
            }
        }
    }
}
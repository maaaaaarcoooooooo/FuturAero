using System.Collections.Generic;
using UnityEngine;

namespace GasHunter
{
    public class ParallaxLayer : MonoBehaviour
    {
        public enum Mode
        {
            HorizontalOnly,
            VerticalOnly,
            Both,
            Fixed
        }

        [SerializeField] Mode mode = Mode.HorizontalOnly;
        [SerializeField] float horizontalFactor = 0.5f;
        [SerializeField] float verticalFactor = 0.5f;
        [SerializeField] bool repeatHorizontally = true;
        [SerializeField] float tilePadding = 1f;

        static Camera cam;
        static float ortho = 10f;
        static float aspect = 1.7778f;

        Vector3 origin;
        Transform target;
        SpriteRenderer source;
        readonly List<SpriteRenderer> tiles = new List<SpriteRenderer>();
        bool built;

        void Awake()
        {
            origin = transform.position;
            source = GetComponent<SpriteRenderer>();
        }

        void OnEnable()
        {
            if (!built)
            {
                Build();
            }
        }

        void Build()
        {
            built = true;

            if (source == null || source.sprite == null || !repeatHorizontally)
            {
                return;
            }

            float width = Mathf.Abs(source.bounds.size.x);
            if (width < 0.001f)
            {
                return;
            }

            cam = Camera.main;
            RefreshCamera();

            float visibleWidth = ortho * aspect * 2f;
            int needed = Mathf.Clamp(Mathf.CeilToInt((visibleWidth + tilePadding * 4f) / width) + 2, 2, 40);

            tiles.Add(source);
            source.transform.localPosition = Vector3.zero;

            for (int i = 1; i < needed; i++)
            {
                GameObject go = new GameObject($"{name}_Tile{i}");
                go.transform.SetParent(transform.parent, true);
                SpriteRenderer r = go.AddComponent<SpriteRenderer>();
                CopySettings(r, source);
                tiles.Add(r);
            }
        }

        void CopySettings(SpriteRenderer targetRenderer, SpriteRenderer from)
        {
            targetRenderer.sprite = from.sprite;
            targetRenderer.sortingLayerID = from.sortingLayerID;
            targetRenderer.sortingOrder = from.sortingOrder;
            targetRenderer.color = from.color;
            targetRenderer.flipX = from.flipX;
            targetRenderer.flipY = from.flipY;
        }

        void LateUpdate()
        {
            if (PlayerController.Instance == null)
            {
                return;
            }

            target = PlayerController.Instance.transform;

            if (cam == null)
            {
                cam = Camera.main;
                if (cam == null)
                {
                    return;
                }
            }

            RefreshCamera();

            Vector3 focus = target.position;
            Vector3 pos = origin;

            if (mode == Mode.HorizontalOnly || mode == Mode.Both)
            {
                pos.x = origin.x + (focus.x - origin.x) * horizontalFactor;
            }

            if (mode == Mode.VerticalOnly || mode == Mode.Both)
            {
                pos.y = origin.y + (focus.y - origin.y) * verticalFactor;
            }

            transform.position = pos;

            if (tiles.Count == 0)
            {
                return;
            }

            float width = Mathf.Abs(source.bounds.size.x);
            if (width < 0.001f)
            {
                return;
            }

            float camX = cam.transform.position.x;
            float halfWidth = ortho * aspect + tilePadding;
            int firstIndex = Mathf.FloorToInt((camX - halfWidth - pos.x) / width);

            for (int i = 0; i < tiles.Count; i++)
            {
                SpriteRenderer r = tiles[i];
                float centerX = pos.x + (firstIndex + i) * width + width * 0.5f;
                bool visible = centerX + width * 0.5f > camX - halfWidth && centerX - width * 0.5f < camX + halfWidth;

                if (r.gameObject.activeSelf != visible)
                {
                    r.gameObject.SetActive(visible);
                }

                if (visible)
                {
                    r.transform.position = new Vector3(centerX, pos.y, pos.z);
                }
            }
        }

        void RefreshCamera()
        {
            if (cam == null)
            {
                return;
            }

            ortho = cam.orthographic ? cam.orthographicSize : 10f;
            aspect = cam.aspect > 0.01f ? cam.aspect : 1.7778f;
        }
    }
}
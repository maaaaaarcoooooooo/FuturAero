using System;
using UnityEngine;

namespace GasHunter
{
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] float smoothTime = 0.14f;
        [SerializeField] Vector2 lookOffset = new Vector2(1.6f, 1.1f);
        [SerializeField] Vector2 lookAheadSmoothing = new Vector2(2.2f, 2.6f);
        [SerializeField] float maxLookAheadX = 3.2f;
        [SerializeField] Vector2 worldMin = new Vector2(-8f, -12f);
        [SerializeField] Vector2 worldMax = new Vector2(220f, 30f);

        Camera cam;
        Transform target;
        Vector3 velocity;
        float lookX;
        float lookY;

        void Start()
        {
            cam = GetComponent<Camera>();
            target = PlayerController.Instance != null ? PlayerController.Instance.transform : null;
            if (target != null)
            {
                ApplyImmediate();
            }
        }

        void LateUpdate()
        {
            if (target == null)
            {
                target = PlayerController.Instance != null ? PlayerController.Instance.transform : null;
                if (target == null)
                {
                    return;
                }

                ApplyImmediate();
                return;
            }

            Vector3 pos = target.position;
            pos.x += lookOffset.x;
            pos.y += lookOffset.y;
            pos.z = transform.position.z;

            lookX = Mathf.Clamp(
                lookX + Mathf.Clamp(pos.x - transform.position.x - lookOffset.x, -maxLookAheadX, maxLookAheadX) * 2f * Time.deltaTime,
                -maxLookAheadX, maxLookAheadX);
            lookY = Mathf.Clamp(lookY + (pos.y - transform.position.y - lookOffset.y) * 2f * Time.deltaTime, -2.5f, 3.5f);

            pos.x += lookX;
            pos.y += lookY;

            if (cam != null && cam.orthographic)
            {
                float halfH = cam.orthographicSize;
                float halfW = halfH * cam.aspect;
                pos.x = Mathf.Clamp(pos.x, worldMin.x + halfW, worldMax.x - halfW);
                pos.y = Mathf.Clamp(pos.y, worldMin.y + halfH, worldMax.y - halfH);
            }

            Vector3 smoothed = Vector3.SmoothDamp(transform.position, pos, ref velocity, smoothTime);
            smoothed.z = transform.position.z;
            transform.position = smoothed;
        }

        void ApplyImmediate()
        {
            Vector3 pos = target.position + new Vector3(lookOffset.x, lookOffset.y, 0f);

            // Keep the camera on its own depth plane. The player sits at z=0, so copying
            // the target's z would push the camera into the sprite plane and every sprite
            // would fall behind the near clip plane.
            pos.z = transform.position.z;

            if (cam != null && cam.orthographic)
            {
                float halfH = cam.orthographicSize;
                float halfW = halfH * cam.aspect;
                pos.x = Mathf.Clamp(pos.x, worldMin.x + halfW, worldMax.x - halfW);
                pos.y = Mathf.Clamp(pos.y, worldMin.y + halfH, worldMax.y - halfH);
            }

            transform.position = pos;
            velocity = Vector3.zero;
            lookX = 0f;
            lookY = 0f;
        }

        public void SetBounds(Vector2 min, Vector2 max)
        {
            worldMin = min;
            worldMax = max;
        }
    }
}
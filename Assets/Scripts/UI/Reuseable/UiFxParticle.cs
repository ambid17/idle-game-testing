using System;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    // One code-driven UI particle spawned by UiFx: drifts (or travels a straight path), scales and
    // fades over its lifetime, then destroys itself. Unscaled time, so it plays while paused.
    [RequireComponent(typeof(Image))]
    public class UiFxParticle : MonoBehaviour
    {
        public Vector2 Velocity;
        public float Drag;
        public float Lifetime = 0.5f;
        public float StartScale = 1f;
        public float EndScale = 1f;
        public float Spin;
        // Share of the lifetime spent popping in from 0 scale.
        public float PopIn;
        // Alpha holds at 1 until this share of the lifetime, then fades to 0.
        public float FadeStart = 0.5f;

        // Set by UiFx.Travel: moves from PathFrom to PathTo (eased) instead of drifting.
        public bool HasPath;
        public Vector2 PathFrom;
        public Vector2 PathTo;
        public Action OnArrive;

        private RectTransform rect;
        private Image image;
        private Color baseColor;
        private float age;

        private void Awake()
        {
            rect = (RectTransform)transform;
            image = GetComponent<Image>();
        }

        private void Start() => baseColor = image.color;

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            age += dt;
            float t = Mathf.Clamp01(age / Lifetime);

            if (HasPath)
            {
                rect.localPosition = Vector2.LerpUnclamped(PathFrom, PathTo, Effects.Easing.InOutSine(t));
            }
            else
            {
                rect.localPosition += (Vector3)(Velocity * dt);
                Velocity *= Mathf.Exp(-Drag * dt);
            }

            float scale = Mathf.Lerp(StartScale, EndScale, t);
            if (PopIn > 0f && t < PopIn) scale *= Effects.Easing.OutBack(t / PopIn);
            rect.localScale = Vector3.one * scale;
            if (Spin != 0f) rect.localRotation = Quaternion.Euler(0f, 0f, Spin * age);

            float alpha = t <= FadeStart ? 1f : 1f - (t - FadeStart) / (1f - FadeStart);
            image.color = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * alpha);

            if (age < Lifetime) return;
            OnArrive?.Invoke();
            Destroy(gameObject);
        }
    }
}

using System;
using System.Collections;
using UnityEngine;
using IronIvy.Data;

namespace IronIvy.Gameplay.Animals
{
    // lo phan VFX va fade in/out model
    public class AnimalVisibilityController : MonoBehaviour
    {
        [Header("Renderers de fade")]
        [Tooltip("Neu de trong se tu tim Renderer con.")]
        public Renderer[] renderers;

        [Tooltip("Thoi gian fade out khi despawn.")]
        public float fadeDuration = 0.6f;

        private AnimalController _controller;
        private float[] _originalAlphas;

        private void Awake()
        {
            _controller = GetComponentInParent<AnimalController>();

            if (renderers == null || renderers.Length == 0)
            {
                renderers = GetComponentsInChildren<Renderer>();
            }

            CacheOriginalAlphas();
        }

        private void CacheOriginalAlphas()
        {
            if (renderers == null) return;

            _originalAlphas = new float[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                var mat = renderers[i].material;
                if (mat != null && mat.HasProperty("_Color"))
                {
                    _originalAlphas[i] = mat.color.a;
                }
                else
                {
                    _originalAlphas[i] = 1f;
                }
            }
        }

        // reset ve alpha ban dau khi lay tu pool ra lai
        public void ResetFadeImmediate()
        {
            if (renderers == null || _originalAlphas == null) return;

            for (int i = 0; i < renderers.Length; i++)
            {
                var mat = renderers[i].material;
                if (mat != null && mat.HasProperty("_Color"))
                {
                    var c = mat.color;
                    c.a = (i < _originalAlphas.Length) ? _originalAlphas[i] : 1f;
                    mat.color = c;
                }
            }
        }

        // spawn VFX nho nho tai con thu
        public void PlaySpawnVFX()
        {
            if (_controller == null) return;

            AnimalDefinition def = _controller.Definition;
            if (def != null && def.spawnVfxPrefab != null)
            {
                Instantiate(def.spawnVfxPrefab, transform.position, Quaternion.identity);
            }
        }

        // fade out + despawn
        public void PlayDespawnVFXAndFadeOut(Action onComplete)
        {
            if (!gameObject.activeInHierarchy)
            {
                onComplete?.Invoke();
                return;
            }

            StartCoroutine(FadeOutRoutine(onComplete));
        }

        private IEnumerator FadeOutRoutine(Action onComplete)
        {
            // play fx bien mat truoc
            if (_controller != null)
            {
                AnimalDefinition def = _controller.Definition;
                if (def != null && def.despawnVfxPrefab != null)
                {
                    Instantiate(def.despawnVfxPrefab, transform.position, Quaternion.identity);
                }
            }

            float t = 0f;
            while (t < fadeDuration)
            {
                t += Time.deltaTime;
                float k = 1f - Mathf.Clamp01(t / fadeDuration);
                ApplyAlpha(k);
                yield return null;
            }

            ApplyAlpha(0f);
            onComplete?.Invoke();
        }

        private void ApplyAlpha(float a)
        {
            if (renderers == null) return;

            for (int i = 0; i < renderers.Length; i++)
            {
                var mat = renderers[i].material;
                if (mat != null && mat.HasProperty("_Color"))
                {
                    Color c = mat.color;
                    c.a = a;
                    mat.color = c;
                }
            }
        }
    }
}

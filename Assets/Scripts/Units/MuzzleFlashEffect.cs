using UnityEngine;
using NapoleonicWars.Core;
using System.Collections.Generic;

namespace NapoleonicWars.Units
{
    /// <summary>
    /// Pooled muzzle flash + smoke effect system.
    /// Limits max active effects and reuses GameObjects to avoid GC pressure.
    /// </summary>
    public class MuzzleFlashEffect : MonoBehaviour
    {
        private static Material flashMat;
        private static Material smokeMat;
        private static Queue<GameObject> flashPool = new Queue<GameObject>(64);
        private static Queue<GameObject> smokePool = new Queue<GameObject>(64);
        private static int activeFlashes;
        private static int activeSmokeCount;
        private const int MaxFlashes = 30;
        private const int MaxSmoke = 20;
        private static int frameCounter;
        private static int effectsThisFrame;
        private const int MaxEffectsPerFrame = 8;

        public static void Create(Vector3 position, Vector3 forward)
        {
            // Throttle: max N effects per frame
            if (Time.frameCount != frameCounter)
            {
                frameCounter = Time.frameCount;
                effectsThisFrame = 0;
            }
            if (effectsThisFrame >= MaxEffectsPerFrame) return;
            effectsThisFrame++;

            // Lazy-init shared materials
            if (flashMat == null)
                flashMat = URPMaterialHelper.CreateLitEmissive(new Color(1f, 0.9f, 0.5f, 1f), new Color(1f, 0.8f, 0.3f) * 4f);
            if (smokeMat == null)
                smokeMat = URPMaterialHelper.CreateLitTransparent(new Color(0.7f, 0.7f, 0.7f, 0.6f));

            // Flash (pooled)
            if (activeFlashes < MaxFlashes)
            {
                GameObject flash = GetFromPool(flashPool, "MuzzleFlash");
                flash.transform.position = position + forward * 0.5f + Vector3.up * 0.8f;
                flash.transform.localScale = Vector3.one * 0.4f;
                flash.GetComponent<Renderer>().sharedMaterial = flashMat;
                flash.SetActive(true);

                FlashFade ff = flash.GetComponent<FlashFade>();
                if (ff == null) ff = flash.AddComponent<FlashFade>();
                ff.Reset();
                activeFlashes++;
            }

            // Smoke (pooled, fewer than flashes)
            if (activeSmokeCount < MaxSmoke)
            {
                GameObject smoke = GetFromPool(smokePool, "SmokePuff");
                smoke.transform.position = position + forward * 0.8f + Vector3.up * 1f;
                smoke.transform.localScale = Vector3.one * 0.1f;
                smoke.GetComponent<Renderer>().sharedMaterial = smokeMat;
                smoke.SetActive(true);

                SmokeFade sf = smoke.GetComponent<SmokeFade>();
                if (sf == null) sf = smoke.AddComponent<SmokeFade>();
                sf.Reset();
                activeSmokeCount++;
            }
        }

        private static GameObject GetFromPool(Queue<GameObject> pool, string name)
        {
            while (pool.Count > 0)
            {
                GameObject go = pool.Dequeue();
                if (go != null) return go;
            }

            // Create new
            GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            obj.name = name;
            Collider col = obj.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            return obj;
        }

        public static void ReturnFlash(GameObject go)
        {
            activeFlashes--;
            go.SetActive(false);
            flashPool.Enqueue(go);
        }

        public static void ReturnSmoke(GameObject go)
        {
            activeSmokeCount--;
            go.SetActive(false);
            smokePool.Enqueue(go);
        }
    }

    public class FlashFade : MonoBehaviour
    {
        private float timer;

        public void Reset()
        {
            timer = 0.1f;
        }

        private void Update()
        {
            timer -= Time.deltaTime;
            float scale = Mathf.Lerp(0f, 0.4f, timer / 0.1f);
            transform.localScale = Vector3.one * Mathf.Max(scale, 0.01f);

            if (timer <= 0f)
                MuzzleFlashEffect.ReturnFlash(gameObject);
        }
    }

    public class SmokeFade : MonoBehaviour
    {
        private float timer;
        private float maxTimer = 1.5f;
        private Vector3 drift;
        private Renderer cachedRenderer;
        private MaterialPropertyBlock mpb;
        private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");

        private void Awake()
        {
            cachedRenderer = GetComponent<Renderer>();
            mpb = new MaterialPropertyBlock();
        }

        public void Reset()
        {
            timer = maxTimer;
            drift = new Vector3(
                Random.Range(-0.3f, 0.3f),
                Random.Range(0.5f, 1.5f),
                Random.Range(-0.3f, 0.3f)
            );
            if (cachedRenderer == null) cachedRenderer = GetComponent<Renderer>();
            if (mpb == null) mpb = new MaterialPropertyBlock();
        }

        private void Update()
        {
            timer -= Time.deltaTime;
            float t = 1f - (timer / maxTimer);

            // Expand and rise
            float scale = Mathf.Lerp(0.1f, 0.4f, t);
            transform.localScale = Vector3.one * scale;
            transform.position += drift * Time.deltaTime;

            // Fade out using MaterialPropertyBlock - no material allocation
            if (cachedRenderer != null)
            {
                float alpha = Mathf.Lerp(0.6f, 0f, t);
                cachedRenderer.GetPropertyBlock(mpb);
                Color c = new Color(0.7f, 0.7f, 0.7f, alpha);
                mpb.SetColor(ColorPropertyId, c);
                mpb.SetColor(BaseColorPropertyId, c);
                cachedRenderer.SetPropertyBlock(mpb);
            }

            if (timer <= 0f)
                MuzzleFlashEffect.ReturnSmoke(gameObject);
        }
    }
}

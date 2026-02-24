using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using NapoleonicWars.Core;

namespace NapoleonicWars.UI
{
    /// <summary>
    /// Scrolling battle log / kill feed in the top-right corner.
    /// Shows regiment events: routs, charges, volleys, officer deaths, etc.
    /// </summary>
    public class BattleLogUI : MonoBehaviour
    {
        public static BattleLogUI Instance { get; private set; }

        private Canvas canvas;
        private RectTransform logPanel;
        private List<LogEntry> entries = new List<LogEntry>();
        private List<GameObject> entryObjects = new List<GameObject>();

        private const int MaxEntries = 8;
        private const float EntryLifetime = 6f;
        private const float FadeTime = 1.5f;
        private float lineHeight = 20f;

        private struct LogEntry
        {
            public string text;
            public Color color;
            public float spawnTime;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            BuildUI();
        }

        private void Update()
        {
            // Fade and remove old entries
            float now = Time.time;
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                float age = now - entries[i].spawnTime;
                if (age > EntryLifetime)
                {
                    RemoveEntry(i);
                    continue;
                }

                // Fade out near end of life
                if (age > EntryLifetime - FadeTime && i < entryObjects.Count)
                {
                    float alpha = 1f - (age - (EntryLifetime - FadeTime)) / FadeTime;
                    Text t = entryObjects[i].GetComponent<Text>();
                    if (t != null)
                    {
                        Color c = t.color;
                        c.a = alpha;
                        t.color = c;
                    }
                }
            }
        }

        // === PUBLIC API ===

        public void LogEvent(string message, Color color)
        {
            if (entries.Count >= MaxEntries)
                RemoveEntry(0);

            LogEntry entry = new LogEntry
            {
                text = message,
                color = color,
                spawnTime = Time.time
            };
            entries.Add(entry);

            // Create UI element
            GameObject go = new GameObject($"Log_{entries.Count}");
            go.transform.SetParent(logPanel, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0, 1);
            rt.sizeDelta = new Vector2(0, lineHeight);

            Text t = go.AddComponent<Text>();
            t.text = message;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (t.font == null) t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            t.fontSize = 12;
            t.color = color;
            t.alignment = TextAnchor.MiddleRight;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;

            // Add shadow for readability
            Shadow shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.8f);
            shadow.effectDistance = new Vector2(1, -1);

            entryObjects.Add(go);
            RepositionEntries();
        }

        public void LogPlayerEvent(string message)
        {
            LogEvent(message, new Color(0.4f, 0.7f, 1f));
        }

        public void LogEnemyEvent(string message)
        {
            LogEvent(message, new Color(1f, 0.4f, 0.35f));
        }

        public void LogNeutralEvent(string message)
        {
            LogEvent(message, new Color(0.85f, 0.8f, 0.6f));
        }

        // === INTERNAL ===

        private void RemoveEntry(int index)
        {
            if (index < entryObjects.Count)
            {
                Destroy(entryObjects[index]);
                entryObjects.RemoveAt(index);
            }
            if (index < entries.Count)
                entries.RemoveAt(index);
            RepositionEntries();
        }

        private void RepositionEntries()
        {
            for (int i = 0; i < entryObjects.Count; i++)
            {
                RectTransform rt = entryObjects[i].GetComponent<RectTransform>();
                if (rt != null)
                {
                    // Stack from bottom up (newest at bottom)
                    float y = -(entryObjects.Count - 1 - i) * lineHeight;
                    rt.anchoredPosition = new Vector2(0, y);
                }
            }
        }

        private void BuildUI()
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 12;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            logPanel = new GameObject("LogPanel").AddComponent<RectTransform>();
            logPanel.SetParent(canvas.transform, false);
            logPanel.anchorMin = new Vector2(0.55f, 0.75f);
            logPanel.anchorMax = new Vector2(0.98f, 0.95f);
            logPanel.offsetMin = Vector2.zero;
            logPanel.offsetMax = Vector2.zero;
        }
    }
}

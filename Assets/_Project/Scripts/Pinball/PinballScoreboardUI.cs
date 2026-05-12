using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ArcadeRoom.Pinball
{
    [RequireComponent(typeof(Canvas))]
    [DisallowMultipleComponent]
    public sealed class PinballScoreboardUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text timeText;
        [SerializeField] private TMP_Text scoreText;
        [SerializeField] private Color timeColor = new(0.88f, 1f, 0.92f, 1f);
        [SerializeField] private Color scoreColor = new(1f, 0.95f, 0.42f, 1f);

        public void EnsureLayout()
        {
            ResolveTextReferences();

            if (timeText == null || scoreText == null)
            {
                BuildDefaultLayout();
                ResolveTextReferences();
            }

            ApplyTextDefaults();
        }

        public void Refresh(float elapsedSeconds, int score)
        {
            EnsureLayout();

            if (timeText != null)
            {
                timeText.text = FormatTime(elapsedSeconds);
            }

            if (scoreText != null)
            {
                scoreText.text = $"{Mathf.Max(0, score):0000}";
            }
        }

        public static string FormatTime(float elapsedSeconds)
        {
            var clampedSeconds = Mathf.Max(0, Mathf.FloorToInt(elapsedSeconds));
            var minutes = clampedSeconds / 60;
            var seconds = clampedSeconds % 60;
            return $"{minutes:00}\"{seconds:00}";
        }

        private void ResolveTextReferences()
        {
            timeText = timeText != null
                ? timeText
                : FindChildText("PinballTimeText", "TimeText");
            scoreText = scoreText != null
                ? scoreText
                : FindChildText("PinballScoreText", "ScoreText");
        }

        private void BuildDefaultLayout()
        {
            var canvasRect = GetComponent<RectTransform>();
            if (canvasRect.sizeDelta == Vector2.zero)
            {
                canvasRect.sizeDelta = new Vector2(720f, 300f);
            }

            if (timeText == null)
            {
                timeText = CreateText(
                    "PinballTimeText",
                    new Vector2(0f, 74f),
                    72f,
                    FontStyles.Bold,
                    timeColor,
                    new Vector2(0f, 0.5f),
                    new Vector2(1f, 0.5f),
                    new Vector2(-48f, 92f));
            }

            if (scoreText == null)
            {
                scoreText = CreateText(
                    "PinballScoreText",
                    new Vector2(0f, -54f),
                    126f,
                    FontStyles.Bold,
                    scoreColor,
                    new Vector2(0f, 0.5f),
                    new Vector2(1f, 0.5f),
                    new Vector2(-48f, 150f));
            }
        }

        private TMP_Text CreateText(
            string objectName,
            Vector2 anchoredPosition,
            float fontSize,
            FontStyles fontStyle,
            Color color,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 sizeDelta)
        {
            var textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(transform, false);

            var rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.sizeDelta = sizeDelta;
            rect.anchoredPosition = anchoredPosition;

            var text = textObject.GetComponent<TextMeshProUGUI>();
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.color = color;
            text.richText = false;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            return text;
        }

        private void ApplyTextDefaults()
        {
            ConfigureText(timeText, timeColor);
            ConfigureText(scoreText, scoreColor);
        }

        private static void ConfigureText(TMP_Text text, Color color)
        {
            if (text == null)
            {
                return;
            }

            // Keep TMP settings predictable even when artists replace the text objects.
            text.richText = false;
            text.color = color;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;

            if (text.font == null)
            {
                text.font = TMP_Settings.defaultFontAsset;
            }

            text.ForceMeshUpdate();
        }

        private TMP_Text FindChildText(params string[] names)
        {
            var transforms = GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < transforms.Length; i++)
            {
                var candidate = transforms[i];
                if (candidate == null)
                {
                    continue;
                }

                for (var nameIndex = 0; nameIndex < names.Length; nameIndex++)
                {
                    if (candidate.name == names[nameIndex])
                    {
                        return candidate.GetComponent<TMP_Text>();
                    }
                }
            }

            return null;
        }
    }
}

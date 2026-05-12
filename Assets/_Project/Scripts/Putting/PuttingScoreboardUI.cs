using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ArcadeRoom.Putting
{
    [RequireComponent(typeof(Canvas))]
    [DisallowMultipleComponent]
    public sealed class PuttingScoreboardUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text strokeCountText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Color readyColor = new(0.9f, 1f, 0.85f, 1f);
        [SerializeField] private Color holedColor = new(0.45f, 1f, 0.45f, 1f);

        public void EnsureLayout()
        {
            ResolveTextReferences();

            if (strokeCountText == null || statusText == null)
            {
                BuildDefaultLayout();
                ResolveTextReferences();
            }
        }

        public void Refresh(int strokeCount, bool allBallsHoled)
        {
            EnsureLayout();

            if (strokeCountText != null)
            {
                strokeCountText.text = $"STROKES {strokeCount:00}";
            }

            if (statusText != null)
            {
                statusText.text = allBallsHoled ? "IN THE HOLE" : "READY";
                statusText.color = allBallsHoled ? holedColor : readyColor;
            }
        }

        private void ResolveTextReferences()
        {
            strokeCountText = strokeCountText != null
                ? strokeCountText
                : FindChildText("PuttingStrokeText", "StrokeCountText", "StrokeText");
            statusText = statusText != null
                ? statusText
                : FindChildText("PuttingStatusText", "StatusText");
        }

        private void BuildDefaultLayout()
        {
            var canvasRect = GetComponent<RectTransform>();
            if (canvasRect.sizeDelta == Vector2.zero)
            {
                canvasRect.sizeDelta = new Vector2(780f, 280f);
            }

            if (transform.Find("PuttingPanel") == null)
            {
                var panelObject = new GameObject("PuttingPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                panelObject.transform.SetParent(transform, false);

                var panelRect = panelObject.GetComponent<RectTransform>();
                panelRect.anchorMin = Vector2.zero;
                panelRect.anchorMax = Vector2.one;
                panelRect.offsetMin = Vector2.zero;
                panelRect.offsetMax = Vector2.zero;

                var panelImage = panelObject.GetComponent<Image>();
                panelImage.color = new Color(0.04f, 0.08f, 0.06f, 0.78f);
            }

            if (strokeCountText == null)
            {
                strokeCountText = CreateText(
                    "PuttingStrokeText",
                    new Vector2(0f, 36f),
                    78f,
                    FontStyles.Bold,
                    Color.white);
            }

            if (statusText == null)
            {
                statusText = CreateText(
                    "PuttingStatusText",
                    new Vector2(0f, -74f),
                    38f,
                    FontStyles.Bold,
                    readyColor);
            }
        }

        private TMP_Text CreateText(string objectName, Vector2 anchoredPosition, float fontSize, FontStyles fontStyle, Color color)
        {
            var textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(transform, false);

            var rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.sizeDelta = new Vector2(-72f, 92f);
            rect.anchoredPosition = anchoredPosition;

            var text = textObject.GetComponent<TextMeshProUGUI>();
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.color = color;
            text.raycastTarget = false;
            return text;
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

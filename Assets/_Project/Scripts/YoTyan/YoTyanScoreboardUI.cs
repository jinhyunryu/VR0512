using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ArcadeRoom.YoTyan
{
    [RequireComponent(typeof(Canvas))]
    [DisallowMultipleComponent]
    public sealed class YoTyanScoreboardUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text blackScoreText;
        [SerializeField] private TMP_Text redScoreText;

        private Transform _blackBallCountRoot;
        private Transform _redBallCountRoot;
        private readonly System.Collections.Generic.List<GameObject> _blackBallCountSlots = new();
        private readonly System.Collections.Generic.List<GameObject> _redBallCountSlots = new();
        private GameObject _blackTurnOn;
        private GameObject _blackTurnOff;
        private GameObject _redTurnOn;
        private GameObject _redTurnOff;
        private Coroutine _turnBlinkRoutine;

        public void EnsureLayout()
        {
            ResolvePrefabLayoutReferences();
        }

        public void Refresh(int blackScore, int redScore, int blackBallsRemaining, int redBallsRemaining, YoTyanBallTeam currentTurn, bool gameOver)
        {
            EnsureLayout();

            if (blackScoreText != null)
            {
                blackScoreText.text = blackScore.ToString("00");
            }

            if (redScoreText != null)
            {
                redScoreText.text = redScore.ToString("00");
            }

            UpdateTurnIndicators(currentTurn, gameOver);
            UpdateBallCountSlots(_blackBallCountSlots, blackBallsRemaining);
            UpdateBallCountSlots(_redBallCountSlots, redBallsRemaining);
        }

        public void PlayExtraTurnBlink(YoTyanBallTeam team)
        {
            EnsureLayout();

            if (!gameObject.activeInHierarchy)
            {
                return;
            }

            var target = team == YoTyanBallTeam.Black ? _blackTurnOn : _redTurnOn;
            if (target == null)
            {
                return;
            }

            if (_turnBlinkRoutine != null)
            {
                StopCoroutine(_turnBlinkRoutine);
            }

            _turnBlinkRoutine = StartCoroutine(BlinkTurnIndicatorRoutine(team));
        }

        private void ResolvePrefabLayoutReferences()
        {
            blackScoreText = blackScoreText != null ? blackScoreText : FindChildComponent<TMP_Text>("BlackPlayer_Score");
            redScoreText = redScoreText != null ? redScoreText : FindChildComponent<TMP_Text>("RedPlayer_Score");
            _blackBallCountRoot = _blackBallCountRoot != null ? _blackBallCountRoot : FindChildTransform("BlackPlayerBall_Count");
            _redBallCountRoot = _redBallCountRoot != null ? _redBallCountRoot : FindChildTransform("RedPlayerBall_Count");
            _blackTurnOn = _blackTurnOn != null ? _blackTurnOn : FindChildGameObject("BlackPlayerTurnOn");
            _blackTurnOff = _blackTurnOff != null ? _blackTurnOff : FindChildGameObject("BlackPlayerTurnOff");
            _redTurnOn = _redTurnOn != null ? _redTurnOn : FindChildGameObject("RedPlayerTurnOn");
            _redTurnOff = _redTurnOff != null ? _redTurnOff : FindChildGameObject("RedPlayerTurnOff");

            RefreshBallCountSlots(_blackBallCountRoot, _blackBallCountSlots);
            RefreshBallCountSlots(_redBallCountRoot, _redBallCountSlots);
        }

        private void UpdateTurnIndicators(YoTyanBallTeam currentTurn, bool gameOver)
        {
            var blackActive = !gameOver && currentTurn == YoTyanBallTeam.Black;
            var redActive = !gameOver && currentTurn == YoTyanBallTeam.Red;

            SetContainerImageVisible(_blackTurnOff, !blackActive);
            SetContainerImageVisible(_redTurnOff, !redActive);
            SetActiveIfNeeded(_blackTurnOn, blackActive);
            SetActiveIfNeeded(_redTurnOn, redActive);
        }

        private static void UpdateBallCountSlots(System.Collections.Generic.List<GameObject> slots, int activeCount)
        {
            for (var i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot == null)
                {
                    continue;
                }

                SetActiveIfNeeded(slot, i < activeCount);
            }
        }

        private static void RefreshBallCountSlots(Transform root, System.Collections.Generic.List<GameObject> slots)
        {
            slots.Clear();
            if (root == null)
            {
                return;
            }

            var orderedChildren = new System.Collections.Generic.List<Transform>();
            for (var i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child != null && child.name.StartsWith("PlayerBall_Count", System.StringComparison.Ordinal))
                {
                    orderedChildren.Add(child);
                }
            }

            orderedChildren.Sort((left, right) => ExtractIndex(left.name).CompareTo(ExtractIndex(right.name)));

            for (var i = 0; i < orderedChildren.Count; i++)
            {
                slots.Add(orderedChildren[i].gameObject);
            }
        }

        private System.Collections.IEnumerator BlinkTurnIndicatorRoutine(YoTyanBallTeam team)
        {
            var target = team == YoTyanBallTeam.Black ? _blackTurnOn : _redTurnOn;
            if (target == null)
            {
                yield break;
            }

            SetActiveIfNeeded(target, false);
            yield return new WaitForSeconds(0.09f);
            SetActiveIfNeeded(target, true);
            yield return new WaitForSeconds(0.09f);
            SetActiveIfNeeded(target, false);
            yield return new WaitForSeconds(0.09f);
            SetActiveIfNeeded(target, true);
            _turnBlinkRoutine = null;
        }

        private static void SetActiveIfNeeded(GameObject target, bool value)
        {
            if (target != null && target.activeSelf != value)
            {
                target.SetActive(value);
            }
        }

        private static void SetContainerImageVisible(GameObject target, bool value)
        {
            if (target == null)
            {
                return;
            }

            if (!target.activeSelf)
            {
                target.SetActive(true);
            }

            var graphic = target.GetComponent<Graphic>();
            if (graphic != null)
            {
                graphic.enabled = value;
            }
        }

        private T FindChildComponent<T>(string childName) where T : Component
        {
            var transforms = GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < transforms.Length; i++)
            {
                var candidate = transforms[i];
                if (!string.Equals(candidate.name, childName, System.StringComparison.Ordinal))
                {
                    continue;
                }

                return candidate.GetComponent<T>();
            }

            return null;
        }

        private Transform FindChildTransform(string childName)
        {
            var transforms = GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < transforms.Length; i++)
            {
                var candidate = transforms[i];
                if (string.Equals(candidate.name, childName, System.StringComparison.Ordinal))
                {
                    return candidate;
                }
            }

            return null;
        }

        private GameObject FindChildGameObject(string childName)
        {
            var transforms = GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < transforms.Length; i++)
            {
                var candidate = transforms[i];
                if (string.Equals(candidate.name, childName, System.StringComparison.Ordinal))
                {
                    return candidate.gameObject;
                }
            }

            return null;
        }

        private static int ExtractIndex(string value)
        {
            var number = 0;
            var started = false;
            for (var i = 0; i < value.Length; i++)
            {
                if (!char.IsDigit(value[i]))
                {
                    continue;
                }

                started = true;
                number = (number * 10) + (value[i] - '0');
            }

            return started ? number : int.MaxValue;
        }
    }
}

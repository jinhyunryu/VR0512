using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ArcadeRoom.Curling
{
    [RequireComponent(typeof(Canvas))]
    [DisallowMultipleComponent]
    public sealed class CurlingScoreboardUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text redScoreText;
        [SerializeField] private TMP_Text blueScoreText;

        private Transform _redBallCountRoot;
        private Transform _blueBallCountRoot;
        private readonly List<GameObject> _redBallCountSlots = new();
        private readonly List<GameObject> _blueBallCountSlots = new();
        private GameObject _redTurnOn;
        private GameObject _redTurnOff;
        private GameObject _blueTurnOn;
        private GameObject _blueTurnOff;
        private Coroutine _turnBlinkRoutine;

        public void EnsureLayout()
        {
            ResolvePrefabLayoutReferences();
        }

        public void Refresh(int redScore, int blueScore, int redBallsRemaining, int blueBallsRemaining, CurlingTeam currentTurn, bool gameOver)
        {
            EnsureLayout();

            if (redScoreText != null)
            {
                redScoreText.text = redScore.ToString("00");
            }

            if (blueScoreText != null)
            {
                blueScoreText.text = blueScore.ToString("00");
            }

            UpdateBallCountSlots(_redBallCountSlots, redBallsRemaining);
            UpdateBallCountSlots(_blueBallCountSlots, blueBallsRemaining);
            UpdateTurnIndicators(currentTurn, gameOver);
        }

        public void PlayTurnBlink(CurlingTeam team)
        {
            EnsureLayout();

            if (!gameObject.activeInHierarchy)
            {
                return;
            }

            var target = team == CurlingTeam.Red ? _redTurnOn : _blueTurnOn;
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
            redScoreText = redScoreText != null ? redScoreText : FindChildComponent<TMP_Text>("RedPlayer_Score");
            blueScoreText = blueScoreText != null ? blueScoreText : FindChildComponent<TMP_Text>("BluePlayer_Score");
            _redBallCountRoot = _redBallCountRoot != null ? _redBallCountRoot : FindChildTransform("RedPlayerBall_Count");
            _blueBallCountRoot = _blueBallCountRoot != null ? _blueBallCountRoot : FindChildTransform("BluePlayerBall_Count");
            _redTurnOn = _redTurnOn != null ? _redTurnOn : FindChildGameObject("RedPlayerTurnOn");
            _redTurnOff = _redTurnOff != null ? _redTurnOff : FindChildGameObject("RedPlayerTurnOff");
            _blueTurnOn = _blueTurnOn != null ? _blueTurnOn : FindChildGameObject("BluePlayerTurnOn");
            _blueTurnOff = _blueTurnOff != null ? _blueTurnOff : FindChildGameObject("BluePlayerTurnOff");

            RefreshBallCountSlots(_redBallCountRoot, _redBallCountSlots);
            RefreshBallCountSlots(_blueBallCountRoot, _blueBallCountSlots);
        }

        private void UpdateTurnIndicators(CurlingTeam currentTurn, bool gameOver)
        {
            var redActive = !gameOver && currentTurn == CurlingTeam.Red;
            var blueActive = !gameOver && currentTurn == CurlingTeam.Blue;

            SetContainerImageVisible(_redTurnOff, !redActive);
            SetContainerImageVisible(_blueTurnOff, !blueActive);
            SetActiveIfNeeded(_redTurnOn, redActive);
            SetActiveIfNeeded(_blueTurnOn, blueActive);
        }

        private static void UpdateBallCountSlots(List<GameObject> slots, int activeCount)
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

        private static void RefreshBallCountSlots(Transform root, List<GameObject> slots)
        {
            slots.Clear();
            if (root == null)
            {
                return;
            }

            var orderedChildren = new List<Transform>();
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

        private IEnumerator BlinkTurnIndicatorRoutine(CurlingTeam team)
        {
            var target = team == CurlingTeam.Red ? _redTurnOn : _blueTurnOn;
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
                if (string.Equals(candidate.name, childName, System.StringComparison.Ordinal))
                {
                    return candidate.GetComponent<T>();
                }
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

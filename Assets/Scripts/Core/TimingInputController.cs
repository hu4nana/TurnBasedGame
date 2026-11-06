using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public enum TimingGrade
{
    Miss,
    Good,
    Perfect
}

[DisallowMultipleComponent]
public sealed class TimingInputController : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private RectTransform track;
    [SerializeField] private RectTransform marker;
    [SerializeField] private UnityEngine.UI.Image resultFlash;
    private bool cancelled;

    public void Cancel() { cancelled = true; HideImmediate(); }
    private void OnDisable() => Cancel();

    public void Configure(
        CanvasGroup newCanvasGroup,
        RectTransform newTrack,
        RectTransform newMarker,
        UnityEngine.UI.Image newResultFlash)
    {
        canvasGroup = newCanvasGroup;
        track = newTrack;
        marker = newMarker;
        resultFlash = newResultFlash;
        HideImmediate();
    }

    public IEnumerator Run(float duration, Action<TimingGrade> completed)
    {
        cancelled = false;
        if (canvasGroup == null || track == null || marker == null)
        {
            completed?.Invoke(TimingGrade.Miss);
            yield break;
        }

        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = false;
        float elapsed = 0f;
        TimingGrade grade = TimingGrade.Miss;
        bool decided = false;

        while (elapsed < duration && !decided && !cancelled)
        {
            elapsed += Time.unscaledDeltaTime;
            float normalized = Mathf.Clamp01(elapsed / duration);
            float x = (normalized - 0.5f) * track.rect.width;
            marker.anchoredPosition = new Vector2(x, marker.anchoredPosition.y);

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
            {
                float centerDistance = Mathf.Abs(normalized - 0.5f);
                grade = centerDistance <= 0.055f
                    ? TimingGrade.Perfect
                    : centerDistance <= 0.16f
                        ? TimingGrade.Good
                        : TimingGrade.Miss;
                decided = true;
            }
            yield return null;
        }

        if (cancelled) yield break;
        ShowResult(grade);
        completed?.Invoke(grade);
        yield return new WaitForSecondsRealtime(0.28f);
        HideImmediate();
    }

    private void ShowResult(TimingGrade grade)
    {
        if (resultFlash == null) return;
        resultFlash.color = grade switch
        {
            TimingGrade.Perfect => new Color(1f, 0.9f, 0.2f, 0.9f),
            TimingGrade.Good => new Color(0.2f, 1f, 0.55f, 0.8f),
            _ => new Color(1f, 0.18f, 0.15f, 0.75f)
        };
    }

    private void HideImmediate()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
        }
        if (resultFlash != null) resultFlash.color = Color.clear;
    }
}

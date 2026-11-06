using System.Collections;
using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CombatFocusDirector : MonoBehaviour
{
    [SerializeField] private BattleFieldView battleFieldView;
    [SerializeField] private TacticalStateController stateController;
    [SerializeField] private TimingInputController timingInput;
    [SerializeField] private BattleCameraController cameraController;
    [SerializeField] private CharacterVisualManager visualManager;

    private Camera battleCamera;
    private Coroutine sequence;
    private bool busy;
    private bool advancing;
    private bool captured;
    private bool savedCameraEnabled;
    private Vector3 savedPosition;
    private Quaternion savedRotation;
    private float savedFov;
    private GridOccupant activeAttacker;
    private GridOccupant activeTarget;
    private BattleField activeBoard;

    public bool IsBusy => busy;
    public Exception LastFailure { get; private set; }
    public event Action<Exception> AttackFailed;
    public TimingGrade LastTimingGrade { get; private set; } = TimingGrade.Miss;
    public float LastPowerMultiplier { get; private set; } = 0.75f;
    public event Action<GridOccupant, GridOccupant, TimingGrade, float> AttackResolved;

    public void Configure(
        BattleFieldView newBattleFieldView,
        TacticalStateController newStateController,
        TimingInputController newTimingInput,
        BattleCameraController newCameraController)
    {
        battleFieldView = newBattleFieldView;
        stateController = newStateController;
        timingInput = newTimingInput;
        cameraController = newCameraController;
        battleCamera = newCameraController != null ? newCameraController.GetComponent<Camera>() : null;
        visualManager = GetComponent<CharacterVisualManager>();
    }

    public bool TryStartAttack(GridOccupant attacker, GridOccupant target)
    {
        if (!isActiveAndEnabled || busy || advancing || !ThreatEvaluator.CanThreaten(attacker, target)) return false;
        if (battleCamera == null && cameraController != null) battleCamera = cameraController.GetComponent<Camera>();
        if (battleCamera == null) return false;
        activeAttacker = attacker;
        activeTarget = target;
        activeBoard = attacker.Board;
        activeBoard.OccupancyChanged += ValidateParticipants;
        activeBoard.RulesChanged += ValidateParticipants;
        savedPosition = battleCamera.transform.position;
        savedRotation = battleCamera.transform.rotation;
        savedFov = battleCamera.fieldOfView;
        savedCameraEnabled = cameraController != null && cameraController.enabled;
        captured = true;
        busy = true;
        LastFailure = null;
        Coroutine started = StartCoroutine(RunSafely(AttackSequence(attacker, target)));
        if (busy) sequence = started;
        return true;
    }

    public void CancelAttack()
    {
        if (!busy) return;
        Coroutine running = sequence;
        Restore();
        if (!advancing && running != null) StopCoroutine(running);
    }

    private void OnDisable() => CancelAttack();
    private void OnDestroy() => CancelAttack();

    private void ValidateParticipants()
    {
        if (busy && !ThreatEvaluator.CanThreaten(activeAttacker, activeTarget)) CancelAttack();
    }

    // Flatten nested iterators so failures inside timing/camera code also reach the same cleanup path.
    private IEnumerator RunSafely(IEnumerator action)
    {
        Stack<IEnumerator> stack = new();
        stack.Push(action);
        Exception failure = null;
        try
        {
            while (busy && stack.Count > 0)
            {
                ValidateParticipants();
                if (!busy) break;
                IEnumerator current = stack.Peek();
                bool next = false;
                object yielded = null;
                try
                {
                    advancing = true;
                    next = current.MoveNext();
                    if (next) yielded = current.Current;
                }
                catch (Exception error) { failure = error; }
                finally { advancing = false; }
                if (failure != null || !busy) break;
                if (!next) { stack.Pop(); (current as IDisposable)?.Dispose(); continue; }
                if (yielded is IEnumerator nested) { stack.Push(nested); continue; }
                yield return yielded;
            }
        }
        finally
        {
            while (stack.Count > 0)
            {
                try { (stack.Pop() as IDisposable)?.Dispose(); }
                catch (Exception error) { failure ??= error; }
            }
            Restore();
        }
        if (failure != null)
        {
            LastFailure = failure;
            if (AttackFailed != null) AttackFailed(failure);
            else Debug.LogException(failure, this);
        }
    }

    private void Restore()
    {
        busy = false;
        sequence = null;
        if (activeBoard != null)
        {
            activeBoard.OccupancyChanged -= ValidateParticipants;
            activeBoard.RulesChanged -= ValidateParticipants;
        }
        activeBoard = null;
        activeAttacker = activeTarget = null;
        if (!captured) return;
        captured = false;
        if (timingInput != null) timingInput.Cancel();
        if (visualManager != null) visualManager.Restore();
        if (battleFieldView != null) battleFieldView.SetPresentationBrightness(1f);
        if (battleCamera != null)
        {
            battleCamera.transform.SetPositionAndRotation(savedPosition, savedRotation);
            battleCamera.fieldOfView = savedFov;
        }
        if (cameraController != null)
        {
            cameraController.NotifyRotationChanged();
            cameraController.enabled = savedCameraEnabled;
        }
    }

    private IEnumerator AttackSequence(GridOccupant attacker, GridOccupant target)
    {
        if (cameraController != null) cameraController.enabled = false;

        if (visualManager == null) visualManager = GetComponent<CharacterVisualManager>();
        visualManager?.Focus(attacker, target);
        battleFieldView?.SetPresentationBrightness(0.32f);

        Vector3 attackerPosition = attacker.transform.position;
        Vector3 targetPosition = target.transform.position;
        Vector3 midpoint = (attackerPosition + targetPosition) * 0.5f + Vector3.up * 1.05f;
        Vector3 battleLine = targetPosition - attackerPosition;
        Vector3 viewDirection = Vector3.Cross(Vector3.up, battleLine).normalized;
        if (Vector3.Dot(viewDirection, savedPosition - midpoint) < 0f) viewDirection = -viewDirection;
        Vector3 focusCameraPosition = midpoint + viewDirection * 5.4f + Vector3.up * 2.4f;
        Quaternion focusCameraRotation = Quaternion.LookRotation(midpoint - focusCameraPosition, Vector3.up);

        yield return MoveCamera(savedPosition, savedRotation, savedFov,
            focusCameraPosition, focusCameraRotation, 37f, 0.38f);

        TimingGrade grade = TimingGrade.Miss;
        if (timingInput != null)
            yield return timingInput.Run(1.35f, result => grade = result);

        LastTimingGrade = grade;
        LastPowerMultiplier = grade switch
        {
            TimingGrade.Perfect => 1.5f,
            TimingGrade.Good => 1f,
            _ => 0.75f
        };

        Vector3 direction = (targetPosition - attackerPosition).normalized;
        GridOccupantVisualizer attackerVisual = attacker.GetComponent<GridOccupantVisualizer>();
        GridOccupantVisualizer targetVisual = target.GetComponent<GridOccupantVisualizer>();
        float lunge = grade == TimingGrade.Perfect ? 0.85f : grade == TimingGrade.Good ? 0.6f : 0.28f;
        attackerVisual?.SetPresentation(1f, true, direction * lunge);
        yield return new WaitForSecondsRealtime(0.12f);
        targetVisual?.Flash(grade == TimingGrade.Miss ? Color.gray : Color.white, 0.14f);
        attackerVisual?.SetPresentation(1f, true, Vector3.zero);
        stateController?.RegisterAttack(attacker, target);
        AttackResolved?.Invoke(attacker, target, grade, LastPowerMultiplier);
        yield return new WaitForSecondsRealtime(0.24f);

        yield return MoveCamera(focusCameraPosition, focusCameraRotation, 37f,
            savedPosition, savedRotation, savedFov, 0.35f);

    }

    private IEnumerator MoveCamera(
        Vector3 fromPosition,
        Quaternion fromRotation,
        float fromFov,
        Vector3 toPosition,
        Quaternion toRotation,
        float toFov,
        float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            battleCamera.transform.SetPositionAndRotation(
                Vector3.LerpUnclamped(fromPosition, toPosition, t),
                Quaternion.SlerpUnclamped(fromRotation, toRotation, t));
            battleCamera.fieldOfView = Mathf.LerpUnclamped(fromFov, toFov, t);
            cameraController?.NotifyRotationChanged();
            yield return null;
        }
        battleCamera.transform.SetPositionAndRotation(toPosition, toRotation);
        battleCamera.fieldOfView = toFov;
        cameraController?.NotifyRotationChanged();
    }
}

using System.Collections;
using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CombatFocusDirector : MonoBehaviour
{
    [SerializeField] private BattleFieldView battleFieldView;
    [SerializeField] private TacticalStateController stateController;
    [SerializeField] private TimingInputController timingInput;
    [SerializeField] private BattleCameraController cameraController;

    private Camera battleCamera;
    private Coroutine sequence;

    public bool IsBusy => sequence != null;
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
        battleCamera = newCameraController != null ? newCameraController.GetComponent<Camera>() : Camera.main;
    }

    public bool TryStartAttack(GridOccupant attacker, GridOccupant target)
    {
        if (IsBusy || !ThreatEvaluator.CanThreaten(attacker, target)) return false;
        sequence = StartCoroutine(AttackSequence(attacker, target));
        return true;
    }

    private IEnumerator AttackSequence(GridOccupant attacker, GridOccupant target)
    {
        if (battleCamera == null) battleCamera = Camera.main;
        if (battleCamera == null)
        {
            sequence = null;
            yield break;
        }

        Vector3 savedPosition = battleCamera.transform.position;
        Quaternion savedRotation = battleCamera.transform.rotation;
        float savedFov = battleCamera.fieldOfView;
        if (cameraController != null) cameraController.enabled = false;

        GridOccupantVisualizer[] visuals =
            UnityEngine.Object.FindObjectsByType<GridOccupantVisualizer>(FindObjectsSortMode.None);
        foreach (GridOccupantVisualizer visual in visuals)
        {
            bool focused = visual.gameObject == attacker.gameObject || visual.gameObject == target.gameObject;
            visual.SetPresentation(focused ? 1f : 0.16f, focused, Vector3.zero);
        }
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

        foreach (GridOccupantVisualizer visual in visuals) visual.ResetPresentation();
        battleFieldView?.SetPresentationBrightness(1f);
        if (cameraController != null) cameraController.enabled = true;
        sequence = null;
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
            yield return null;
        }
        battleCamera.transform.SetPositionAndRotation(toPosition, toRotation);
        battleCamera.fieldOfView = toFov;
    }
}

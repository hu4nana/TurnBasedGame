using System.Collections.Generic;
using UnityEngine;

/// <summary>Registration-based visual ownership. No per-frame polling or scene searches.</summary>
[ExecuteAlways, DisallowMultipleComponent]
public sealed class CharacterVisualManager : MonoBehaviour
{
    [SerializeField] private BattleCameraController battleCamera;
    private readonly HashSet<GridOccupantVisualizer> visuals = new();
    private GridOccupant focusedAttacker;
    private GridOccupant focusedTarget;
    private bool focused;
    public int RegisteredCount => visuals.Count;

    public void BindCamera(BattleCameraController camera)
    {
        Unsubscribe();
        battleCamera = camera;
        Subscribe();
        RefreshCameraFacing();
    }

    private void OnEnable() { Subscribe(); RefreshCameraFacing(); }
    private void OnDisable() => Unsubscribe();
    private void Subscribe()
    {
        if (battleCamera == null || !isActiveAndEnabled) return;
        battleCamera.RotationChanged -= RefreshCameraFacing;
        battleCamera.RotationChanged += RefreshCameraFacing;
    }
    private void Unsubscribe() { if (battleCamera != null) battleCamera.RotationChanged -= RefreshCameraFacing; }

    public void Register(GridOccupantVisualizer visual)
    {
        if (!visuals.Add(visual)) return;
        if (battleCamera != null) visual.SetFacing(battleCamera.transform.forward);
        if (focused)
        {
            bool participant = visual.gameObject == focusedAttacker?.gameObject || visual.gameObject == focusedTarget?.gameObject;
            visual.SetPresentation(participant ? 1f : 0.16f, participant, Vector3.zero);
        }
    }

    public void Unregister(GridOccupantVisualizer visual) => visuals.Remove(visual);

    public void RefreshSorting(GridOccupantVisualizer visual)
    {
        if (battleCamera != null) visual.RefreshSorting(battleCamera.transform.forward);
    }

    private void RefreshCameraFacing()
    {
        if (battleCamera == null) return;
        Vector3 forward = battleCamera.transform.forward;
        foreach (GridOccupantVisualizer visual in visuals)
            if (visual != null) visual.SetFacing(forward);
    }

    public void Focus(GridOccupant attacker, GridOccupant target)
    {
        focused = true;
        focusedAttacker = attacker;
        focusedTarget = target;
        foreach (GridOccupantVisualizer visual in visuals)
        {
            if (visual == null) continue;
            bool focused = visual.gameObject == attacker.gameObject || visual.gameObject == target.gameObject;
            visual.SetPresentation(focused ? 1f : 0.16f, focused, Vector3.zero);
        }
    }

    public void Restore()
    {
        focused = false;
        focusedAttacker = focusedTarget = null;
        foreach (GridOccupantVisualizer visual in visuals)
            if (visual != null) visual.ResetPresentation();
    }
}

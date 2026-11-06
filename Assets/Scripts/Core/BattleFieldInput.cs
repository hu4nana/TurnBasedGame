using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(BattleField))]
public sealed class BattleFieldInput : MonoBehaviour
{
    [SerializeField] private Camera inputCamera;
    [SerializeField] private BattleFieldView battleFieldView;
    [SerializeField] private CombatFocusDirector combatFocus;

    private BattleField battleField;
    private GridOccupant selected;
    private GridCoordinate? hoverCell;
    private bool attackMode;
    private bool wasBusy;
    private readonly List<GridCoordinate> moveCells = new();
    private readonly List<GridCoordinate> attackCells = new();

    private void Awake()
    {
        battleField = GetComponent<BattleField>();
        if (battleFieldView == null) battleFieldView = GetComponent<BattleFieldView>();
        if (combatFocus == null) combatFocus = GetComponent<CombatFocusDirector>();
    }

    public void BindCamera(Camera camera) => inputCamera = camera;

    private void OnEnable()
    {
        battleField = GetComponent<BattleField>();
        battleField.OccupancyChanged += RefreshAvailableCells;
        battleField.RulesChanged += RefreshAvailableCells;
    }

    private void OnDisable()
    {
        if (battleField != null) battleField.OccupancyChanged -= RefreshAvailableCells;
        if (battleField != null) battleField.RulesChanged -= RefreshAvailableCells;
    }

    private void Update()
    {
        bool busy = combatFocus != null && combatFocus.IsBusy;
        if (busy) { wasBusy = true; return; }
        if (wasBusy) { wasBusy = false; RefreshAvailableCells(); }

        Mouse mouse = Mouse.current;
        Keyboard keyboard = Keyboard.current;
        if (mouse == null || inputCamera == null) return;

        UpdateHover(mouse.position.ReadValue());

        if (mouse.rightButton.wasPressedThisFrame ||
            (keyboard != null && keyboard.escapeKey.wasPressedThisFrame))
        {
            ClearSelection();
            return;
        }

        if (keyboard != null && keyboard.fKey.wasPressedThisFrame && selected != null)
        {
            attackMode = !attackMode;
            RefreshAvailableCells();
        }

        if (!mouse.leftButton.wasPressedThisFrame || hoverCell == null) return;
        HandleCellClick(hoverCell.Value);
    }

    private void UpdateHover(Vector2 screenPosition)
    {
        Ray ray = inputCamera.ScreenPointToRay(screenPosition);
        GridCoordinate? next = battleField.RayToCoordinate(ray, out GridCoordinate coordinate)
            ? coordinate
            : null;
        if (next == hoverCell) return;
        hoverCell = next;
        battleFieldView?.SetHover(hoverCell);
    }

    private void HandleCellClick(GridCoordinate coordinate)
    {
        GridOccupant occupant = battleField.GetCell(coordinate)?.Occupant;

        if (selected == null)
        {
            if (occupant != null && occupant.Faction == BattleFaction.Player)
                Select(occupant);
            return;
        }

        if (attackMode)
        {
            if (occupant != null && attackCells.Contains(coordinate) &&
                combatFocus != null && combatFocus.TryStartAttack(selected, occupant))
            {
                attackMode = false;
                battleFieldView?.ClearInteractionHighlights();
            }
            return;
        }

        if (occupant != null && occupant.Faction == BattleFaction.Player)
        {
            Select(occupant);
            return;
        }

        if (occupant == null && moveCells.Contains(coordinate)) battleField.TryMove(selected, coordinate);
    }

    private void Select(GridOccupant occupant)
    {
        selected?.GetComponent<GridOccupantVisualizer>()?.SetSelected(false);
        selected = occupant;
        selected.GetComponent<GridOccupantVisualizer>()?.SetSelected(true);
        attackMode = false;
        RefreshAvailableCells();
    }

    private void ClearSelection()
    {
        selected?.GetComponent<GridOccupantVisualizer>()?.SetSelected(false);
        selected = null;
        attackMode = false;
        moveCells.Clear();
        attackCells.Clear();
        battleFieldView?.ClearInteractionHighlights();
    }

    private void RefreshAvailableCells()
    {
        if (selected == null || selected.Board != battleField || !selected.isActiveAndEnabled ||
            selected.Faction != BattleFaction.Player)
        {
            ClearSelection();
            return;
        }
        moveCells.Clear();
        attackCells.Clear();
        if (attackMode) battleField.FillThreatenedEnemyCells(selected, attackCells);
        else battleField.FillReachableCells(selected, moveCells);
        ApplyHighlights();
    }

    private void ApplyHighlights()
    {
        battleFieldView?.SetRanges(
            selected != null ? selected.Coordinate : null,
            moveCells,
            attackCells);
        battleFieldView?.SetHover(hoverCell);
    }
}

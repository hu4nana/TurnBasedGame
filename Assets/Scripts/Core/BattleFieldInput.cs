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
    private List<GridCoordinate> moveCells = new();
    private List<GridCoordinate> attackCells = new();

    private void Awake()
    {
        battleField = GetComponent<BattleField>();
        if (inputCamera == null) inputCamera = Camera.main;
        if (battleFieldView == null) battleFieldView = GetComponent<BattleFieldView>();
        if (combatFocus == null) combatFocus = GetComponent<CombatFocusDirector>();
    }

    private void Update()
    {
        if (combatFocus != null && combatFocus.IsBusy) return;

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
        hoverCell = battleField.RayToCoordinate(ray, out GridCoordinate coordinate)
            ? coordinate
            : null;
        ApplyHighlights();
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

        if (occupant == null && moveCells.Contains(coordinate) && battleField.TryMove(selected, coordinate))
            RefreshAvailableCells();
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
        moveCells = selected != null && !attackMode
            ? battleField.GetReachableCells(selected)
            : new List<GridCoordinate>();
        attackCells = selected != null && attackMode
            ? battleField.GetThreatenedEnemyCells(selected)
            : new List<GridCoordinate>();
        ApplyHighlights();
    }

    private void ApplyHighlights()
    {
        battleFieldView?.SetInteractionHighlights(
            selected != null ? selected.Coordinate : null,
            moveCells,
            attackCells,
            hoverCell);
    }
}

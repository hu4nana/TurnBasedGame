using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class TacticalStateController : MonoBehaviour
{
    [SerializeField] private BattleField battleField;

    private readonly HashSet<GridOccupant> engagedUntilOwnTurn = new();

    private void Awake()
    {
        if (battleField == null) battleField = GetComponent<BattleField>();
    }

    private void OnEnable()
    {
        if (battleField == null) battleField = GetComponent<BattleField>();
        if (battleField != null) battleField.OccupancyChanged += RecalculateAll;
    }

    private void Start() => RecalculateAll();

    private void OnDisable()
    {
        if (battleField != null) battleField.OccupancyChanged -= RecalculateAll;
    }

    public void RegisterAttack(GridOccupant attacker, GridOccupant target)
    {
        if (attacker == null || target == null) return;
        if (attacker.Board != battleField || target.Board != battleField) return;
        if (!ThreatEvaluator.AreEnemies(attacker, target)) return;

        engagedUntilOwnTurn.Add(attacker);
        RecalculateAll();
    }

    public void BeginTurn(GridOccupant activeCharacter)
    {
        if (activeCharacter == null) return;
        engagedUntilOwnTurn.Remove(activeCharacter);
        RecalculateAll();
    }

    public void RecalculateAll()
    {
        if (battleField == null) return;

        List<GridOccupant> occupants = new(battleField.Occupants);
        engagedUntilOwnTurn.RemoveWhere(occupant => occupant == null || occupant.Board != battleField);

        foreach (GridOccupant occupant in occupants)
        {
            TacticalState nextState = engagedUntilOwnTurn.Contains(occupant)
                ? TacticalState.Engaged
                : HasMutualThreat(occupant, occupants)
                    ? TacticalState.Standoff
                    : TacticalState.Guard;
            occupant.SetTacticalState(nextState);
        }
    }

    private static bool HasMutualThreat(GridOccupant subject, List<GridOccupant> occupants)
    {
        foreach (GridOccupant other in occupants)
            if (ThreatEvaluator.IsMutualThreat(subject, other))
                return true;
        return false;
    }
}

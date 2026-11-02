public static class ThreatEvaluator
{
    public static bool AreEnemies(GridOccupant first, GridOccupant second)
    {
        if (first == null || second == null || first == second) return false;
        if (first.Faction == BattleFaction.Neutral || second.Faction == BattleFaction.Neutral)
            return false;
        return first.Faction != second.Faction;
    }

    public static bool CanThreaten(GridOccupant attacker, GridOccupant target)
    {
        if (!AreEnemies(attacker, target)) return false;
        if (!attacker.IsPlaced || !target.IsPlaced || attacker.Board != target.Board) return false;
        return attacker.Coordinate.ManhattanDistance(target.Coordinate) <= attacker.ThreatRange;
    }

    public static bool IsMutualThreat(GridOccupant first, GridOccupant second) =>
        CanThreaten(first, second) && CanThreaten(second, first);
}

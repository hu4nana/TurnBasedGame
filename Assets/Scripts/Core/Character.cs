using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Stats
{
    public int hp;
    public int attack;
    public int def;
    public int dodge;
    public int speed;
    public int registnce;
    public int accuracy;
}

public class Character : MonoBehaviour, IDamagable
{
    [SerializeField] private Stats stats = new Stats();
    CharacterConditions characterconditions;
    public Dictionary<int, CrowdControl> crowdcontrol = new();
    public Dictionary<int, StatusCondition> statuscondition = new();
    public Stats Stats => stats;
    public bool IsDead => characterconditions == CharacterConditions.DEAD;

    public virtual void Initialized()
    {
        characterconditions = CharacterConditions.NORMAL;
    }
    public void Damaged(int damage)
    {
        if (IsDead || damage <= 0) return;
        stats.hp = Mathf.Max(0, stats.hp - damage);
        if (stats.hp == 0)
            Dead();
    }
    public void Dead()
    {
        if (IsDead) return;
        stats.hp = 0;
        characterconditions = CharacterConditions.DEAD;
        GridOccupant occupant = GetComponent<GridOccupant>();
        if (occupant != null && occupant.Board != null) occupant.Board.TryRemove(occupant);
    }
    
    public virtual void Start() { if (!IsDead) Initialized(); }
}

using System;
using System.Collections.Generic;
using System.ComponentModel;
using NUnit.Framework.Constraints;
using Unity.VisualScripting;
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
    public Dictionary<int, CrowdControl> crowdcontrol;
    public Dictionary<int, StatusCondition> statuscondition;
    public Stats Stats => stats;

    public virtual void Initialized()
    {
        characterconditions = CharacterConditions.NORMAL;
    }
    public void Damaged(int damage)
    {
        stats.hp -= damage;
        if (stats.hp < 0)
            Dead();
    }
    public void Dead()
    {
        characterconditions = CharacterConditions.DEAD;
    }
    
    public virtual void Start()
    {
        
    }
    
    public virtual void Update()
    {
        if (characterconditions == CharacterConditions.DEAD)
            return;
    }
}

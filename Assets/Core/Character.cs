using System;
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
    
    public Stats Stats => stats;

    public void Damaged(int damage)
    {
        stats.hp -= damage;
        if (stats.hp < 0)
            Dead();
    }

    public void Dead()
    {
        
    }
}

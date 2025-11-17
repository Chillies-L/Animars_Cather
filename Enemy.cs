using System;
using System.Collections.Generic;
using UnityEngine;

public class Enemy : MonoBehaviour
{
    [Header("Stats")]
    public float moveSpeed = 3f;
    public float maxHP = 50f;
    public int goldReward = 5;
    public int damageToBase = 10;   // BattleManager 用这个字段

    [Header("VFX/SFX")]
    public GameObject deathEffect;
    public AudioClip deathSound;

    private AudioSource audioSource;
    private float currentHP;
    private List<Vector3> path;
    private int currentIndex;

    // 供 BattleManager 订阅
    public event Action<Enemy> onDeath;
    public event Action<Enemy> onReachGoal;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
    }

    /// <summary>BattleManager 调用：设置路径，并根据当前波次做强度提升。</summary>
    public void InitPath(List<Vector3> pathPoints)
    {
        path = pathPoints;
        if (path == null || path.Count == 0)
        {
            Debug.LogError("Enemy.InitPath: 路径为空。");
            enabled = false;
            return;
        }

        // 根据当前波次做简单强度提升
        int wave = Mathf.Max(1, BattleManager.Instance ? BattleManager.Instance.wave : 1);
        currentHP = maxHP * Mathf.Pow(1.1f, wave);
        goldReward += Mathf.RoundToInt(wave * 1.5f);
        moveSpeed = 3f + wave * 0.05f;

        transform.position = path[0];
        currentIndex = 0;
    }

    private void Update()
    {
        if (path == null || currentIndex >= path.Count) return;

        Vector3 target = path[currentIndex];
        Vector3 dir = (target - transform.position).normalized;
        transform.position += dir * moveSpeed * Time.deltaTime;

        if (Vector3.Distance(transform.position, target) < 0.25f)
        {
            currentIndex++;
            if (currentIndex >= path.Count)
            {
                // 到达终点
                onReachGoal?.Invoke(this);
                Destroy(gameObject);
            }
        }
    }

    public void TakeDamage(float dmg)
    {
        currentHP -= dmg;
        if (currentHP <= 0f)
        {
            Die();
        }
    }

    private void Die()
    {
        if (deathEffect) Instantiate(deathEffect, transform.position, Quaternion.identity);
        if (audioSource && deathSound) audioSource.PlayOneShot(deathSound);

        onDeath?.Invoke(this);
        Destroy(gameObject);
    }
}

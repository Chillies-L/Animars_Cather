using UnityEngine;

public class Tower : MonoBehaviour
{
    public int towerID;
    public int level = 1;
    public float detectRange = 10f;
    public float fireRate = 1.5f;
    public float baseDamage = 10f;
    public LayerMask targetMask;

    private float fireTimer;

    private void Update()
    {
        fireTimer += Time.deltaTime;
        if (fireTimer >= fireRate)
        {
            var target = FindTarget();
            if (target != null)
            {
                Shoot(target);
                fireTimer = 0f;
            }
        }
    }

    Enemy FindTarget()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, detectRange, targetMask);
        float min = float.MaxValue;
        Enemy best = null;

        foreach (var hit in hits)
        {
            Enemy e = hit.GetComponent<Enemy>();
            if (e != null)
            {
                float d = Vector3.Distance(transform.position, e.transform.position);
                if (d < min)
                {
                    min = d;
                    best = e;
                }
            }
        }
        return best;
    }

    void Shoot(Enemy e)
    {
        e.TakeDamage(baseDamage * level);
    }

    public void Upgrade()
    {
        level++;
        baseDamage *= 2f;
        fireRate = Mathf.Max(0.5f, fireRate - 0.1f);
        Debug.Log($"升级完成：等级 {level}，伤害 {baseDamage}");
    }
}

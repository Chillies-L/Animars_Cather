using UnityEngine;

public class AutoDisable : MonoBehaviour
{
    [SerializeField] private float delay = 2f; // 可在 Inspector 调整时间

    private void OnEnable()
    {
        Invoke(nameof(DisableSelf), delay);
    }

    private void DisableSelf()
    {
        gameObject.SetActive(false);
    }
}

using UnityEngine;
using UnityEngine.UI;

public class TerrainButtonController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("拖入场景中的 GridTerrainGenerator 脚本对象")]
    public GridTerrainGenerator generator;

    [Tooltip("重置按钮（恢复最原始网格）")]
    public Button resetButton;

    [Tooltip("重新生成按钮（从最原始网格重新生成地形）")]
    public Button regenerateButton;

    [Header("Optional Settings")]
    [Tooltip("重置后是否自动延迟几秒再重新生成（若为0则立即响应按钮）")]
    public float regenerateDelay = 0f;

    void Start()
    {
        if (resetButton != null)
            resetButton.onClick.AddListener(OnResetClicked);

        if (regenerateButton != null)
            regenerateButton.onClick.AddListener(OnRegenerateClicked);
    }

    void OnDestroy()
    {
        if (resetButton != null)
            resetButton.onClick.RemoveListener(OnResetClicked);
        if (regenerateButton != null)
            regenerateButton.onClick.RemoveListener(OnRegenerateClicked);
    }

    /// <summary>
    /// 点击【重置按钮】
    /// </summary>
    public void OnResetClicked()
    {
        if (generator == null)
        {
            Debug.LogError("未绑定 GridTerrainGenerator！");
            return;
        }

        generator.ResetAllToOrigin();
        Debug.Log("地形已重置为最原始状态。");
    }

    /// <summary>
    /// 点击【重新生成按钮】
    /// </summary>
    public void OnRegenerateClicked()
    {
        if (generator == null)
        {
            Debug.LogError("未绑定 GridTerrainGenerator！");
            return;
        }

        // 先重置为原始网格，再触发地形生成
        generator.ResetAllToOrigin();
        if (regenerateDelay > 0)
            Invoke(nameof(StartGenerationAfterDelay), regenerateDelay);
        else
            StartGenerationAfterDelay();
    }

    void StartGenerationAfterDelay()
    {
        generator.SignalGenerateAndRaise();
        Debug.Log("重新开始生成地形。");
    }
}

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using DG.Tweening;
using AnimarsCatcher;

public class SceneLoader : MonoBehaviour
{
    [Header("按钮设置")]
    public Button loadButton;                 // 触发加载的按钮
    public string sceneName = "GameScene";    // 要加载的场景名（可在 Inspector 设置）
    public float transitionDuration = 0.5f;   // 动画持续时间

    [Header("可选动画对象")]
    public CanvasGroup fadePanel;             // 可选的淡出面板（用于过渡动画）

    private void Start()
    {
        if (loadButton != null)
        {
            loadButton.onClick.AddListener(OnLoadButtonClick);
        }

        if (fadePanel != null)
        {
            fadePanel.alpha = 0f;
            fadePanel.gameObject.SetActive(false);
        }
    }

    private void OnLoadButtonClick()
    {
        // 播放点击音效（如果有音频管理器）
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayUIBtnAudio();

        // 使用 DOTween 播放过渡动画
        if (fadePanel != null)
        {
            fadePanel.gameObject.SetActive(true);
            fadePanel.DOFade(1f, transitionDuration).SetEase(Ease.InOutQuad)
                .OnComplete(() =>
                {
                    LoadScene();
                });
        }
        else
        {
            // 如果没有过渡面板，直接加载场景
            LoadScene();
        }
    }

    private void LoadScene()
    {
        // 异步加载新场景
        SceneManager.LoadScene(sceneName);
    }
}

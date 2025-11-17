using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace AnimarsCatcher
{
    public class HPBar : MonoBehaviour
    {
        private ReactiveProperty<int> mHP;
        private Image mHPBar;
        private int mHPMax;

        [Header("显示当前HP/最大HP的TMP")]
        [SerializeField] private TextMeshProUGUI mHpText;   // Inspector 拖一个 TMP 文本

        private void Awake()
        {
            // 确保 Image 正确引用（即血条填充图）
            if (mHPBar == null)
            {
                mHPBar = GetComponent<Image>();
                if (mHPBar == null)
                {
                    // 若自身没有 Image，则在子物体里找
                    mHPBar = GetComponentInChildren<Image>(true);
                }
            }

            // 尝试自动获取 TMP 文本
            if (mHpText == null)
                mHpText = GetComponentInChildren<TextMeshProUGUI>(true);
        }

        /// <summary>
        /// 初始化生命条
        /// </summary>
        public void Init(ReactiveProperty<int> hp)
        {
            if (hp == null)
            {
                Debug.LogError("[HPBar] Init() 参数 hp 为空！", this);
                return;
            }

            mHP = hp;
            mHPMax = Mathf.Max(1, mHP.Value);
            mHP.Subscribe(OnHPChanged);
            OnHPChanged(mHP.Value); // 初始刷新
        }

        private void OnHPChanged(int hp)
        {
            if (mHPBar == null)
            {
                Debug.LogWarning("[HPBar] mHPBar 未找到", this);
                return;
            }

            // 更新进度条
            float fill = Mathf.Clamp01(mHPMax > 0 ? (float)hp / mHPMax : 0f);
            mHPBar.fillAmount = fill;

            // 更新文字
            if (mHpText != null)
                mHpText.text = $"{Mathf.Max(0, hp)}/{mHPMax}";
        }
    }
}

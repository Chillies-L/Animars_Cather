using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class TowerMenuCanvas : MonoBehaviour
{
    public Button[] towerButtons;   // 购塔按钮（与 BattleManager.towerPrefabs 顺序一致）
    public Button upgradeButton;
    public Button sellButton;
    public Button closeButton;

    private ClickableCell parentCell;
    private Tower currentTower;

    public void Init(ClickableCell cell, Tower tower = null)
    {
        parentCell = cell;
        currentTower = tower;

        if (towerButtons != null && towerButtons.Length > 0)
        {
            for (int i = 0; i < towerButtons.Length; i++)
            {
                int index = i;
                if (towerButtons[i])
                {
                    towerButtons[i].onClick.AddListener(() =>
                    {
                        if (BattleManager.Instance.TryPurchaseTower(cell, index))
                            Close();
                    });
                }
            }
        }

        if (upgradeButton)
        {
            upgradeButton.gameObject.SetActive(tower != null);
            upgradeButton.onClick.AddListener(() =>
            {
                tower?.Upgrade();
                BattleManager.Instance.UpdateUI();
                Close();
            });
        }

        if (sellButton)
        {
            sellButton.gameObject.SetActive(tower != null);
            sellButton.onClick.AddListener(() =>
            {
                if (tower != null)
                {
                    // 简单出售价：按基础伤害取整
                    BattleManager.Instance.gold += Mathf.RoundToInt(tower.baseDamage);
                    Destroy(tower.gameObject);
                    cell.hasTower = false;
                    BattleManager.Instance.UpdateUI();
                }
                Close();
            });
        }

        if (closeButton) closeButton.onClick.AddListener(Close);

        transform.localScale = Vector3.zero;
        transform.DOScale(Vector3.one, 0.25f).SetEase(Ease.OutBack);
    }

    public void Close()
    {
        transform.DOScale(Vector3.zero, 0.25f)
            .SetEase(Ease.InBack)
            .OnComplete(() => Destroy(gameObject));
    }
}

using UnityEngine;
using TMPro;

public class UIManager : MonoBehaviour
{
    public TMP_Text hpText;
    public TMP_Text goldText;
    public TMP_Text waveText;
    public TMP_Text enemyText;

    public void UpdateAll(int hp, int gold, int wave, int enemies)
    {
        string waveStr = BattleManager.Instance.endlessMode ? "¡Þ" : wave.ToString();
        if (hpText) hpText.text = $"HP: {hp}";
        if (goldText) goldText.text = $"Gold: {gold}";
        if (waveText) waveText.text = $"Wave: {waveStr}";
        if (enemyText) enemyText.text = $"Enemies: {enemies}";
    }
}

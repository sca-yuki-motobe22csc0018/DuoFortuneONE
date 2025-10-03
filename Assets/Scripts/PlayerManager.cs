using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class PlayerManager : MonoBehaviour
{
    [Header("Mana Settings")]
    public int maxMana = 2;
    public int currentMana;

    [Header("UI")]
    public TMP_Text energyText;

    [Header("References")]
    public HandManager handManager;   // 手札管理
    public LifeManager lifeManager;   // ライフ管理

    void Start()
    {
        currentMana = maxMana;
        UpdateEnergyUI();
    }

    /// <summary>
    /// マナを消費
    /// </summary>
    public bool SpendMana(int amount)
    {
        if (currentMana >= amount)
        {
            currentMana -= amount;
            UpdateEnergyUI();
            return true;
        }
        Debug.Log("マナが足りません！");
        return false;
    }

    /// <summary>
    /// マナを増やす（最大値は maxMana まで）
    /// </summary>
    public void GainMana(int amount)
    {
        currentMana = Mathf.Min(currentMana + amount, maxMana);
        UpdateEnergyUI();
    }

    /// <summary>
    /// マナを最大まで回復
    /// </summary>
    public void ResetMana()
    {
        currentMana = maxMana;
        UpdateEnergyUI();
    }

    /// <summary>
    /// 最大マナを増やす
    /// </summary>
    public void IncreaseMaxMana(int amount)
    {
        maxMana += amount;
        ResetMana();
    }

    public void IncreaseMaxManaOnly(int amount)
    {
        maxMana += amount;
        // 現在値は変えない（＝支払い後の値を保持する）
        currentMana = Mathf.Min(currentMana, maxMana);
        UpdateEnergyUI();
    }

    /// <summary>
    /// 初期手札を引く
    /// </summary>
    public void DrawInitialHand(DeckManager deckManager, int count)
    {
        if (deckManager == null || handManager == null) return;

        for (int i = 0; i < count; i++)
        {
            deckManager.DrawCardToHand(this);
        }
    }

    /// <summary>
    /// UI の更新
    /// </summary>
    public void UpdateEnergyUI()
    {
        Debug.Log("ManaUpdate");
        if (energyText != null)
        {
            energyText.text = $"{currentMana}/{maxMana}";
        }
    }
}

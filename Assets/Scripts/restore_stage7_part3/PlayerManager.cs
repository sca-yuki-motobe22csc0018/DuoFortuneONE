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
    public HandManager handManager;       // 手札管理
    public LifeManager lifeManager;       // ライフ管理
    public DiscardManager discardManager; // ★ 捨て札管理を追加

    void Start()
    {
        currentMana = maxMana;
        UpdateEnergyUI();
    }

    // ====== マナ関連 ======

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

    public void GainMana(int amount)
    {
        currentMana = Mathf.Min(currentMana + amount, maxMana);
        UpdateEnergyUI();
    }

    public void ResetMana()
    {
        currentMana = maxMana;
        UpdateEnergyUI();
    }

    public void IncreaseMaxMana(int amount)
    {
        maxMana += amount;
        ResetMana();
    }

    public void IncreaseMaxManaOnly(int amount)
    {
        maxMana += amount;
        currentMana = Mathf.Min(currentMana, maxMana);
        UpdateEnergyUI();
    }

    // ====== 手札関連 ======

    public void DrawInitialHand(DeckManager deckManager, int count)
    {
        if (deckManager == null || handManager == null) return;

        for (int i = 0; i < count; i++)
        {
            deckManager.DrawCardToHand(this);
        }
    }

    // ====== UI更新 ======

    public void UpdateEnergyUI()
    {
        if (energyText != null)
        {
            energyText.text = $"{currentMana}/{maxMana}";
        }
    }
}

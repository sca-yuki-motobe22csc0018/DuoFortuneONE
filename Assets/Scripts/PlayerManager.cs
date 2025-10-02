using UnityEngine;
using TMPro;

public class PlayerManager : MonoBehaviour
{
    [Header("Mana Settings")]
    public int maxMana = 2;
    public int currentMana;

    [Header("UI")]
    public TMP_Text energyText;

    [Header("References")]
    public HandManager handManager;
    public LifeManager lifeManager;

    void Start()
    {
        currentMana = maxMana;
        UpdateEnergyUI();
    }

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

    public void DrawInitialHand(DeckManager deckManager, int count)
    {
        if (deckManager == null || handManager == null) return;

        for (int i = 0; i < count; i++)
        {
            deckManager.DrawCardToHand(this);
        }
    }

    public void UpdateEnergyUI()
    {
        if (energyText != null)
        {
            energyText.text = $"{currentMana}/{maxMana}";
        }
    }
}

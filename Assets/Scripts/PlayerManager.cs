using UnityEngine;
using UnityEngine.UI;

public class PlayerManager : MonoBehaviour
{
    [Header("Mana Settings")]
    public int maxMana = 2;
    public int currentMana;

    [Header("UI")]
    public Text energyText;

    [Header("References")]
    public HandManager handManager;   // ��D�Ǘ�
    public LifeManager lifeManager;   // ���C�t�Ǘ�

    void Start()
    {
        currentMana = maxMana;
        UpdateEnergyUI();
    }

    /// <summary>
    /// �}�i������
    /// </summary>
    public bool SpendMana(int amount)
    {
        if (currentMana >= amount)
        {
            currentMana -= amount;
            UpdateEnergyUI();
            return true;
        }
        Debug.Log("�}�i������܂���I");
        return false;
    }

    /// <summary>
    /// �}�i�𑝂₷�i�ő�l�� maxMana �܂Łj
    /// </summary>
    public void GainMana(int amount)
    {
        currentMana = Mathf.Min(currentMana + amount, maxMana);
        UpdateEnergyUI();
    }

    /// <summary>
    /// �}�i���ő�܂ŉ�
    /// </summary>
    public void ResetMana()
    {
        currentMana = maxMana;
        UpdateEnergyUI();
    }

    /// <summary>
    /// �ő�}�i�𑝂₷
    /// </summary>
    public void IncreaseMaxMana(int amount)
    {
        maxMana += amount;
        ResetMana();
    }

    public void IncreaseMaxManaOnly(int amount)
    {
        maxMana += amount;
        // ���ݒl�͕ς��Ȃ��i���x������̒l��ێ�����j
        currentMana = Mathf.Min(currentMana, maxMana);
        UpdateEnergyUI();
    }

    /// <summary>
    /// ������D������
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
    /// UI �̍X�V
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

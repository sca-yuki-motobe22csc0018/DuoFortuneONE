using UnityEngine;
using TMPro;
using Fusion;

public class PlayerManager : NetworkBehaviour
{
    [Header("Mana Settings")]
    public int maxMana = 2;

    [Networked] public int currentMana { get; set; }

    [Header("UI (Prefab 内)")]
    public TMP_Text energyText;          // 自分のマナ
    public TMP_Text opponentEnergyText;  // 相手のマナ

    [Header("Managers (Prefab 内)")]
    public HandManager handManager;
    public LifeManager lifeManager;

    // GameManager・相手プレイヤー参照用
    private GameManager gameManager;
    private PlayerManager opponent;

    // ================================
    //  Spawn（Prefabが参加した時） 
    // ================================
    public override void Spawned()
    {
        gameManager = FindAnyObjectByType<GameManager>();

        // GameManagerに登録（プレイヤーリスト追加）
        gameManager.RegisterPlayer(this);

        // 自分だけUIを表示（相手のCanvasは隠す）
        if (energyText != null)
            energyText.transform.root.gameObject.SetActive(Object.HasInputAuthority);

        // マナ初期値
        currentMana = maxMana;
        UpdateEnergyUI();
    }

    // ================================
    //  マナ処理（Networked）
    // ================================

    public bool SpendMana(int amount)
    {
        if (currentMana >= amount)
        {
            currentMana -= amount;
            UpdateEnergyUI();
            UpdateOpponentUI();
            return true;
        }
        Debug.Log("マナが足りません！");
        return false;
    }

    public void GainMana(int amount)
    {
        currentMana = Mathf.Min(currentMana + amount, maxMana);
        UpdateEnergyUI();
        UpdateOpponentUI();
    }

    public void ResetMana()
    {
        currentMana = maxMana;
        UpdateEnergyUI();
        UpdateOpponentUI();
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
        UpdateOpponentUI();
    }

    // ================================
    //  UI 更新
    // ================================

    public void UpdateEnergyUI()
    {
        if (energyText != null)
            energyText.text = $"{currentMana}/{maxMana}";
    }

    public void UpdateOpponentUI()
    {
        if (opponent != null && opponentEnergyText != null)
            opponentEnergyText.text = $"{opponent.currentMana}/{opponent.maxMana}";
    }

    // ================================
    //  相手プレイヤーの参照セット
    // ================================
    public void SetOpponent(PlayerManager pm)
    {
        opponent = pm;
        UpdateOpponentUI();
    }
}

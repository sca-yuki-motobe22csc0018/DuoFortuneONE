using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BattleManager : MonoBehaviour
{
    public static BattleManager Instance;

    public EffectProcessWindow processWindow; // 進行ウィンドウ表示用

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    /// <summary>
    /// 攻撃処理本体
    /// </summary>
    public IEnumerator HandleAttack(PlayerManager attacker, PlayerManager defender, CardGenerator.CardData attackCard)
    {
        if (attacker == null || defender == null || attackCard == null)
        {
            Debug.LogError("HandleAttack: 引数がnullです。");
            yield break;
        }

        if (processWindow != null)
            processWindow.ShowMessage($"{attacker.name} の攻撃！ {attackCard.name}");

        yield return new WaitForSeconds(0.6f);

        // --- ブロック判定 ---
        if (CheckBlockOpportunity(defender))
        {
            if (processWindow != null)
                processWindow.ShowMessage("BLOCKカードが発動！ 攻撃は無効化されました！");
            yield return new WaitForSeconds(0.6f);
            yield break; // 攻撃終了
        }

        // --- 攻撃成功 → ライフ破壊 ---
        CardGenerator.CardData destroyedCard = null;
        if (defender.lifeManager != null)
        {
            destroyedCard = defender.lifeManager.RemoveLife();
            if (processWindow != null)
                processWindow.ShowMessage($"{defender.name} のライフが1枚破壊された！");
        }

        yield return new WaitForSeconds(0.6f);

        // --- DEFENCE チェック ---
        if (destroyedCard != null)
        {
            if (processWindow != null)
                processWindow.ShowMessage("DEFENCE発動可能なカードを確認中…");

            yield return DefenceWindow.Instance.ShowDefenceChoice(defender, destroyedCard);

            yield return new WaitForSeconds(0.6f);
        }

        // --- 攻撃側の残り効果（追加処理など）---
        if (processWindow != null)
            processWindow.ShowMessage("攻撃処理が完了しました。");

        yield return new WaitForSeconds(0.5f);
    }

    /// <summary>
    /// BLOCKを持つカードが手札にあるか確認
    /// </summary>
    private bool CheckBlockOpportunity(PlayerManager defender)
    {
        if (defender == null || defender.handManager == null)
            return false;

        // 手札の Transform 配下にあるすべてのカードを走査
        foreach (Transform cardTransform in defender.handManager.transform)
        {
            CardGenerator cg = cardTransform.GetComponent<CardGenerator>();
            if (cg == null) continue;

            // ★ あなたのCardGeneratorでは「myData」がカード情報を持つ
            var data = cg.cardData;
            if (data != null && data.type == "B")
            {
                Debug.Log($"BLOCKカード検出: {data.name}");
                return true;
            }
        }

        return false;
    }
}

using Mirror;
using UnityEngine;

/// <summary>
/// 攻撃や効果の進行メッセージをネットワークで全プレイヤーに同期表示するクラス。
/// Host（サーバー）が進行メッセージを送信し、
/// クライアント側では EffectProcessWindow で同じメッセージを表示する。
/// </summary>
public class NetEffectFeed : NetworkBehaviour
{
    // ★ シングルトンとして他クラスから参照できるようにする
    public static NetEffectFeed Instance;

    private void Awake()
    {
        // すでに存在する場合は重複破棄
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    /// <summary>
    /// サーバー → 全クライアントへ同時送信（進行メッセージ表示）
    /// </summary>
    /// <param name="text">表示したいメッセージ</param>
    [ClientRpc]
    public void RpcShowStep(string text)
    {
        // ★ 全クライアントで同時に進行メッセージを表示
        if (EffectProcessWindow.Instance != null)
        {
            // Nextボタン待機などは EffectProcessWindow 側で制御
            _ = EffectProcessWindow.Instance.ShowProcess(text);
        }
        else
        {
            Debug.LogWarning($"[NetEffectFeed] EffectProcessWindow がシーン上に見つかりません。メッセージ: {text}");
        }
    }

    /// <summary>
    /// サーバー → 特定のクライアントだけに送る（例：BlockやDefenceの選択UIを開く）
    /// </summary>
    /// <param name="conn">対象プレイヤーの接続情報</param>
    /// <param name="kind">開くUIの種類（"Block"や"Defence"など）</param>
    [TargetRpc]
    public void TargetOpenUi(NetworkConnectionToClient conn, string kind)
    {
        // ★ kindの種類に応じてウィンドウを開く
        // 今の段階ではまだ使わないが、今後BlockWindowやDefenceWindowを
        // ネットワーク経由で開く時に利用予定。
        switch (kind)
        {
            case "Block":
                var blockWindow = FindAnyObjectByType<BlockWindow>();
                if (blockWindow != null)
                    blockWindow.OpenWindow();
                break;

            case "Defence":
                var defenceWindow = FindAnyObjectByType<DefenceWindow>();
                if (defenceWindow != null)
                    defenceWindow.OpenWindow();
                break;

            default:
                Debug.Log($"[NetEffectFeed] 未対応のUI要求: {kind}");
                break;
        }
    }
}

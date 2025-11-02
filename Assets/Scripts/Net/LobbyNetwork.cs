using Fusion;
using UnityEngine;

public class LobbyNetwork : NetworkBehaviour
{
    public static LobbyNetwork Instance;

    private void Awake()
    {
        Instance = this;
    }

    [Rpc(sources: RpcSources.All, targets: RpcTargets.All)]
    public void RPC_SendHostName(string name)
    {
        if (LobbyManager.Instance != null)
            LobbyManager.Instance.OnReceiveHostName(name);
    }

    [Rpc(sources: RpcSources.All, targets: RpcTargets.All)]
    public void RPC_SendClientName(string name)
    {
        if (LobbyManager.Instance != null)
            LobbyManager.Instance.OnReceiveClientName(name);
    }

    // 🟢 追加：Clientの準備状態を全員に通知
    [Rpc(sources: RpcSources.All, targets: RpcTargets.All)]
    public void RPC_SetClientReady(bool ready)
    {
        if (LobbyManager.Instance != null)
            LobbyManager.Instance.OnClientReadyChanged(ready);
    }
    [Rpc(sources: RpcSources.All, targets: RpcTargets.All)]
    public void RPC_SetTurnOrderText(string text)
    {
        if (LobbyManager.Instance != null)
            LobbyManager.Instance.OnReceiveTurnOrderText(text);
    }
}

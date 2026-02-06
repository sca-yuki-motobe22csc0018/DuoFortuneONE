using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RetireMenuUI : MonoBehaviour
{
    [Header("Open Button (Option)")]
    public Button openButton;

    [Header("Panel")]
    public GameObject panel;
    public Button retireButton;
    public Button backButton;

    [Header("Local Fallback GameOver UI (通信が死んでる時用)")]
    public GameObject gameOverPanel;
    public TMP_Text gameOverResultText;
    public TMP_Text gameOverReasonText;

    private void Start()
    {
        if (panel != null) panel.SetActive(false);

        if (openButton != null)
        {
            openButton.onClick.RemoveAllListeners();
            openButton.onClick.AddListener(Open);
        }

        if (backButton != null)
        {
            backButton.onClick.RemoveAllListeners();
            backButton.onClick.AddListener(Close);
        }

        if (retireButton != null)
        {
            retireButton.onClick.RemoveAllListeners();
            retireButton.onClick.AddListener(OnRetireClicked);
        }
    }

    private void Open()
    {
        if (panel != null) panel.SetActive(true);
    }

    private void Close()
    {
        if (panel != null) panel.SetActive(false);
    }

    private void OnRetireClicked()
    {
        Close();

        var gm = GameManager.Instance ?? FindAnyObjectByType<GameManager>();

        // 通信が生きてて GameManager がいるなら同期リタイア（勝敗を両者に反映）
        if (gm != null)
        {
            gm.RequestRetire();

            // RequestRetire は通信が死んでると何もしないので、その場合はローカル敗北表示へ
            // （runnerの状態はgm側で見てないので、ここは単純に「出しちゃう」でもOK）
        }

        // ★通信死んでる/GM消えてる等でも、必ずローカルで敗北表示できるようにする
        ShowLocalRetireLose();
    }

    private void ShowLocalRetireLose()
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(true);

        if (gameOverResultText != null) gameOverResultText.text = "LOSE";
        if (gameOverReasonText != null) gameOverReasonText.text = "リタイアした";
    }
}

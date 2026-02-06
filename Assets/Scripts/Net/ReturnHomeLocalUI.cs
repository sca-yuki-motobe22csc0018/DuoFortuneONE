using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Fusion;

public class ReturnHomeLocalUI : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private string homeSceneName = "Title";

    public void Setup(Button btn, string sceneName)
    {
        button = btn;
        homeSceneName = sceneName;
        Bind();
    }

    private void Awake()
    {
        if (button == null) button = GetComponent<Button>();
        Bind();
    }

    private void Bind()
    {
        if (button == null) return;

        // ★Missing を含む古い listener を全部消して、必ずローカルで押せるようにする
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => StartCoroutine(Co_ReturnHomeLocal()));
    }

    private IEnumerator Co_ReturnHomeLocal()
    {
        var r = FindAnyObjectByType<NetworkRunner>();
        if (r != null && r.IsRunning)
        {
            r.Shutdown();
            yield return new WaitForSeconds(0.2f);
        }

        SceneManager.LoadScene(homeSceneName);
    }
}

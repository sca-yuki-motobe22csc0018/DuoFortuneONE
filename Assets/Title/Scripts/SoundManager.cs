using UnityEngine;

public class SoundManager : MonoBehaviour
{
    [Header("BGM")]
    public AudioSource bgmSource;
    public AudioClip bgmClip;

    [Header("SE")]
    public AudioSource seSource;
    public AudioClip se1; // センター
    public AudioClip se2; // ライト
    public AudioClip se3; // 通常クリック
    public AudioClip se4; // 起動時クリック

    const string BOOT_SE_KEY = "HasPlayedBootSE";

    void Start()
    {
        PlayBGM();
    }

    // --------------------
    // BGM
    // --------------------
    void PlayBGM()
    {
        if (bgmSource && bgmClip)
        {
            bgmSource.clip = bgmClip;
            bgmSource.loop = true;
            bgmSource.Play();
        }
    }

    // --------------------
    // SE
    // --------------------
    public void PlaySE1() => PlaySE(se1);
    public void PlaySE2() => PlaySE(se2);
    public void PlaySE3() => PlaySE(se3);

    // ★ タイトルクリック用（起動判定つき）
    public void PlayTitleClickSE()
    {
        if (!PlayerPrefs.HasKey(BOOT_SE_KEY))
        {
            PlaySE(se4);
            PlayerPrefs.SetInt(BOOT_SE_KEY, 1);
            PlayerPrefs.Save();
        }
        else
        {
            PlaySE(se3);
        }
    }

    void PlaySE(AudioClip clip)
    {
        if (seSource && clip)
            seSource.PlayOneShot(clip);
    }

    // ==================================================
    // ★ ゲーム終了時に起動SEフラグをリセット
    // ==================================================
    void OnApplicationQuit()
    {
        PlayerPrefs.DeleteKey(BOOT_SE_KEY);
        PlayerPrefs.Save();
    }
}

using UnityEngine;
using UnityEngine.UI;

public class FailPanel : BasePanel
{
    public Button backButton;
    private PlayerStats playerStats;

    private void Start()
    {
        backButton.onClick.AddListener(BackClick);
        playerStats = GameObject.FindGameObjectWithTag("Player").GetComponent<PlayerStats>();
        playerStats.CurrentHealth = playerStats.GetFinalMaxHealthValue();
    }

    private void BackClick()
    {
        SceneMgr.Instance.LoadSceneAsync("StartScene", () =>
        {
            MaskSystemManager.Instance.IsMaskSelected = false;
            
            UIManager.Instance.closePanel<FailPanel>();
            UIManager.Instance.closePanel<PlayerProPanel>();
        });
    }

    private void OnDestroy()
    {
        backButton.onClick.RemoveAllListeners();
        playerStats = null;
    }
    
}

using System.Globalization;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerProPanel : BasePanel
{
    public TMP_Text damaageText;
    public TMP_Text fangyuText;
    public TMP_Text moveSpeedText;
    public TMP_Text shootRateText;
    public TMP_Text currentWaveText;
    public Image healthImage;
    public Button changeShootBtn;
    private PlayerStats playerStats;
    public float currentMaxHealth;
    private bool isInitPlayerHealth;  // 检查是否初始化了玩家血量

    public Button openEscPanelBtn;
    public Joystick moveJoystick;
    public Joystick rotateJoystick;
    [HideInInspector] public bool isNormalShoot = true;  // 是否是普通射击状态
    [Tooltip("拖拽手机端高度调节的UI Slider（Bar）")]
    public Slider cameraHeightSlider;

    private new void Awake()
    {
        playerStats = SceneObjectManager.Instance.GetObjectByTag<PlayerStats>("Player");
    }
    
    /*// 给按钮绑定按下/抬起/离开事件
    private void AddButtonEventListeners()
    {
        // 射击按钮事件
        EventTrigger shootTrigger = shootBtn.gameObject.GetComponent<EventTrigger>();
        if (shootTrigger == null) shootTrigger = shootBtn.gameObject.AddComponent<EventTrigger>();
        // 按下事件
        AddEventTrigger(shootTrigger, EventTriggerType.PointerDown, (data) => { isPressingShoot = true; });
        // 抬起事件
        AddEventTrigger(shootTrigger, EventTriggerType.PointerUp, (data) => { isPressingShoot = false; });
        // 离开事件
        AddEventTrigger(shootTrigger, EventTriggerType.PointerExit, (data) => { isPressingShoot = false; });

        // 散射按钮事件
        EventTrigger burstTrigger = burstShootBtn.gameObject.GetComponent<EventTrigger>();
        if (burstTrigger == null) burstTrigger = burstShootBtn.gameObject.AddComponent<EventTrigger>();
        AddEventTrigger(burstTrigger, EventTriggerType.PointerDown, (data) => { isPressBurst = true; });
        AddEventTrigger(burstTrigger, EventTriggerType.PointerUp, (data) => { isPressBurst = false; });
        AddEventTrigger(burstTrigger, EventTriggerType.PointerExit, (data) => { isPressBurst = false; });
    }

    /// <summary>
    /// 封装EventTrigger添加逻辑（通用方法）
    /// </summary>
    private void AddEventTrigger(EventTrigger trigger, EventTriggerType type, Action<BaseEventData> action)
    {
        EventTrigger.Entry entry = new EventTrigger.Entry();
        entry.eventID = type;
        entry.callback.AddListener((data) => action.Invoke(data));
        trigger.triggers.Add(entry);
    }*/

    private void EscButtonClick()
    {
        SoundAudioPool.Instance.PlaySound(SoundAudioPool.Instance.showPanelClip, transform.position);
        UIManager.Instance.openPanel<EscPanel>(escPanel =>
        {
            escPanel.transform.localPosition = new Vector3(0, 750, 0);
            escPanel.transform.DOLocalMove(Vector3.zero, 0.5f);
        });
    }

    private void OnEnable()
    {
        // 确保playerStats不为null
        if (playerStats == null)
        {
            playerStats = SceneObjectManager.Instance.GetObjectByTag<PlayerStats>("Player");
        }
        changeShootBtn.onClick.AddListener(ChangeButtonClick);
        openEscPanelBtn.onClick.AddListener(EscButtonClick);
        // 直接给按钮绑定按下/抬起事件（无需依赖接口挂载对象）
        //AddButtonEventListeners();
        EventCenter.Instance.AddEventListener<PlayerStats>(GameEvent.玩家面板属性变化,UpdateData);
    }

    private void ChangeButtonClick()
    {
        TMP_Text text = changeShootBtn.GetComponentInChildren<TMP_Text>();
        if (text.text == "散射")
        {
            isNormalShoot = false;
            text.text = "单射";
        }
        else
        {
            isNormalShoot = true;
            text.text = "散射";
        }
    }

    private void Update()
    {
        if (playerStats == null || healthImage == null) return;
        if (!isInitPlayerHealth)
        {
            currentMaxHealth = playerStats.GetFinalMaxHealthValue();
            playerStats.CurrentHealth = currentMaxHealth;
            damaageText.text = playerStats.attackDamage.ToString(CultureInfo.InvariantCulture);
            fangyuText.text = playerStats.GetFinalMaxHealthValue().ToString(CultureInfo.InvariantCulture);
            moveSpeedText.text = playerStats.moveSpeed.ToString(CultureInfo.InvariantCulture);
            shootRateText.text = playerStats.normalShootRate.ToString(CultureInfo.InvariantCulture);
            isInitPlayerHealth = true;
        }
        float targetHealthPer = Mathf.Clamp01(playerStats.CurrentHealth / playerStats.GetFinalMaxHealthValue());
        // 用Mathf.MoveTowards实现平滑变化（Time.deltaTime保证帧率无关）
        healthImage.fillAmount = Mathf.MoveTowards(
            healthImage.fillAmount,  // 当前填充值
            targetHealthPer,         // 目标填充值（当前血量百分比）
            Time.deltaTime * 0.5f    // 变化速度（0.5是可调系数，值越大血条变化越快）
        );
    }

    private void UpdateData(PlayerStats playerStats)
    {
        if (playerStats == null) return;
        // 确保UI组件不为null
        fangyuText.text = playerStats.GetFinalMaxHealthValue().ToString(CultureInfo.InvariantCulture);
        damaageText.text = playerStats.GetFinalAttackDamage().ToString(CultureInfo.InvariantCulture);
        moveSpeedText.text = playerStats.GetFinalMoveSpeed().ToString(CultureInfo.InvariantCulture);
        shootRateText.text = playerStats.GetFinalShootRate().ToString(CultureInfo.InvariantCulture);
    }

    private void OnDisable()
    {
        EventCenter.Instance.RemoveEventListener<PlayerStats>(GameEvent.玩家面板属性变化,UpdateData);
        if (openEscPanelBtn != null) openEscPanelBtn.onClick.RemoveAllListeners();
        changeShootBtn.onClick.RemoveAllListeners();
    }
    
}
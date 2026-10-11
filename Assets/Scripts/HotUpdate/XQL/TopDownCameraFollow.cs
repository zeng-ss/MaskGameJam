using UnityEngine;

/// <summary>
/// 俯视角3D摄像机控制脚本（适配手机端Bar调节高度）
/// 挂载在主摄像机上，功能：跟随玩家+UI Bar调节高度+边界限制+平滑过渡
/// </summary>
[RequireComponent(typeof(Camera))]
public class TopDownCameraFollow : MonoBehaviour
{
    [Header("【核心跟随配置】")]
    [Tooltip("拖拽场景中的Player根节点（必须赋值）")]
    public Transform playerTarget;
    [Tooltip("摄像机与玩家的X/Z轴偏移（默认0,0 即始终在玩家正上方）")]
    public Vector3 xzOffset = new (0, 0, 0);
    [Tooltip("摄像机跟随平滑系数（值越大越流畅，建议0.15-0.3）")]
    public float followSmoothSpeed = 0.2f;

    [Header("【高度调节配置（手机端核心）】")]
    [Tooltip("摄像机最低高度（避免穿模，建议3-5）")]
    public float minCameraHeight = 4f;
    [Tooltip("摄像机最高高度（避免丢失视野，建议8-15）")]
    public float maxCameraHeight = 10f;
    [Tooltip("是否保留PC端滚轮调节（仅编辑器/PC生效）")]
    public bool keepScrollWheelForPC = true;
    [Tooltip("PC滚轮灵敏度（仅当keepScrollWheelForPC为true时生效）")]
    public float scrollHeightSensitivity = 0.5f;

    [Header("【边界限制配置】")]
    [Tooltip("是否限制摄像机跟随范围（防止走出地图）")]
    public bool limitFollowArea = true;
    [Tooltip("摄像机X轴跟随范围（左右）")]
    public Vector2 followRangeX = new (-20f, 20f);
    [Tooltip("摄像机Z轴跟随范围（前后）")]
    public Vector2 followRangeZ = new (-20f, 20f);

    // 私有变量
    private float _targetHeight;       // 目标高度（带平滑）
    private Vector3 _targetFollowPos;  // 目标跟随位置
    private bool _isSliderInit;// 滑动条是否初始化完成
    private PlayerProPanel playerProPanel;

    private void Awake()
    {
        // 校验玩家目标
        if (playerTarget == null)
        {
            Debug.LogError("未指定玩家目标！请拖拽Player根节点到playerTarget字段！");
            enabled = false;
            return;
        }
        // 初始化目标高度为当前摄像机高度
        _targetHeight = transform.position.y;
        // 初始化跟随位置
        _targetFollowPos = GetLimitFollowPos();
    }

    /// <summary>
    /// 初始化高度调节滑动条（绑定事件+同步初始值）
    /// </summary>
    private void InitCameraHeightSlider()
    {
        if (playerProPanel.cameraHeightSlider == null)
        {
            Debug.LogWarning("未绑定高度调节Slider！手机端无法调节摄像机高度");
            _isSliderInit = false;
            return;
        }
        // 1. 设置滑动条范围（0~1）
        playerProPanel.cameraHeightSlider.minValue = 0f;
        playerProPanel.cameraHeightSlider.maxValue = 1f;
        // 2. 同步初始值：将当前高度映射到 0~1范围
        playerProPanel.cameraHeightSlider.value = MapHeightToSliderValue(_targetHeight);
        // 3. 绑定滑动条数值变化事件（手机端拖动时触发）
        playerProPanel.cameraHeightSlider.onValueChanged.AddListener(OnSliderValueChanged);
        _isSliderInit = true;
    }

    /// <summary>
    /// 滑动条数值变化回调（手机端核心）
    /// </summary>
    /// <param name="sliderValue">滑动条当前值（0~1）</param>
    private void OnSliderValueChanged(float sliderValue)
    {
        // 将滑动条 0~1映射到 min~max高度
        _targetHeight = Mathf.Lerp(minCameraHeight, maxCameraHeight, sliderValue);
        // 强制限制高度边界（防止异常值）
        _targetHeight = Mathf.Clamp(_targetHeight, minCameraHeight, maxCameraHeight);
    }

    /// <summary>
    /// 高度值转滑动条值（min→0，max→1）
    /// </summary>
    private float MapHeightToSliderValue(float height)
    {
        return Mathf.InverseLerp(minCameraHeight, maxCameraHeight, height);
    }
    
    private void LateUpdate()
    {
        if (playerProPanel == null)
        {
            playerProPanel = UIManager.Instance.getPanel<PlayerProPanel>();
            return;
        }
        // 初始化UI滑动条
        if (!_isSliderInit) InitCameraHeightSlider();
        if (playerTarget == null) return;
        // 1. 处理输入（手机端Slider/PC端滚轮）
        HandleHeightInput();
        // 2. 计算带边界的跟随位置
        _targetFollowPos = GetLimitFollowPos();
        // 3. 平滑更新摄像机位置
        SmoothUpdateCameraPos();
        // 4. 同步滑动条数值（防止高度被代码修改后Slider不同步）
        SyncSliderWithTargetHeight();
    }

    /// <summary>
    /// 处理高度调节输入（手机端Slider已绑定事件，此处仅处理PC滚轮）
    /// </summary>
    private void HandleHeightInput()
    {
        // PC端滚轮调节（仅在编辑器/PC生效）
        if (keepScrollWheelForPC && (Application.isEditor || !Application.isMobilePlatform))
        {
            float scrollValue = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scrollValue) > 0.001f)
            {
                // 更新目标高度
                _targetHeight += scrollValue * scrollHeightSensitivity * 10;
                // 限制高度边界
                _targetHeight = Mathf.Clamp(_targetHeight, minCameraHeight, maxCameraHeight);
            }
        }
    }

    /// <summary>
    /// 同步滑动条数值与目标高度（双向同步）
    /// </summary>
    private void SyncSliderWithTargetHeight()
    {
        if (!_isSliderInit) return;
        // 避免频繁更新：只有差值大于0.01时才同步
        float targetSliderValue = MapHeightToSliderValue(_targetHeight);
        if (Mathf.Abs(playerProPanel.cameraHeightSlider.value - targetSliderValue) > 0.01f)
        {
            playerProPanel.cameraHeightSlider.value = targetSliderValue;
        }
    }

    /// <summary>
    /// 计算带边界的跟随位置（X/Z轴）
    /// </summary>
    private Vector3 GetLimitFollowPos()
    {
        Vector3 followPos = playerTarget.position + xzOffset;
        // 边界限制
        if (limitFollowArea)
        {
            followPos.x = Mathf.Clamp(followPos.x, followRangeX.x, followRangeX.y);
            followPos.z = Mathf.Clamp(followPos.z, followRangeZ.x, followRangeZ.y);
        }
        // 赋值目标高度
        followPos.y = _targetHeight;
        return followPos;
    }

    /// <summary>
    /// 平滑更新摄像机位置
    /// </summary>
    private void SmoothUpdateCameraPos()
    {
        // 平滑插值（乘以60适配帧率）
        Vector3 smoothPos = Vector3.Lerp(transform.position, _targetFollowPos, followSmoothSpeed * Time.deltaTime * 60);
        transform.position = smoothPos;
        // 固定俯视角旋转
        transform.rotation = Quaternion.Euler(90f, 0f, 0f);
    }

    // Gizmos绘制跟随范围（调试用）
    private void OnDrawGizmosSelected()
    {
        if (limitFollowArea && playerTarget != null)
        {
            Gizmos.color = Color.cyan;
            Vector3 center = new Vector3((followRangeX.x + followRangeX.y) / 2, playerTarget.position.y, (followRangeZ.x + followRangeZ.y) / 2);
            Vector3 size = new Vector3(followRangeX.y - followRangeX.x, 0.1f, followRangeZ.y - followRangeZ.x);
            Gizmos.DrawWireCube(center, size);
        }
    }

    // 防止场景切换时事件重复绑定
    private void OnDestroy()
    {
        if (_isSliderInit && playerProPanel.cameraHeightSlider != null)
        {
            playerProPanel.cameraHeightSlider.onValueChanged.RemoveListener(OnSliderValueChanged);
        }
    }
}
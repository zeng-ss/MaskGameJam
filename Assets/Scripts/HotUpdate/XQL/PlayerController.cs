using UnityEngine;

public class PlayerController : MonoBehaviour
{
    #region 可配置参数
    [Header("旋转设置")]
    public float RotateSpeed = 18f;

    [Header("重力设置")]
    public float Gravity = -9.81f;
    private float _yVelocity;

    [Header("动画组件（必须赋值）")]
    public Animator PlayerAnimator;
    [Tooltip("普通射击动画状态名称（")]
    public string normalShootAnimStateName = "Shoot";
    [Tooltip("散射射击动画状态名称")]
    public string burstShootAnimStateName = "ShootBurst";
    [Tooltip("射击动画层索引")]
    public int shootAnimLayerIndex = 1;

    [Header("射击设置")]
    public float ShootLayerWeight = 1f;
    [Tooltip("子弹预制体（ZiDan）")]
    public GameObject BulletPrefab;
    [Tooltip("子弹生成点")]
    public Transform BulletSpawnPoint;
    [Tooltip("散射扇形角度（越大扩散越广，默认15度）")]
    public float BurstSpreadAngle = 15f;

    private Vector3 _currentShootDirection; // 缓存当前射击方向

    [Header("核心组件")]
    public Camera MainCamera;
    public Transform PlayerModleTransform;
    private CharacterController _characterController;

    [Header("数值管理）")]
    public PlayerStats playerStats;

    // 射击冷却私有变量
    private float _lastNormalShootTime;
    private float _lastBurstShootTime;
    #endregion
    
    private Joystick moveJoystick;
    private Joystick rotateJoystick;
    private PlayerProPanel playerProPanel;

    private void Awake()
    {
        // 初始化CharacterController
        _characterController = GetComponent<CharacterController>();
        if (_characterController == null)
        {
            _characterController = gameObject.AddComponent<CharacterController>();
            _characterController.radius = 0.3f;
            _characterController.height = 1.5f;
            _characterController.center = new Vector3(0, 0.75f, 0);
            _characterController.enabled = true;
        }
        // 自动获取主相机
        MainCamera ??= Camera.main;
        // 启用射击层（
        PlayerAnimator.SetLayerWeight(shootAnimLayerIndex, ShootLayerWeight);
        // 初始化子弹生成点
        if (BulletSpawnPoint == null)
        {
            BulletSpawnPoint = PlayerModleTransform;
            Debug.LogWarning("未指定子弹生成点，默认使用PlayerModle位置");
        }
        // 初始化上一次射击时间
        _lastNormalShootTime = -GetNormalShootCoolDown();
        _lastBurstShootTime = -GetBurstShootCoolDown();
        // 校验依赖组件
        if (playerStats == null) Debug.LogWarning("未关联PlayerStats！");
        if (BulletPrefab == null) Debug.LogWarning("未指定BulletPrefab！"); 
    }

    private void Update()
    {
        if (playerProPanel == null)
        {
            playerProPanel = UIManager.Instance.getPanel<PlayerProPanel>();
        }
        UpdatePlayerRotation();
        // 缓存射击方向（用于动画事件发射子弹）
        _currentShootDirection = PlayerModleTransform.forward;
        // 正常状态：移动+普通射击+散射检测
        bool isMoving = UpdatePlayerMovement();
        PlayerAnimator.SetBool("IsMoving", isMoving);
        UpdatePlayerNormalShootInput(); // 普通射击（参照散射逻辑）
        UpdatePlayerBurstShootInput(); // 散射射击（原有正确逻辑）
    }

    #region 基础逻辑：转向+移动+基础动画
    
    private bool UpdatePlayerMovement()
    {
        if (playerProPanel == null) { return false; }
        moveJoystick = playerProPanel.moveJoystick;
        float h = moveJoystick.Horizontal;
        float v = moveJoystick.Vertical;
        Vector3 moveDir = new Vector3(h, 0, v).normalized;
        bool isMoving = moveDir.magnitude > 0.01f;

        // 重力处理
        if (_characterController.isGrounded) _yVelocity = -0.5f;
        else _yVelocity += Gravity * Time.deltaTime;
        
        if (isMoving && playerStats != null)
        {
            Vector3 moveDelta = moveDir * (playerStats.GetFinalMoveSpeed() * Time.deltaTime);
            moveDelta.y = _yVelocity * Time.deltaTime;
            _characterController.Move(moveDelta);
        }
        return isMoving;
    }

    private void UpdatePlayerRotation()
    {
        if (playerProPanel == null) { return; }
        rotateJoystick = playerProPanel.rotateJoystick;
        float h = rotateJoystick.Horizontal;
        float v = rotateJoystick.Vertical;
        // 判断摇杆是否有有效输入，无输入时直接返回，不执行旋转逻辑（避免零向量）
        if (Mathf.Abs(h) < 0.01f && Mathf.Abs(v) < 0.01f) { return; }
        // 先创建向量，再判断是否为零向量（双重兜底）
        Vector3 moveDirection = new Vector3(h, 0, v).normalized;
        PlayerModleTransform.rotation = Quaternion.Lerp(
            PlayerModleTransform.rotation,
            Quaternion.LookRotation(moveDirection),
            RotateSpeed * Time.deltaTime
        );
    }
    
    #endregion

    #region 攻速逻辑：实时计算射击冷却时间（与原有逻辑一致）
    private float GetNormalShootCoolDown()
    {
        if (playerStats == null) return 1f;
        float finalShootPerSecond = playerStats.GetFinalShootRate() * playerStats.shootRateMultiplier;
        float coolDown = 1f / finalShootPerSecond;
        return Mathf.Max(coolDown, 0.05f);
    }

    private float GetBurstShootCoolDown()
    {
        if (playerStats == null) return 2f;
        float finalBurstPerSecond = playerStats.burstShootRate * playerStats.shootRateMultiplier;
        float coolDown = 1f / finalBurstPerSecond;
        return Mathf.Max(coolDown, 0.5f);
    }
    #endregion

    #region 普通射击：参照散射逻辑（PlayerAnimator.Play()直接播放，可移动）
    private void UpdatePlayerNormalShootInput()
    {
        if (playerProPanel == null) return;
        // 条件判断：长按左键 + 冷却完成 + 组件齐全（与散射逻辑一致）
        bool canShoot = playerProPanel.isNormalShoot
            && Time.time >= _lastNormalShootTime + GetNormalShootCoolDown()
            && BulletPrefab != null
            && playerStats != null;
        if (canShoot)
        {
            // 1. 更新冷却时间（与散射逻辑一致）
            _lastNormalShootTime = Time.time;
            // 2. 直接播放普通射击动画（参照散射的PlayerAnimator.Play()，核心修改）
            // 参数：动画状态名、射击层索引、从动画开头播放（0f），不打断移动动画（上半身叠加）
            PlayerAnimator.Play(normalShootAnimStateName, shootAnimLayerIndex, 0f);
        }
    }

    /// <summary>
    /// 普通射击动画事件回调（与散射一致，动画开火帧触发，同步发射子弹）
    /// </summary>
    public void OnNormalShootAnimEvent()
    {
        if (BulletPrefab != null && playerStats != null && _currentShootDirection != Vector3.zero)
        {
            FireNormalBullet(_currentShootDirection);
        }
    }

    /// <summary>
    /// 普通射击动画结束事件回调（仅预留，无锁定，不影响移动）
    /// </summary>
    public void OnNormalShootEndAnimEvent()
    {
        // 无需额外逻辑，普通射击可随时中断或重复播放
    }

    private void FireNormalBullet(Vector3 shootDirection)
    {
        int bulletCount = playerStats.normalBallisticCount;
        for (int i = 0; i < bulletCount; i++)
        {
            PoolMgr.Instance.GetObjAsync("PlayerBullet", bullet =>
            {
                bullet.transform.position = BulletSpawnPoint.position;
                Bullet bulletScript = bullet.GetComponent<Bullet>();
                if (bulletScript != null)
                {
                    bulletScript.InitBullet(playerStats.normalBulletSpeed, playerStats.bulletMaxRange,
                        playerStats.attackDamage,
                        shootDirection
                    );
                }
            });
        }
    }
    #endregion

    #region 散射射击：原有正确逻辑（保留不变）
    private void UpdatePlayerBurstShootInput()
    {
        if (playerProPanel == null) return;
        bool canBurst = !playerProPanel.isNormalShoot
                        && Time.time >= _lastBurstShootTime + GetBurstShootCoolDown()
                        && BulletPrefab != null
                        && playerStats != null;
        if (canBurst)
        {
            _lastBurstShootTime = Time.time;
            PlayerAnimator.speed = 1f;
            PlayerAnimator.Play(burstShootAnimStateName, shootAnimLayerIndex, 0f);
        }
    }

    public void OnBurstShootAnimEvent()
    {
        if (BulletPrefab != null && playerStats != null && _currentShootDirection != Vector3.zero)
        {
            FireBurstBullet(_currentShootDirection);
        }
    }

    public void OnBurstShootEndAnimEvent()
    {
        
    }

    private void FireBurstBullet(Vector3 shootDirection)
    {
        int baseBulletMin = 5;
        int baseBulletMax = 10;
        int ballisticMultiplier = (int)Mathf.Pow(2, playerStats.burstBallisticCount - 1);
        int bulletCountMin = baseBulletMin * ballisticMultiplier;
        int bulletCountMax = baseBulletMax * ballisticMultiplier;
        int actualBulletCount = Random.Range(bulletCountMin, bulletCountMax + 1);

        float halfSpreadAngle = BurstSpreadAngle / 2f;

        for (int i = 0; i < actualBulletCount; i++)
        {
            float randomAngle = Random.Range(-halfSpreadAngle, halfSpreadAngle);
            Quaternion spreadRotation = Quaternion.Euler(0, randomAngle, 0);
            Vector3 spreadDirection = spreadRotation * shootDirection;

            PoolMgr.Instance.GetObjAsync("PlayerBullet", bullet =>
            {
                bullet.transform.position = BulletSpawnPoint.position;
                Bullet bulletScript = bullet.GetComponent<Bullet>();
                if (bulletScript != null)
                {
                    bulletScript.InitBullet(
                        playerStats.burstBulletSpeed,
                        playerStats.bulletMaxRange,
                        playerStats.attackDamage,
                        spreadDirection
                    );
                }
            });
        }
    }
    #endregion

    #region 通用：获取射击方向（与原有逻辑一致）
    private Vector3 GetShootDirection()
    {
        Ray mouseRay = MainCamera.ScreenPointToRay(Input.mousePosition);
        Plane groundPlane = new Plane(Vector3.up, PlayerModleTransform.position.y);
        if (groundPlane.Raycast(mouseRay, out float distance))
        {
            Vector3 dir = (mouseRay.GetPoint(distance) - PlayerModleTransform.position).normalized;
            dir.y = 0;
            return dir;
        }
        return PlayerModleTransform.forward;
    }
    
    #endregion
}
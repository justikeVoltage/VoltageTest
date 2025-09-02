/****************************************************
    功能：扎带完整流程控制器
    
    【流程概述】
    1. Socket连接触发 → 实例化初始扎带模型
    2. 初始扎带模型连接 → 播放收紧动画 → 激活打孔模型
    3. 拖拽控制：通过拖拽打孔模型控制动画进度和松紧度
    4. 穿孔触发：Trigger触发后播放最终系紧动画完成流程
    
    作者：LZL
    创建日期：#2025/08/01#
    修改人：LZL
    修改日期：#2025/08/03#
    修改内容：
    1. 新增可调节的动画终点百分比 (animationEndPointPercentage)
    2. 新增AutoHand的触觉反馈API调用
    3. 新增松紧度考核系统，包含UI进度条和分级触觉反馈
    4. 优化UI引用，自动从Slider获取Image组件
    修改人：LZL
    修改日期：#2025/08/06#
    修改内容：
    1. 新增扎带抓取放置-动画捆管-抓取穿孔全流程控制
    2. 自动组件查找，减少Inspector引用设置
*****************************************************/
using UnityEngine;
using UnityEngine.UI;
using Autohand;
using Voltage;
using System.Collections;

public class CableTieController : MonoBehaviour
{
    #region ===== 流程预制体设置 =====
    [Header("流程预制体设置")]
    [SerializeField] private GameObject initialGrabbableModel;   // 初始可抓取扎带（流程步骤1）
    [SerializeField] private GameObject TightenTubeAni;          // 收紧动画（流程步骤2）
    [SerializeField] private GameObject punchHoleModel;          // 打孔模型（流程步骤3 - 包含拖拽控制对象和Trigger）
    [SerializeField] private GameObject tieUpAni;                // 系紧动画（流程步骤4 - 同时也是拖拽控制的动画）
    #endregion
    
    #region ===== 拖拽控制设置 =====
    [Header("拖拽控制设置")]
    [SerializeField] private float maxNegativeX = -1f;          // 最大拖拽距离（X轴负方向）
    [Range(0f, 1f)]
    [SerializeField] private float animationEndPointPercentage = 1.0f; // 动画终点百分比
    
    [Header("教学模式设置")]
    [SerializeField] public bool isTeachingMode = true;         // 是否处于教学模式
    
    // 注意：targetGrabbable 自动从punchHoleModel中查找挂载了Grabbable_Voltage组件的物体
    #endregion
    
    #region ===== 系统自动获取变量 =====
    // Socket系统
    private SocketBase socketBase;                               // Socket基础组件（自动获取）
    private Vector3 instanceOffset = new Vector3(-0.06f, 0.06f, 0.06f); // 实例化偏移量
    #endregion

    #region ===== 松紧度考核系统 =====
    [Header("松紧度考核设置")]
    // 注释掉外部引用，改为动态查找
    // [SerializeField] private Slider tightnessProgressBar;        // 松紧度进度条UI
    [Range(0f, 1f)]
    [SerializeField] private float minAcceptableTightness = 0.33f; // 最小可接受松紧度
    [Range(0f, 1f)]
    [SerializeField] private float maxAcceptableTightness = 0.66f; // 最大可接受松紧度
    
    [Header("松紧度颜色设置")]
    [SerializeField] private Color tooLooseColor = Color.yellow;  // 太松时的颜色
    [SerializeField] private Color acceptableColor = Color.green; // 合适时的颜色
    [SerializeField] private Color tooTightColor = Color.red;     // 太紧时的颜色
    #endregion

    #region ===== 触觉反馈系统 =====
    [Header("触觉反馈设置")]
    [SerializeField] private bool useHaptics = true;              // 是否启用触觉反馈
    [SerializeField, Range(0.1f, 3.0f)] private float hapticIntensityMultiplier = 1.0f; // 全局触觉强度调节
    #endregion
    
    #region ===== 触觉反馈参数（私有） =====
    // 基础触觉参数
    private int ratchetSteps = 10;                               // 棘轮步数
    private float onGrabStrength = 0.4f;                         // 抓取时触觉强度
    private float onGrabDuration = 0.05f;                        // 抓取时触觉持续时间
    private float ratchetClickStrength = 0.2f;                   // 棘轮点击触觉强度
    private float ratchetClickDuration = 0.03f;                  // 棘轮点击触觉持续时间
    private float onReleaseStrength = 0.6f;                      // 释放时触觉强度
    private float onReleaseDuration = 0.1f;                      // 释放时触觉持续时间

    // 松紧度触觉参数
    private float tooLooseHapticStrength = 0.1f;                 // 太松时触觉强度
    private float tooLooseHapticDuration = 0.5f;                 // 太松时触觉持续时间
    private float acceptableHapticStrength = 0.3f;               // 合适时触觉强度
    private float acceptableHapticDuration = 0.3f;               // 合适时触觉持续时间
    private float tooTightHapticStrength = 0.8f;                 // 太紧时触觉强度
    private float tooTightHapticDuration = 0.1f;                 // 太紧时触觉持续时间
    #endregion

    #region ===== 拖拽控制私有变量 =====
    private Rigidbody targetRb;                                  // 目标物体的刚体
    private Vector3 initialWorldPosition;                        // 初始世界位置
    private Quaternion initialWorldRotation;                     // 初始世界旋转
    private Vector3 movementAxis_World;                          // 世界坐标系下的运动轴
    private float lastNormalizedTime = 0f;                       // 上次标准化时间
    private int lastRatchetStep = 0;                             // 上次棘轮步骤
    private int currentAnimationStateHash = 0;                   // 当前动画状态哈希（用于拖拽控制）
    
    // 步骤5：GrabControlCube拖拽控制
    private GameObject grabControlCube;                          // 拖拽控制点
    private GameObject tieUpEndCube;                             // 系带末端位置同步点
    private Grabbable_Voltage grabControlGrabbable;             // 拖拽控制组件
    private Slider dynamicTightnessSlider;                       // 动态查找的松紧度滑块
    
    // UI组件
    private Image progressBarFill;                               // 进度条填充图像（自动获取）
    
    // 松紧度状态
    private TightnessLevel currentTightnessLevel = TightnessLevel.TooLoose; // 当前松紧度等级
    private TightnessLevel lastTightnessLevel = TightnessLevel.TooLoose;    // 上次松紧度等级
    private float lastTightnessHapticTime = 0f;                  // 上次松紧度触觉时间
    #endregion
    
    #region ===== 流程控制私有变量 =====
    // 实例化对象跟踪
    private GameObject currentInitialModel;                      // 当前实例化的初始模型（步骤1）
    private GameObject currentTightenAni;                        // 当前实例化的收紧动画（步骤2）
    private GameObject currentPunchHoleModel;                    // 当前实例化的打孔模型（步骤3）
    private GameObject currentTieUpAni;                          // 当前实例化的系紧动画（步骤4 - 也是拖拽控制动画）
    
    // 自动查找的组件
    private GameObject triggerObject;                            // 自动从punchHoleModel中查找的Trigger对象
    private Animator currentStrapAnimator;                       // 当前拖拽控制的动画器
    private Grabbable_Voltage targetGrabbable;                  // 自动从punchHoleModel中查找的可抓取组件
    private Grabbable_Voltage WCNM;
    
    // 事件管理标志
    private bool isSocketEventAdded = false;                     // Socket事件是否已添加
    private bool isInitialModelEventAdded = false;               // 初始模型事件是否已添加
    
    // 流程完成回调事件
    public System.Action OnStep2ForwardCompleted;                       // 步骤2完成回调：TightenTubeAni播放完成后触发
    public System.Action OnStep2BackwardCompleted;                       // 步骤2完成回调：TightenTubeAni播放完成后触发
    public System.Action OnStep4Completed;
    public System.Action OnStep5Completed;                       // 步骤5完成回调：tieUpAni播放完成（lastNormalizedTime=1.0）时触发
    
    /*
    使用示例：
    cableTieController.OnStep2Completed += () => {
        Debug.Log("步骤2完成：收紧动画播放完毕");
        // 在这里添加步骤2完成后的逻辑
    };
    
    cableTieController.OnStep5Completed += () => {
        Debug.Log("步骤5完成：系紧动画播放完毕");
        // 在这里添加步骤5完成后的逻辑
    };
    */
    
    // 流程完成状态跟踪
    private bool isStep2Completed = false;                       // 步骤2完成状态
    private bool isStep5Completed = false;                       // 步骤5完成状态
    #endregion

    #region ===== 松紧度等级枚举 =====
    public enum TightnessLevel
    {
        TooLoose,    // 太松
        Acceptable,  // 合适
        TooTight     // 太紧
    }
    #endregion

    #region ===== 生命周期方法 =====
    /// <summary>
    /// 初始化阶段：查找组件并设置事件监听
    /// </summary>
    private void Awake()
    {
        // 从punchHoleModel预制体中查找目标可抓取对象
        FindTargetGrabbableFromPunchHole();
        
        // 设置目标可抓取对象的事件监听
        if (targetGrabbable != null)
        {
            targetRb = targetGrabbable.GetComponent<Rigidbody>();
            targetGrabbable.onGrab.AddListener(OnGrab);
        }
        else
        {
            Debug.LogWarning("[CableTieController] 未找到targetGrabbable！将在运行时从实例化的punchHoleModel中查找。", this);
        }
    }

    /// <summary>
    /// 启动阶段：初始化各种设置
    /// </summary>
    private void Start()
    {
        // 设置拖拽控制的初始位置和旋转
        if (targetGrabbable != null)
        {
            initialWorldPosition = targetGrabbable.transform.position;
            initialWorldRotation = targetGrabbable.transform.rotation;
            movementAxis_World = -targetGrabbable.transform.forward; // 改为Z轴反方向
        }
        else
        {
            // 备用方案：使用本物体作为参考
            initialWorldPosition = transform.position;
            initialWorldRotation = transform.rotation;
            movementAxis_World = -transform.forward; // 改为Z轴反方向
        }

        // 初始化各个系统
        InitializeStrapAnimator();        // 初始化拖拽控制动画器
        InitializeProgressBar();          // 初始化松紧度进度条
        InitializeSocketEvents();         // 初始化Socket事件监听
        
        // 测试模式：直接实例化初始模型（跳过步骤1的Socket连接等待）
        // TestDirectInstantiateInitialModel();
        
        // 测试模式备用方案：暂时禁用，避免与正常流程冲突
        // StartCoroutine(ForceSetupTriggerForTesting());
    }
    
    /// <summary>
    /// 销毁阶段：清理事件和对象
    /// </summary>
    private void OnDestroy()
    {
        // 清理事件监听
        if (targetGrabbable != null)
        {
            targetGrabbable.onGrab.RemoveListener(OnGrab);
        }
        
        // 清理Socket事件
        CleanupSocketEvents();
        
        // 清理实例化的对象
        // CleanupInstantiatedObjects();
    }

    /// <summary>
    /// 拖拽控制更新：监控目标物体移动并更新动画进度
    /// </summary>
    private void LateUpdate()
    {
        // 检查是否有目标可抓取对象且正在被抓取
        if (targetGrabbable == null || !targetGrabbable.IsHeld()) return;

        // 计算拖拽位移
        Vector3 worldDisplacementVector = targetGrabbable.transform.position - initialWorldPosition;
        float projectedDisplacement = Vector3.Dot(worldDisplacementVector, movementAxis_World);

        // 限制为正值并标准化
        projectedDisplacement = Mathf.Max(0, projectedDisplacement);
        float potentialNormalizedTime = Mathf.Abs(maxNegativeX) > 0 ? projectedDisplacement / Mathf.Abs(maxNegativeX) : 0;
        potentialNormalizedTime = Mathf.Clamp01(potentialNormalizedTime);

        // 🔧 修复：棘轮效果 - 只允许增加，不允许减少（系扎带只紧不松）
        if (potentialNormalizedTime > lastNormalizedTime)
        {
            // 🎓 教学模式限制：达到0.5后不再增加进度
            if (isTeachingMode && potentialNormalizedTime > 0.5f)
            {
                potentialNormalizedTime = 0.5f;
                Debug.Log("[CableTieController]  教学模式：拖拽进度已达到0.5，不再增加");
            }
            
            lastNormalizedTime = potentialNormalizedTime;
            

        }
        
        // 更新拖拽控制动画进度
        if (currentStrapAnimator != null && currentAnimationStateHash != 0)
        {
            // 基于拖拽距离的标准化时间做分段缩放：
            // - 当 n <= 0.5 时：保持原速
            // - 当 n > 0.5 时：后半段进度按 1/3 速率推进，确保在 0.5 处连续
            float normalizedForAnimation = lastNormalizedTime;
            if (normalizedForAnimation > 0.5f)
            {
                normalizedForAnimation = 0.5f + (normalizedForAnimation - 0.5f) / 3f;
            }

            float finalAnimationProgress = normalizedForAnimation * animationEndPointPercentage;
            // 使用正确的状态哈希，而不是硬编码的0
            currentStrapAnimator.Play(currentAnimationStateHash, 0, finalAnimationProgress);
        }

        // 更新各种反馈系统
        UpdateTightnessAssessment();  // 更新松紧度评估
        HandleRatchetHaptics();       // 处理棘轮触觉反馈
        HandleTightnessHaptics();     // 处理松紧度触觉反馈
        
        // 🔧 检测步骤5完成：现在改为在OnRelease时基于松紧度状态触发回调
     
    }
    #endregion

    #region ===== 拖拽事件处理 =====
    /// <summary>
    /// 抓取事件：启动拖拽控制和反馈系统
    /// </summary>
    public void OnGrab(Hand hand, Grabbable grabbable)
    {
        // 🔧 修复：检查这是否是GrabControlCube的首次抓取
        bool isGrabControlCube = grabbable.gameObject == grabControlCube;
        if (isGrabControlCube)
        {
            Debug.Log($"[CableTieController] 🔧 OnGrab: 检测到GrabControlCube被抓取，当前lastNormalizedTime = {lastNormalizedTime}");
        }
        
        // 触觉反馈
        if(useHaptics && hand != null)
            TriggerHapticFeedback(hand, onGrabStrength, onGrabDuration);

        // 重置棘轮步骤
        lastRatchetStep = 0;
        
        // 显示松紧度进度条（带淡入效果）
        Slider currentSlider = GetCurrentSlider();
        if (currentSlider != null && !currentSlider.gameObject.activeSelf)
        {
            // 🔧 新增：停止任何旧淡入协程
            if (fadeInCoroutine != null)
            {
                StopCoroutine(fadeInCoroutine);
                Debug.Log("[CableTieController] 停止了旧淡入协程");
            }
            
            // 🔧 修复：确保Slider值正确显示当前进度
            currentSlider.value = lastNormalizedTime;
            Debug.Log($"[CableTieController] 🔧 OnGrab: 设置Slider.value = {lastNormalizedTime}");
            
            currentSlider.gameObject.SetActive(true);
            fadeInCoroutine = StartCoroutine(FadeInProgressBar());
        }
        else if (currentSlider != null && currentSlider.gameObject.activeSelf)
        {
            // 🔧 修复：如果Slider已经显示，确保值正确
            currentSlider.value = lastNormalizedTime;
            Debug.Log($"[CableTieController] 🔧 OnGrab: Slider已显示，确保value = {lastNormalizedTime}");
        }
    }




    #region ===== 触觉反馈系统 =====
    /// <summary>
    /// AutoHand兼容的触觉反馈方法
    /// </summary>
    private void TriggerHapticFeedback(Hand hand, float strength, float duration)
    {
        if (hand == null) return;
        
        try
        {
            // 应用全局触觉强度倍数
            float adjustedStrength = Mathf.Clamp01(strength * hapticIntensityMultiplier);
            
            // AutoHand触觉反馈调用（参数顺序：duration, strength）
            hand.PlayHapticVibration(duration, adjustedStrength);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"触觉反馈触发失败: {e.Message}");
        }
    }

    /// <summary>
    /// 棘轮触觉反馈：每达到一个步骤触发一次
    /// </summary>
    private void HandleRatchetHaptics()
    {
        if (!useHaptics || ratchetSteps <= 0) return;

        int currentStep = Mathf.FloorToInt(lastNormalizedTime * ratchetSteps);

        if (currentStep > lastRatchetStep && targetGrabbable != null && targetGrabbable.GetHeldBy().Count > 0)
        {
            TriggerHapticFeedback(targetGrabbable.GetHeldBy()[0], ratchetClickStrength, ratchetClickDuration);
            lastRatchetStep = currentStep;
        }
    }
    #endregion

    #region ===== 松紧度考核系统 =====
    /// <summary>
    /// 初始化松紧度进度条UI
    /// </summary>
    private void InitializeProgressBar()
    {
        // 松紧度进度条现在通过动态查找获取，这里不需要初始化
        // 初始化工作会在步骤5中进行
        Debug.Log("[CableTieController] 松紧度进度条将在步骤5中动态初始化");
    }

    private void UpdateTightnessAssessment()
    {
        // 🔧 修复：正确更新进度条值 - 参考TieStrapMovementController的方法
        Slider currentSlider = GetCurrentSlider();
        if (currentSlider != null)
        {
            // 确保Slider的min/max值正确设置
            if (currentSlider.minValue != 0f || currentSlider.maxValue != 1f)
            {
                currentSlider.minValue = 0f;
                currentSlider.maxValue = 1f;
                Debug.Log("[CableTieController] 🔧 Slider范围已修正为 [0, 1]");
            }
            currentSlider.value = lastNormalizedTime;
        }

        // 判断当前松紧度等级
        currentTightnessLevel = GetTightnessLevel(lastNormalizedTime);

        // 更新进度条颜色
        UpdateProgressBarColor();
    }

    private TightnessLevel GetTightnessLevel(float tightness)
    {
        if (tightness < minAcceptableTightness)
            return TightnessLevel.TooLoose;
        else if (tightness <= maxAcceptableTightness)
            return TightnessLevel.Acceptable;
        else
            return TightnessLevel.TooTight;
    }

    private void UpdateProgressBarColor()
    {
        if (progressBarFill == null) return;

        Color targetColor;
        switch (currentTightnessLevel)
        {
            case TightnessLevel.TooLoose:
                targetColor = tooLooseColor;
                break;
            case TightnessLevel.Acceptable:
                targetColor = acceptableColor;
                break;
            case TightnessLevel.TooTight:
                targetColor = tooTightColor;
                break;
            default:
                targetColor = tooLooseColor;
                break;
        }

        // 平滑颜色过渡
        progressBarFill.color = Color.Lerp(progressBarFill.color, targetColor, Time.deltaTime * 5f);
    }

    private void HandleTightnessHaptics()
    {
        if (!useHaptics || targetGrabbable == null || targetGrabbable.GetHeldBy().Count == 0) return;

        bool shouldTriggerHaptic = false;
        float hapticStrength = 0f;
        float hapticDuration = 0f;

        if (currentTightnessLevel != lastTightnessLevel)
        {
            shouldTriggerHaptic = true;
            lastTightnessLevel = currentTightnessLevel;
            lastTightnessHapticTime = Time.time;
        }
        else if (Time.time - lastTightnessHapticTime >= GetHapticInterval())
        {
            shouldTriggerHaptic = true;
            lastTightnessHapticTime = Time.time;
        }

        if (shouldTriggerHaptic)
        {
            switch (currentTightnessLevel)
            {
                case TightnessLevel.TooLoose:
                    hapticStrength = tooLooseHapticStrength;
                    hapticDuration = tooLooseHapticDuration;
                    break;
                case TightnessLevel.Acceptable:
                    hapticStrength = acceptableHapticStrength;
                    hapticDuration = acceptableHapticDuration;
                    break;
                case TightnessLevel.TooTight:
                    hapticStrength = tooTightHapticStrength;
                    hapticDuration = tooTightHapticDuration;
                    break;
            }

            TriggerHapticFeedback(targetGrabbable.GetHeldBy()[0], hapticStrength, hapticDuration);
        }
    }

    private float GetHapticInterval()
    {
        switch (currentTightnessLevel)
        {
            case TightnessLevel.TooLoose:
                return 2.0f;
            case TightnessLevel.Acceptable:
                return 1.0f;
            case TightnessLevel.TooTight:
                return 0.3f;
            default:
                return 1.0f;
        }
    }

    // 淡入动画
    private IEnumerator FadeInProgressBar()
    {
        Slider currentSlider = GetCurrentSlider();
        if (currentSlider != null)
        {
            CanvasGroup canvasGroup = currentSlider.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = currentSlider.gameObject.AddComponent<CanvasGroup>();
            }
            
            // 🔧 强制重置：确保从隐藏状态开始
            currentSlider.gameObject.SetActive(true);
            canvasGroup.alpha = 0f;
            Debug.Log($"[CableTieController] 开始淡入进度条，初始alpha: {canvasGroup.alpha}, active: {currentSlider.gameObject.activeSelf}");
            
            while (canvasGroup.alpha < 1f)
            {
                canvasGroup.alpha += Time.deltaTime * 3f;
                yield return null;
            }
            canvasGroup.alpha = 1f;
            Debug.Log($"[CableTieController] 淡入完成，最终alpha: {canvasGroup.alpha}");
        }
        else
        {
            Debug.LogWarning("[CableTieController] 淡入失败：GetCurrentSlider返回null");
        }
    }

    // 淡出动画
    private IEnumerator FadeOutProgressBar()
    {
        Slider currentSlider = GetCurrentSlider();
        if (currentSlider != null)
        {
            CanvasGroup canvasGroup = currentSlider.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                // 🔧 新增：强制上限，防止alpha >1
                canvasGroup.alpha = Mathf.Min(canvasGroup.alpha, 1f);
                Debug.Log($"[CableTieController] 开始淡出进度条，初始alpha: {canvasGroup.alpha}, active: {currentSlider.gameObject.activeSelf}");
                
                float fadeTimer = 0f;
                const float maxFadeTime = 1f;
                while (canvasGroup.alpha > 0f && fadeTimer < maxFadeTime)
                {
                    canvasGroup.alpha -= Time.deltaTime * 2f;
                    fadeTimer += Time.deltaTime;
                    yield return null;
                }
                canvasGroup.alpha = 0f;
                Debug.Log($"[CableTieController] 淡出完成，最终alpha: {canvasGroup.alpha}, 用时: {fadeTimer}s");
            }
            currentSlider.gameObject.SetActive(false);
        }
        else
        {
            Debug.LogWarning("[CableTieController] 淡出失败：GetCurrentSlider返回null");
        }
    }

    // 公共方法：获取当前松紧度等级（供其他脚本调用）
    public TightnessLevel GetCurrentTightnessLevel()
    {
        return currentTightnessLevel;
    }

    /// <summary>
    /// 公共方法：获取当前松紧度数值（供其他脚本调用）
    /// </summary>
    public float GetCurrentTightness()
    {
        return lastNormalizedTime;
    }
    
    /// <summary>
    /// 公共方法：重置流程完成状态（供其他脚本调用）
    /// </summary>
    public void ResetCompletionStatus()
    {
        isStep2Completed = false;
        isStep5Completed = false;
        Debug.Log("[CableTieController] 🔄 流程完成状态已重置");
    }
    #endregion
    
    #region ===== 【核心流程控制】扎带完整流程管理 =====
    
    /// <summary>
    /// 初始化拖拽控制动画器
    /// </summary>
    private void InitializeStrapAnimator()
    {
        if (tieUpAni != null)
        {
            // 从tieUpAni预制体获取Animator组件
            Animator animator = tieUpAni.GetComponent<Animator>();
            if (animator == null)
            {
                animator = tieUpAni.GetComponentInChildren<Animator>();
            }
            
            if (animator != null)
            {
                // 实例化一个用于拖拽控制的动画对象
                // 使用脚本所挂物体的position和预制体自身的rotation
                currentTieUpAni = Instantiate(tieUpAni, transform.position, transform.rotation);
                
                // 立即失活，防止自动播放动画
                currentTieUpAni.SetActive(false);
                Debug.Log("[CableTieController] tieUpAni已实例化并失活，等待步骤5激活");
                
                currentStrapAnimator = currentTieUpAni.GetComponent<Animator>();
                if (currentStrapAnimator == null)
                {
                    currentStrapAnimator = currentTieUpAni.GetComponentInChildren<Animator>();
                }
                
                if (currentStrapAnimator != null)
                {
                    // 立即停止动画播放，防止默认状态自动播放
                    currentStrapAnimator.enabled = false;
                    currentStrapAnimator.enabled = true;
                    
                    // 获取当前状态信息并保存状态哈希
                    AnimatorStateInfo stateInfo = currentStrapAnimator.GetCurrentAnimatorStateInfo(0);
                    currentAnimationStateHash = stateInfo.shortNameHash;
                    
                    // 设置动画速度为0，完全停止播放
                    currentStrapAnimator.speed = 0;
                    
                    // 播放当前状态并设置到开始位置
                    currentStrapAnimator.Play(currentAnimationStateHash, 0, 0f);
                    
                    // 强制更新Animator状态
                    currentStrapAnimator.Update(0f);
                    
                    Debug.Log($"[CableTieController] 拖拽控制动画器初始化成功，状态哈希: {currentAnimationStateHash}");
                }
                else
                {
                    Debug.LogError("[CableTieController] 无法从tieUpAni中获取Animator组件！");
                }
            }
            else
            {
                Debug.LogError("[CableTieController] tieUpAni预制体中没有Animator组件！");
            }
        }
        else
        {
            Debug.LogError("[CableTieController] tieUpAni未设置！");
        }
    }
    
    /// <summary>
    /// 从punchHoleModel中查找挂载了Grabbable_Voltage组件的目标可抓取对象
    /// </summary>
    private void FindTargetGrabbableFromPunchHole()
    {
        if (punchHoleModel != null)
        {
            // 在punchHoleModel及其所有子物体中查找Grabbable_Voltage组件
            Grabbable_Voltage[] grabbables = punchHoleModel.GetComponentsInChildren<Grabbable_Voltage>();
            if (grabbables.Length > 0)
            {
                targetGrabbable = grabbables[0]; // 取第一个找到的
                Debug.Log($"[CableTieController] 从punchHoleModel预制体中找到目标可抓取对象: {targetGrabbable.name}");
            }
            else
            {
                Debug.LogWarning("[CableTieController] 在punchHoleModel预制体中未找到Grabbable_Voltage组件！");
            }
        }
    }
    
    /// <summary>
    /// 从实例化的punchHoleModel中查找目标可抓取对象（运行时调用）
    /// </summary>
    private void FindTargetGrabbableFromInstantiatedPunchHole()
    {
        if (currentPunchHoleModel != null)
        {
            // 在实例化的punchHoleModel及其所有子物体中查找Grabbable_Voltage组件
            Grabbable_Voltage[] grabbables = currentPunchHoleModel.GetComponentsInChildren<Grabbable_Voltage>();
            if (grabbables.Length > 0)
            {
                // 如果之前没有找到，现在设置事件
                if (targetGrabbable == null)
                {
                    targetGrabbable = grabbables[0];
                    targetRb = targetGrabbable.GetComponent<Rigidbody>();
                    targetGrabbable.onGrab.AddListener(OnGrab);
                    Debug.Log($"[CableTieController] 从实例化的punchHoleModel中找到目标可抓取对象: {targetGrabbable.name}");
                }
                else
                {
                    // 更新引用到实例化的对象
                    targetGrabbable = grabbables[0];
                    targetRb = targetGrabbable.GetComponent<Rigidbody>();
                    Debug.Log($"[CableTieController] 更新目标可抓取对象引用到实例化对象: {targetGrabbable.name}");
                }
            }
            else
            {
                Debug.LogWarning("[CableTieController] 在实例化的punchHoleModel中未找到Grabbable_Voltage组件！");
            }
        }
    }
    
    /// <summary>
    /// 查找用于检测punchHole物体碰撞的Trigger检测器对象
    /// 优先策略：
    /// 1) 在currentPunchHoleModel子物体中查找挂载了CableTieTriggerDetector的对象
    /// 2) 若未找到，查找任意Collider.isTrigger为true的子物体
    /// 3) 若仍未找到，最后回退到名称包含"StartTriggerCube"/"TriggerCube"的对象
    /// </summary>
    private void FindTriggerFromPunchHole()
    {
        triggerObject = null;
        
        WCNM = this.currentPunchHoleModel.transform.Find("Zadai-Mod/AttachmentObjects/AttachmentObject_cp")
            .GetComponent<Grabbable_Voltage>();
        
        if (currentPunchHoleModel == null)
        {
            Debug.LogError("[CableTieController] ❌ currentPunchHoleModel为空，无法查找Trigger对象！");
            return;
        }

        // 1) 优先查找挂了CableTieTriggerDetector组件的子物体
        var detectorComponents = currentPunchHoleModel.GetComponentsInChildren<CableTieTriggerDetector>(true);
        if (detectorComponents != null && detectorComponents.Length > 0)
        {
            triggerObject = detectorComponents[0].gameObject;
            Debug.Log($"[CableTieController] ✅ 优先使用带CableTieTriggerDetector的对象: {triggerObject.name}");
            return;
        }

        // 2) 次优先：查找任意Collider.isTrigger为true的子物体
        var colliders = currentPunchHoleModel.GetComponentsInChildren<Collider>(true);
        if (colliders != null && colliders.Length > 0)
        {
            foreach (var col in colliders)
            {
                if (col != null && col.enabled && col.isTrigger)
                {
                    triggerObject = col.gameObject;
                    Debug.Log($"[CableTieController] ✅ 在子物体中找到isTrigger的Collider: {triggerObject.name}");
                    return;
                }
            }
        }

        // 3) 最后回退：基于名称匹配（兼容旧资源）
        Transform[] allChildren = currentPunchHoleModel.GetComponentsInChildren<Transform>(true);
        foreach (Transform child in allChildren)
        {
            if (child.name.Contains("StartTriggerCube") || child.name.Contains("TriggerCube"))
            {
                triggerObject = child.gameObject;
                Debug.Log($"[CableTieController] ⚠️ 基于名称回退找到Trigger对象: {triggerObject.name}");
                return;
            }
        }

        Debug.LogError("[CableTieController] ❌ 未能在currentPunchHoleModel中找到合适的Trigger对象（无CableTieTriggerDetector、无isTrigger Collider、无名称匹配）。请检查预制体设置。");
    }
    
    /// <summary>
    /// 初始化Socket事件（测试期间已注释）
    /// </summary>
    private void InitializeSocketEvents()
    {
        if (socketBase == null)
        {
            socketBase = GetComponent<SocketBase>();
         
        }
        
        // 🔧 修改：启用Socket事件监听，等待扎带模型连接
        if (socketBase != null && !isSocketEventAdded)
        {
            socketBase.m_enable = true;
            socketBase.m_afterConnection.AddListener(OnSocketConnected);
            isSocketEventAdded = true;
            Debug.Log($"[CableTieController] Socket事件已添加到 {gameObject.name}，等待扎带模型连接");
        }
        else if (socketBase == null)
        {
            Debug.LogError($"[CableTieController] SocketBase组件未找到！请检查 {gameObject.name}");
        }
        else
        {
            Debug.LogWarning($"[CableTieController] Socket事件已经添加过了");
        }
    }
    
    /// <summary>
    /// 清理Socket事件（测试期间已注释）
    /// </summary>
    private void CleanupSocketEvents()
    {
        if (socketBase != null && isSocketEventAdded)
        {
            socketBase.m_afterConnection.RemoveListener(OnSocketConnected);
            isSocketEventAdded = false;
            Debug.Log($"[CableTieController] Socket事件已从 {gameObject.name} 移除");
        }
    }
    
    /// <summary>
    /// 清理所有实例化的对象
    /// </summary>
    private void CleanupInstantiatedObjects()
    {
        if (currentInitialModel != null)
        {
            DestroyImmediate(currentInitialModel);
            currentInitialModel = null;
        }
        
        if (currentTightenAni != null)
        {
            DestroyImmediate(currentTightenAni);
            currentTightenAni = null;
        }
        
        if (currentPunchHoleModel != null)
        {
            DestroyImmediate(currentPunchHoleModel);
            currentPunchHoleModel = null;
        }
        
        if (currentTieUpAni != null)
        {
            DestroyImmediate(currentTieUpAni);
            currentTieUpAni = null;
        }
    }
    
    /// <summary>
    /// 通用方法：检测动画播放完成后失活动画，激活模型
    /// </summary>
    /// <param name="animator">动画器</param>
    /// <param name="model">要激活的模型</param>
    private void ActivateModelAfterAnimation(Animator animator, GameObject model)
    {
        if (animator != null && model != null)
        {
            StartCoroutine(WaitForAnimationComplete(animator, model));
        }
        else
        {
            Debug.LogError("[CableTieController] ActivateModelAfterAnimation: 参数为空！");
        }
    }
    
    /// <summary>
    /// 通用方法：失活模型，激活动画
    /// </summary>
    /// <param name="model">要失活的模型</param>
    /// <param name="animation">要激活的动画</param>
    private void ActivateAnimationDeactivateModel(GameObject model, GameObject animation)
    {
        if (model != null && animation != null)
        {
            model.SetActive(false);
            animation.SetActive(true);
            Debug.Log($"[CableTieController] 模型 {model.name} 已失活，动画 {animation.name} 已激活");
        }
        else
        {
            Debug.LogError("[CableTieController] ActivateAnimationDeactivateModel: 参数为空！");
        }
    }
    
    /// <summary>
    /// 协程：等待动画播放完成
    /// </summary>
    private IEnumerator WaitForAnimationComplete(Animator animator, GameObject modelToActivate)
    {
        yield return null; // 等待一帧确保动画开始播放
        
        while (animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1.0f)
        {
            yield return null; // 等待动画播放完成
        }
        
        // 动画播放完成，失活动画，激活模型
        animator.gameObject.SetActive(false);
        if (modelToActivate != null)
        {
            modelToActivate.SetActive(true);
            Debug.Log($"[CableTieController] 动画播放完成，{animator.gameObject.name} 已失活，{modelToActivate.name} 已激活");
        }
        
        // 🔧 触发步骤2完成回调（TightenTubeAni播放完成后）
        if (!isStep2Completed && OnStep2BackwardCompleted != null)
        {
            isStep2Completed = true;
            Debug.Log("[CableTieController] 🎯 步骤2完成，触发OnStep2Completed回调");
            OnStep2BackwardCompleted.Invoke();
        }
    }
    
    #endregion
    
    #region 流程事件处理
    
    // ==================== 【测试模式】直接实例化初始模型 ====================
    /// <summary>
    /// 测试模式：直接实例化初始扎带模型（跳过Socket连接等待）
    /// 用于测试流程，不需要等待外部Socket连接触发
    /// </summary>
    private void TestDirectInstantiateInitialModel()
    {
        Debug.Log("[CableTieController] 【测试模式】开始实例化初始扎带模型");
        Debug.Log($"[CableTieController] 当前物体位置: {transform.position}");
        Debug.Log($"[CableTieController] 实例化偏移: {instanceOffset}");
        
        // 检查预制体引用状态
        Debug.Log($"[CableTieController] 预制体检查:");
        Debug.Log($"  - initialGrabbableModel: {(initialGrabbableModel != null ? "已设置" : "未设置")}");
        Debug.Log($"  - TightenTubeAni: {(TightenTubeAni != null ? "已设置" : "未设置")}");
        Debug.Log($"  - punchHoleModel: {(punchHoleModel != null ? "已设置" : "未设置")}");
        Debug.Log($"  - tieUpAni: {(tieUpAni != null ? "已设置" : "未设置")}");
        
        // InstantiateOriginModel();
    }

    public GameObject InstantiateOriginModel(Transform spawnPos = null)
    {
        // 实例化初始可抓取模型
        if (initialGrabbableModel != null)
        {
            // Vector3 spawnPosition = transform.position + instanceOffset;
            Vector3 spawnPosition = spawnPos == null ? transform.position + instanceOffset : spawnPos.position;
            Debug.Log($"[CableTieController] 实例化位置: {spawnPosition}");
            
            // currentInitialModel = Instantiate(initialGrabbableModel, spawnPosition, initialGrabbableModel.transform.rotation);
            // currentInitialModel = Instantiate(initialGrabbableModel, spawnPosition, spawnPos.rotation);
            currentInitialModel = Instantiate(initialGrabbableModel);
            currentInitialModel.transform.position = spawnPosition;
            Debug.Log($"[CableTieController] 成功实例化初始模型: {currentInitialModel.name}");
            OriginModelSetting();
            
            // SetSocketBaseComplete();
            return currentInitialModel;
        }
        else
        {
            Debug.LogError("[CableTieController] initialGrabbableModel未设置！请在Inspector中设置预制体引用");
        }
        
        return null;
    }

    public void SetSocketBaseComplete()
    {
        // 为Socket添加连接事件，准备进入步骤2
        SocketBase socketBase = this.GetComponent<SocketBase>();
            
        if (socketBase != null && !isSocketEventAdded)
        {
            socketBase.m_enable = true;
            socketBase.m_afterConnection.AddListener(OnSocketConnected);
            isSocketEventAdded = true;
            Debug.Log($"[CableTieController] ✅ Socket事件已添加到 {socketBase.gameObject.name}，等待扎带模型连接");
            Debug.Log($"[CableTieController] 当前监听器数量: {socketBase.m_afterConnection.GetPersistentEventCount()}");
        }
        else if (socketBase == null)
        {
            Debug.LogError("[CableTieController] ❌ 未找到SocketBase组件！请检查预制体设置");
        }
        else
        {
            Debug.LogWarning("[CableTieController] ⚠️ Socket事件已经添加过了");
        }
    }

    public void SetCurrentInitialModel(GameObject model)
    {
        currentInitialModel = model;
    }

    private void OriginModelSetting()
    {
        Debug.Log("对扎带初始模型进行设置".FontColoring("yellow"));
        PlugBase plug = currentInitialModel.GetComponentInChildren<PlugBase>();

        if (plug == null)
        {
            Debug.Log("获取扎带初始模型的plug失败，请检查代码".FontColoring("red"));
            return;
        }

    }

    // ==================== 【流程步骤1】Socket连接触发（已注释用于测试） ====================
    /// <summary>
    /// 流程步骤1：Socket连接后实例化初始扎带模型
    /// 触发条件：外部物体与本物体的SocketBase连接
    /// 执行内容：实例化initialGrabbableModel，设置下一步事件监听
    /// 注意：测试期间已注释，使用TestDirectInstantiateInitialModel代替
    /// </summary>
    /*
    private void OnSocketConnected()
    {
        Debug.Log("[CableTieController] 【步骤1】Socket连接成功，开始实例化初始扎带模型");
        
        // 移除Socket事件（只触发一次）
        if (socketBase != null && isSocketEventAdded)
        {
            socketBase.m_afterConnection.RemoveListener(OnSocketConnected);
            isSocketEventAdded = false;
        }
        
        // 实例化初始可抓取模型
        if (initialGrabbableModel != null)
        {
            Vector3 spawnPosition = transform.position + instanceOffset;
            currentInitialModel = Instantiate(initialGrabbableModel, spawnPosition, transform.rotation);
            
            // 为初始模型添加连接事件，准备进入步骤2
            SocketBase initialSocketBase = currentInitialModel.GetComponent<SocketBase>();
            if (initialSocketBase == null)
            {
                initialSocketBase = currentInitialModel.GetComponentInChildren<SocketBase>();
            }
            
            if (initialSocketBase != null && !isInitialModelEventAdded)
            {
                initialSocketBase.m_afterConnection.AddListener(OnInitialModelConnected);
                isInitialModelEventAdded = true;
                Debug.Log("[CableTieController] 初始模型Socket事件已添加，等待进入步骤2");
            }
            else
            {
                Debug.LogError("[CableTieController] 初始模型中未找到SocketBase组件！");
            }
        }
        else
        {
            Debug.LogError("[CableTieController] initialGrabbableModel未设置！");
        }
    }
    */
    
    // ==================== 【流程步骤2】Socket连接触发 ====================
    /// <summary>
    /// 流程步骤2：Socket连接触发后播放收紧动画并准备打孔模型
    /// 触发条件：任意扎带模型与此Socket连接
    /// 执行内容：播放TightenTubeAni，准备punchHoleModel（步骤3的拖拽控制对象）
    /// </summary>
    private void OnSocketConnected()
    {
        Debug.Log("[CableTieController] 【步骤2】Socket连接成功，开始播放收紧动画");
        
        // 获取触发连接的模型（从SocketBase获取）
        SocketBase socketBase = this.GetComponent<SocketBase>();
        GameObject connectedModel = null;
        if (socketBase != null && socketBase._connectedPlug != null)
        {
            connectedModel = socketBase._connectedPlug.transform.parent.transform.parent.gameObject;
            Debug.Log($"[CableTieController] 检测到连接的模型: {connectedModel.name}");
            
            // 🔧 关键修改：让触发连接的模型消失，而不管它原属于哪个Controller
            if (connectedModel != null)
            {
                Debug.Log($"[CableTieController] 销毁触发连接的模型: {connectedModel.name}");
                DestroyImmediate(connectedModel);
            }
        }
        
        // 移除Socket事件（只触发一次）
        if (socketBase != null && isSocketEventAdded)
        {
            socketBase.m_afterConnection.RemoveListener(OnSocketConnected);
            isSocketEventAdded = false;
        }
        
        // 播放收紧动画（不再需要失活初始模型，因为已经销毁了）
        if (TightenTubeAni != null)
        {
            currentTightenAni = Instantiate(TightenTubeAni, transform.position, transform.rotation);
            currentTightenAni.SetActive(true);
            Debug.Log($"[CableTieController] 收紧动画 {currentTightenAni.name} 已激活");
            
            // 设置动画播放完成后激活打孔模型（进入步骤3）
            Animator tightenAnimator = currentTightenAni.GetComponent<Animator>();
            if (tightenAnimator == null)
            {
                tightenAnimator = currentTightenAni.GetComponentInChildren<Animator>();
            }
            
            if (tightenAnimator != null && punchHoleModel != null)
            {
                OnStep2ForwardCompleted?.Invoke();
                Transform parentTransform = GameObject.FindWithTag("OBIParent").transform;
                currentPunchHoleModel = Instantiate(punchHoleModel, parentTransform);

                Debug.Log("实例化打孔模型（punchHoleModel）".FontColoring("yellow"), currentPunchHoleModel.gameObject);
                
                currentPunchHoleModel.transform.SetPositionAndRotation(transform.position, transform.rotation);
                currentPunchHoleModel.SetActive(false); // 先失活，等动画完成后激活
                ActivateModelAfterAnimation(tightenAnimator, currentPunchHoleModel);
                // 延迟设置：等打孔模型激活后查找拖拽控制对象和Trigger
                StartCoroutine(DelayedSetup());
            }
            else
            {
                Debug.LogError("[CableTieController] 收紧动画的Animator或打孔模型未找到！");
            }
        }
        else
        {
            Debug.LogError("[CableTieController] TightenTubeAni未设置！");
        }
    }
    
    // ==================== 【流程步骤3】拖拽控制阶段 ====================
    /// <summary>
    /// 步骤3的延迟设置：等打孔模型激活后查找拖拽控制对象和Trigger
    /// 执行时机：TightenTubeAni播放完成，currentPunchHoleModel激活后
    /// 执行内容：查找targetGrabbable（拖拽控制）和triggerObject（穿孔检测）
    /// </summary>
    private IEnumerator DelayedSetup()
    {
        Debug.Log("[CableTieController] 🔄 DelayedSetup协程开始执行");
        
        // 等待一段时间确保打孔模型已经激活
        yield return new WaitForSeconds(0.1f);
        
        Debug.Log("[CableTieController] 【步骤3】开始设置拖拽控制阶段");
        
        // 查找并更新目标可抓取对象到实例化的对象（用于拖拽控制动画进度）
        FindTargetGrabbableFromInstantiatedPunchHole();
        
        // 查找Trigger对象（用于检测穿孔完成）
        FindTriggerFromPunchHole();
        
        // 设置Trigger事件（为步骤4做准备）
        SetupPunchHoleTrigger();
        
        Debug.Log("[CableTieController] 🔄 DelayedSetup协程执行完成");
    }
    
    #region ===== 递归查找工具方法 =====
    /// <summary>
    /// 递归查找指定名称的子物体
    /// </summary>
    /// <param name="parent">父物体</param>
    /// <param name="targetName">目标名称</param>
    /// <returns>找到的GameObject，未找到返回null</returns>
    private GameObject FindChildByName(Transform parent, string targetName)
    {
        // 直接子物体中查找
        Transform child = parent.Find(targetName);
        if (child != null)
        {
            return child.gameObject;
        }
        
        // 递归查找所有子物体
        for (int i = 0; i < parent.childCount; i++)
        {
            GameObject found = FindChildByName(parent.GetChild(i), targetName);
            if (found != null)
            {
                return found;
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// 递归查找指定Tag的子物体
    /// </summary>
    /// <param name="parent">父物体</param>
    /// <param name="targetTag">目标Tag</param>
    /// <returns>找到的GameObject，未找到返回null</returns>
    private GameObject FindChildByTag(Transform parent, string targetTag)
    {
        // 检查当前物体
        if (parent.CompareTag(targetTag))
        {
            return parent.gameObject;
        }
        
        // 递归查找所有子物体
        for (int i = 0; i < parent.childCount; i++)
        {
            GameObject found = FindChildByTag(parent.GetChild(i), targetTag);
            if (found != null)
            {
                return found;
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// 递归查找指定组件类型的子物体
    /// </summary>
    /// <typeparam name="T">组件类型</typeparam>
    /// <param name="parent">父物体</param>
    /// <returns>找到的组件，未找到返回null</returns>
    private T FindChildComponent<T>(Transform parent) where T : Component
    {
        // 检查当前物体
        T component = parent.GetComponent<T>();
        if (component != null)
        {
            return component;
        }
        
        // 递归查找所有子物体
        for (int i = 0; i < parent.childCount; i++)
        {
            T found = FindChildComponent<T>(parent.GetChild(i));
            if (found != null)
            {
                return found;
            }
        }
        
        return null;
    }
    #endregion
    
    /// <summary>
    /// 获取当前使用的Slider组件（优先动态查找，回退到null）
    /// </summary>
    private Slider GetCurrentSlider()
    {
        // 如果还没有动态查找过，尝试查找
        if (dynamicTightnessSlider == null && currentTieUpAni != null)
        {
            dynamicTightnessSlider = FindChildComponent<Slider>(currentTieUpAni.transform);
            // 🔧 修复：如果在运行时找到Slider，确保它从0开始
            if (dynamicTightnessSlider != null)
            {
                Debug.Log($"[CableTieController] 🔧 GetCurrentSlider: 运行时发现Slider，初始值为: {dynamicTightnessSlider.value}");
                dynamicTightnessSlider.minValue = 0f;
                dynamicTightnessSlider.maxValue = 1f;
                dynamicTightnessSlider.value = 0f;
                Debug.Log($"[CableTieController] 🔧 GetCurrentSlider: 强制重置Slider为0，当前值: {dynamicTightnessSlider.value}");
            }
        }
        
        return dynamicTightnessSlider;
    }
    
    /// <summary>
    /// 清理场景中其他不相关的CableTieTriggerDetector组件
    /// 确保只有正确的triggerObject (StartTriggerCube) 有事件监听器
    /// </summary>
    private void CleanupOtherTriggerDetectors()
    {
        Debug.Log("[CableTieController] 🧹 开始清理其他Trigger检测器...");
        
        // 查找场景中所有的CableTieTriggerDetector
        CableTieTriggerDetector[] allTriggers = FindObjectsOfType<CableTieTriggerDetector>();
        Debug.Log($"[CableTieController] 找到 {allTriggers.Length} 个CableTieTriggerDetector组件");
        
        int cleanedCount = 0;
        foreach (var trigger in allTriggers)
        {
            // 跳过正确的triggerObject (StartTriggerCube)
            if (triggerObject != null && trigger.gameObject == triggerObject)
            {
                Debug.Log($"[CableTieController] 保留正确的Trigger检测器: {trigger.gameObject.name}");
                continue;
            }
            
            // 清理其他trigger的事件监听器
            if (trigger.OnTriggerActivated != null)
            {
                Debug.Log($"[CableTieController] 清理不相关Trigger事件: {trigger.gameObject.name} (监听器数量: {trigger.OnTriggerActivated.GetInvocationList().Length})");
                trigger.OnTriggerActivated = null;
                cleanedCount++;
            }
            
            // 如果是明显不相关的测试对象，移除组件
            if (trigger.gameObject.name.Contains("Test") || trigger.gameObject.name.Contains("Debug"))
            {
                Debug.Log($"[CableTieController] 移除测试对象上的CableTieTriggerDetector: {trigger.gameObject.name}");
                DestroyImmediate(trigger);
                cleanedCount++;
            }
        }
        
        Debug.Log($"[CableTieController] ✅ 清理完成，处理了 {cleanedCount} 个不相关的Trigger");
    }
    
    /// <summary>
    /// 设置StartTriggerCube检测器的Trigger事件，用于检测punchHole物体碰撞，准备进入步骤4
    /// </summary>
    private void SetupPunchHoleTrigger()
    {
        Debug.Log("[CableTieController] 开始设置StartTriggerCube检测器事件...");
        
        // 第一步：清理场景中其他不相关的CableTieTriggerDetector
        CleanupOtherTriggerDetectors();
        
        if (triggerObject != null)
        {
            Debug.Log($"[CableTieController] 找到Trigger检测器: {triggerObject.name}");
            
            // 使用找到的Trigger检测器对象
            CableTieTriggerDetector triggerDetector = triggerObject.GetComponent<CableTieTriggerDetector>();
            if (triggerDetector == null)
            {
                Debug.Log("[CableTieController] 未找到CableTieTriggerDetector组件，正在添加...");
                triggerDetector = triggerObject.AddComponent<CableTieTriggerDetector>();
            }
            else
            {
                Debug.Log("[CableTieController] 找到现有的CableTieTriggerDetector组件");
                // 清理现有的事件监听器
                triggerDetector.OnTriggerActivated = null;
                Debug.Log("[CableTieController] 已清理现有事件监听器");
            }

            // 配置trigger检测器的属性：检测punchHole标签的物体
            triggerDetector.useTagFilter = true;
            triggerDetector.allowedTags = new string[] { "punchHole" };
            triggerDetector.triggerOnce = true;
            
            // 设置Trigger触发后的回调，准备进入步骤4
            triggerDetector.OnTriggerActivated = OnPunchHoleTriggerActivated;
            Debug.Log($"[CableTieController] ✅ StartTriggerCube检测器 {triggerObject.name} 事件已设置，等待punchHole物体碰撞触发进入步骤4");
            
            // 验证事件是否正确注册
            if (triggerDetector.OnTriggerActivated != null)
            {
                Debug.Log($"[CableTieController] ✅ 事件注册验证成功，监听器数量: {triggerDetector.OnTriggerActivated.GetInvocationList().Length}");
                
                // 保存引用，用于调试检查
                Debug.Log($"[CableTieController] TriggerDetector对象ID: {triggerDetector.GetInstanceID()}");
                Debug.Log($"[CableTieController] TriggerDetector GameObject: {triggerDetector.gameObject.name}");
                Debug.Log($"[CableTieController] TriggerDetector 检测标签: {string.Join(", ", triggerDetector.allowedTags)}");
            }
            else
            {
                Debug.LogError("[CableTieController] ❌ 事件注册失败！");
            }
        }
        else
        {
            Debug.LogError("[CableTieController] ❌ 未找到StartTriggerCube检测器对象！请确保场景中有名为'StartTriggerCube'的检测器对象");
        }
    }
    
    // ==================== 【流程步骤4】穿孔触发完成 ====================
    /// <summary>
    /// 流程步骤4：穿孔Trigger触发后播放最终系紧动画
    /// 触发条件：StartTriggerCube检测器检测到Tag为"punchHole"的物体碰撞
    /// 执行内容：播放tieUpAni最终系紧动画，完成整个扎带流程
    /// </summary>
    private void OnPunchHoleTriggerActivated()
    {
        Debug.Log("[CableTieController] 【步骤4】穿孔Trigger触发，进入步骤5：GrabControlCube拖拽控制");
        
        // 失活打孔模型，激活tieUpAni，准备进入拖拽控制阶段
        if (currentPunchHoleModel != null)
        {
            Debug.Log($"[CableTieController] 失活打孔模型: {currentPunchHoleModel.name}");
            currentPunchHoleModel.SetActive(false);
        }
        
        // 激活tieUpAni（如果还没有激活）
        if (currentTieUpAni != null && !currentTieUpAni.activeInHierarchy)
        {
            currentTieUpAni.SetActive(true);
            Debug.Log($"[CableTieController] 激活tieUpAni: {currentTieUpAni.name}");
        }
        
        // 启动步骤5：GrabControlCube拖拽控制
        InitializeStep5DragControl();
        OnStep4Completed?.Invoke();
    }
    
    // ==================== 【流程步骤5】GrabControlCube拖拽控制 ====================
    /// <summary>
    /// 初始化步骤5：GrabControlCube拖拽控制
    /// </summary>
    private void InitializeStep5DragControl()
    {
        Debug.Log("[CableTieController] 🎯 【步骤5】开始初始化GrabControlCube拖拽控制");
        
        if (currentTieUpAni == null)
        {
            Debug.LogError("[CableTieController] ❌ currentTieUpAni为空，无法初始化步骤5");
            return;
        }
        
        // 激活tieUpAni，使其可见和可交互
        currentTieUpAni.SetActive(true);
        Debug.Log("[CableTieController] ✅ tieUpAni已激活，开始拖拽控制阶段");
        
        // 查找GrabControlCube（拖拽控制点）
        grabControlCube = currentTieUpAni.GetComponentInChildren<Grabbable_Voltage>().gameObject;
        if (grabControlCube == null)
        {
            Debug.LogError("[CableTieController] ❌ 未找到GrabControlCube，无法初始化拖拽控制");
            return;
        }
        
        // 查找TieUpEndCube（位置同步点）
        tieUpEndCube = FindChildByTag(currentTieUpAni.transform, "TieUpEndCube");
        if (tieUpEndCube == null)
        {
            Debug.LogWarning("[CableTieController] ⚠️ 未找到TieUpEndCube，将跳过位置同步功能");
        }
        
        // 查找Slider组件
        dynamicTightnessSlider = FindChildComponent<Slider>(currentTieUpAni.transform);
        if (dynamicTightnessSlider == null)
        {
            Debug.LogWarning("[CableTieController] ⚠️ 未找到Slider组件，松紧度进度条功能将不可用");
        }
        else
        {
            // 🔧 修复：强制初始化Slider，确保从0开始
            Debug.Log($"[CableTieController] 🔧 发现Slider，初始值为: {dynamicTightnessSlider.value}");
            dynamicTightnessSlider.minValue = 0f;
            dynamicTightnessSlider.maxValue = 1f;
            dynamicTightnessSlider.value = 0f;
            dynamicTightnessSlider.gameObject.SetActive(false);
            Debug.Log($"[CableTieController] 🔧 Slider已强制重置为0，当前值: {dynamicTightnessSlider.value}");
            
            // 🔧 修复：初始化progressBarFill引用，用于颜色变化
            if (dynamicTightnessSlider.fillRect != null)
            {
                progressBarFill = dynamicTightnessSlider.fillRect.GetComponent<Image>();
                if (progressBarFill == null)
                {
                    Debug.LogWarning("[CableTieController] ⚠️ Slider的fillRect上没有Image组件");
                }
                else
                {
                    Debug.Log("[CableTieController] ✅ progressBarFill已初始化，可以进行颜色变化");
                }
            }
            else
            {
                Debug.LogWarning("[CableTieController] ⚠️ Slider的fillRect未设置！");
            }
            
            // 🔧 修复：添加CanvasGroup组件用于淡入淡出效果（参考TieStrapMovementController）
            if (dynamicTightnessSlider.GetComponent<CanvasGroup>() == null)
            {
                dynamicTightnessSlider.gameObject.AddComponent<CanvasGroup>();
                Debug.Log("[CableTieController] ✅ 已为Slider添加CanvasGroup组件");
            }
            
            Debug.Log($"[CableTieController] ✅ 找到并初始化Slider组件: {dynamicTightnessSlider.name}");
        }
        
        // 获取GrabControlCube的Grabbable_Voltage组件
        grabControlGrabbable = grabControlCube.GetComponent<Grabbable_Voltage>();
        if (grabControlGrabbable == null)
        {
            Debug.LogError("[CableTieController] ❌ GrabControlCube没有Grabbable_Voltage组件");
            return;
        }
        
        // 🔧 修复：更新currentStrapAnimator为tieUpAni的Animator
        currentStrapAnimator = currentTieUpAni.GetComponent<Animator>();
        if (currentStrapAnimator == null)
        {
            currentStrapAnimator = currentTieUpAni.GetComponentInChildren<Animator>();
        }
        
        if (currentStrapAnimator != null)
        {
            // 重新获取动画状态哈希（确保使用正确的动画）
            AnimatorStateInfo stateInfo = currentStrapAnimator.GetCurrentAnimatorStateInfo(0);
            currentAnimationStateHash = stateInfo.shortNameHash;
            currentStrapAnimator.speed = 0; // 暂停动画，完全由拖拽控制
            Debug.Log($"[CableTieController] ✅ 步骤5动画控制器已更新，状态哈希: {currentAnimationStateHash}");
        }
        else
        {
            Debug.LogError("[CableTieController] ❌ 无法从tieUpAni中获取Animator组件");
        }
        
        // 设置新的拖拽控制对象
        SetupStep5DragControl();
        
        // 🎓 教学模式判断：如果不是教学模式，直接触发步骤5完成回调
        if (!isTeachingMode)
        {
            isStep5Completed = true;
            Debug.Log("[CableTieController] 🎓 非教学模式：直接触发步骤5完成回调");
            WCNM.ForceHandsRelease();
            
            OnStep5Completed?.Invoke();
            
            // 🔧 新增：强制隐藏进度条，防止残留
            Slider currentSlider = GetCurrentSlider();
            if (currentSlider != null)
            {
                currentSlider.gameObject.SetActive(false);
                Debug.Log("[CableTieController] 非教学模式：强制隐藏进度条");
            }
        }
        
        Debug.Log("[CableTieController] ✅ 步骤5初始化完成，开始GrabControlCube拖拽控制");
    }
    
    /// <summary>
    /// 设置步骤5的拖拽控制
    /// </summary>
    private void SetupStep5DragControl()
    {
        // 更新targetGrabbable为GrabControlCube
        if (targetGrabbable != null)
        {
            // 清理原有事件绑定
            targetGrabbable.onGrab.RemoveListener(OnGrab);
        }
        
        // 设置新的目标
        targetGrabbable = grabControlGrabbable;
        targetRb = grabControlCube.GetComponent<Rigidbody>();
        
        // 重新设置初始位置和运动轴（基于Z轴）
        initialWorldPosition = grabControlCube.transform.position;
        initialWorldRotation = grabControlCube.transform.rotation;
        movementAxis_World = -grabControlCube.transform.forward; // Z轴反方向
        
        // 绑定新的事件
        targetGrabbable.onGrab.AddListener(OnGrabControlCubeGrab);
        targetGrabbable.onRelease.AddListener(OnGrabControlCubeRelease);
        
        // 重置进度
        lastNormalizedTime = 0f;
        lastRatchetStep = 0;
        
        Debug.Log("[CableTieController] ✅ GrabControlCube拖拽控制设置完成");
    }
    
    /// <summary>
    /// GrabControlCube被抓取时的处理
    /// </summary>
    private void OnGrabControlCubeGrab(Hand hand, Grabbable grabbable)
    {
        Debug.Log("[CableTieController] 📦 GrabControlCube被抓取，开始拖拽控制");
        OnGrab(hand, grabbable); // 复用原有逻辑
    }
    
    /// <summary>
    /// GrabControlCube被释放时的处理
    /// </summary>
    private void OnGrabControlCubeRelease(Hand hand, Grabbable grabbable)
    {
        Debug.Log("[CableTieController] 📦 GrabControlCube被释放，执行触觉反馈和位置同步");
        
        // 执行触觉反馈
        if(useHaptics && hand != null)
        {
            float releaseStrength = currentTightnessLevel == TightnessLevel.Acceptable ? 
                onReleaseStrength : onReleaseStrength * 0.5f;
            TriggerHapticFeedback(hand, releaseStrength, onReleaseDuration);
        }

        // 🔧 修复：参考TieStrapMovementController，直接同步执行位置修正
        PerformPositionSync();
        
        // 🎯 新增：检查松紧度状态，如果是Acceptable则触发步骤5完成回调
        if (currentTightnessLevel == TightnessLevel.Acceptable && !isStep5Completed && isTeachingMode)
        {
            isStep5Completed = true;
            Debug.Log("[CableTieController] 步骤5完成（松紧度状态为Acceptable），触发OnStep5Completed回调");
            
            WCNM.ForceHandsRelease();
            
            OnStep5Completed?.Invoke();

            DestroyImmediate(grabControlCube);
        }
        else if (currentTightnessLevel != TightnessLevel.Acceptable)
        {
            Debug.Log($"[CableTieController] 松紧度状态为 {currentTightnessLevel}，未达到Acceptable，不触发步骤5完成回调");
        }
        
        // 隐藏松紧度进度条（如果需要）
        Slider currentSlider = GetCurrentSlider();
        if (currentSlider != null && currentSlider.gameObject.activeSelf)
        {
            Debug.Log("[CableTieController] 准备启动淡出协程");
            
            // 🔧 新增：先停止任何正在进行的淡入协程
            if (fadeInCoroutine != null)
            {
                StopCoroutine(fadeInCoroutine);
                fadeInCoroutine = null;
                Debug.Log("[CableTieController] 停止了正在进行的淡入协程，确保淡出干净启动");
            }
            
            // 已存在的 FadeOut 防护
            if (fadeOutCoroutine != null)
            {
                StopCoroutine(fadeOutCoroutine);
            }
            fadeOutCoroutine = StartCoroutine(FadeOutProgressBar());
        }
    }
    
    /// <summary>
    /// 🔧 修复：参考TieStrapMovementController，直接执行位置同步
    /// </summary>
    private void PerformPositionSync()
    {
        // 功能：将GrabControlCube的位置和旋转同步到TieUpEndCube的位置和旋转
        if (tieUpEndCube != null && grabControlCube != null)
        {
            Vector3 endCubePosition = tieUpEndCube.transform.position;
            Quaternion endCubeRotation = tieUpEndCube.transform.rotation;
            Vector3 oldGrabControlPosition = grabControlCube.transform.position;
            Quaternion oldGrabControlRotation = grabControlCube.transform.rotation;
            
            // 将GrabControlCube的位置和旋转都同步成TieUpEndCube的位置和旋转
            grabControlCube.transform.position = endCubePosition;
            grabControlCube.transform.rotation = endCubeRotation;
            
            Debug.Log($"[CableTieController] 📍 位置同步完成: GrabControlCube({oldGrabControlPosition}, {oldGrabControlRotation.eulerAngles}) -> TieUpEndCube({endCubePosition}, {endCubeRotation.eulerAngles})");
            
            // 🔍 调试：验证同步结果
            Debug.Log($"[CableTieController] 🔍 同步后验证:");
            Debug.Log($"  - GrabControlCube最终位置: {grabControlCube.transform.position}");
            Debug.Log($"  - TieUpEndCube位置: {tieUpEndCube.transform.position}");
            Debug.Log($"  - 位置差异: {Vector3.Distance(grabControlCube.transform.position, tieUpEndCube.transform.position)}");
        }
        else
        {
            Debug.LogWarning("[CableTieController] ⚠️ 位置同步失败：grabControlCube或tieUpEndCube为空");
            Debug.Log($"  - grabControlCube: {(grabControlCube != null ? grabControlCube.name : "null")}");
            Debug.Log($"  - tieUpEndCube: {(tieUpEndCube != null ? tieUpEndCube.name : "null")}");
        }
    }
    
    public void CloseTieUpAni()
    {
        if (currentTieUpAni != null)     currentTieUpAni.SetActive(false);
    }
    
    
    
    #endregion

    private Coroutine fadeInCoroutine;   // 跟踪淡入协程
    private Coroutine fadeOutCoroutine;  // 跟踪淡出协程（已存在）
}
#endregion
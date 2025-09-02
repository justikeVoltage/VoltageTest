/****************************************************
    功能：定制化抓取类，根据抓取方块X轴的位移计算播放动画进度，限制方块抓取后回到X轴轨道并且修正旋转角度
    作者：LZL
    创建日期：#2025/08/01#
    修改人：LZL
    修改日期：#2025/08/03#
    修改内容：
    1. 新增可调节的动画终点百分比 (animationEndPointPercentage)
    2. 修正AutoHand 4.0.1.1的触觉反馈API调用
    3. 新增松紧度考核系统，包含UI进度条和分级触觉反馈
    4. 优化UI引用，自动从Slider获取Image组件
*****************************************************/
using UnityEngine;
using UnityEngine.UI;
using Autohand;
using Voltage;
using System.Collections;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Grabbable))]
public class TieStrapMovementController : MonoBehaviour
{
    [Header("拖拽限制设置")]
    [SerializeField] private float maxNegativeX = -1f;

    [Header("动画设置")]
    [SerializeField] private Animator strapAnimator;
    [SerializeField] private string animationStateName = "Tighten";

    [Header("动画终点百分比")]
    [Range(0f, 1f)]
    [SerializeField] private float animationEndPointPercentage = 1.0f;

    [Header("松紧度考核设置")]
    [SerializeField] private Slider tightnessProgressBar; // 只需要这一个引用！
    [Range(0f, 1f)]
    [SerializeField] private float minAcceptableTightness = 0.7f;
    [Range(0f, 1f)]
    [SerializeField] private float maxAcceptableTightness = 0.9f;
    
    [Header("松紧度颜色设置")]
    [SerializeField] private Color tooLooseColor = Color.yellow;
    [SerializeField] private Color acceptableColor = Color.green;
    [SerializeField] private Color tooTightColor = Color.red;

    [Header("触觉反馈设置")]
    [SerializeField] private bool useHaptics = true;
    [SerializeField] private int ratchetSteps = 10;
    [SerializeField, Range(0,1)] private float onGrabStrength = 0.4f;
    [SerializeField] private float onGrabDuration = 0.05f;
    [SerializeField, Range(0,1)] private float ratchetClickStrength = 0.2f;
    [SerializeField] private float ratchetClickDuration = 0.03f;
    [SerializeField, Range(0,1)] private float onReleaseStrength = 0.6f;
    [SerializeField] private float onReleaseDuration = 0.1f;

    [Header("松紧度触觉反馈设置")]
    [SerializeField, Range(0,1)] private float tooLooseHapticStrength = 0.1f;
    [SerializeField] private float tooLooseHapticDuration = 0.5f;
    [SerializeField, Range(0,1)] private float acceptableHapticStrength = 0.3f;
    [SerializeField] private float acceptableHapticDuration = 0.3f;
    [SerializeField, Range(0,1)] private float tooTightHapticStrength = 0.8f;
    [SerializeField] private float tooTightHapticDuration = 0.1f;

    // --- 私有变量 ---
    private Rigidbody rb;
    private Grabbable_Voltage grabbable;
    private Vector3 initialWorldPosition;
    private Quaternion initialWorldRotation;
    private Vector3 movementAxis_World;
    private float lastNormalizedTime = 0f;
    private int lastRatchetStep = 0;
    
    // 自动获取的Image组件
    private Image progressBarFill;
    
    // 松紧度状态追踪
    private TightnessLevel currentTightnessLevel = TightnessLevel.TooLoose;
    private TightnessLevel lastTightnessLevel = TightnessLevel.TooLoose;
    private float lastTightnessHapticTime = 0f;

    // 松紧度等级枚举
    public enum TightnessLevel
    {
        TooLoose,
        Acceptable,
        TooTight
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        grabbable = GetComponent<Grabbable_Voltage>();

        if (grabbable != null)
        {
            grabbable.onGrab.AddListener(OnGrab);
            grabbable.onRelease.AddListener(OnRelease);
        }
    }

    private void Start()
    {
        initialWorldPosition = transform.position;
        initialWorldRotation = transform.rotation;
        movementAxis_World = -transform.right;

        if (strapAnimator != null)
        {
            strapAnimator.speed = 0;
            strapAnimator.Play(animationStateName, 0, 0f);
        }
        else
        {
            Debug.LogError("Animator组件未被引用！", this.gameObject);
        }

        InitializeProgressBar();
    }
    
    private void OnDestroy()
    {
        if (grabbable != null)
        {
            grabbable.onGrab.RemoveListener(OnGrab);
            grabbable.onRelease.RemoveListener(OnRelease);
        }
    }

    private void LateUpdate()
    {
        if (!grabbable.IsHeld()) return;

        Vector3 worldDisplacementVector = transform.position - initialWorldPosition;
        float projectedDisplacement = Vector3.Dot(worldDisplacementVector, movementAxis_World);

        projectedDisplacement = Mathf.Max(0, projectedDisplacement);
        float potentialNormalizedTime = Mathf.Abs(maxNegativeX) > 0 ? projectedDisplacement / Mathf.Abs(maxNegativeX) : 0;
        potentialNormalizedTime = Mathf.Clamp01(potentialNormalizedTime);

        if (potentialNormalizedTime > lastNormalizedTime)
        {
            lastNormalizedTime = potentialNormalizedTime;
        }
        
        if (strapAnimator != null)
        {
            float finalAnimationProgress = lastNormalizedTime * animationEndPointPercentage;
            strapAnimator.Play(animationStateName, 0, finalAnimationProgress);
        }

        UpdateTightnessAssessment();
        HandleRatchetHaptics();
        HandleTightnessHaptics();
    }

    public void OnGrab(Hand hand, Grabbable grabbable)
    {
        if(useHaptics && hand != null)
            TriggerHapticFeedback(hand, onGrabStrength, onGrabDuration);

        lastRatchetStep = 0;
        
        // 显示进度条，带淡入效果
        if (tightnessProgressBar != null)
        {
            tightnessProgressBar.gameObject.SetActive(true);
            StartCoroutine(FadeInProgressBar());
        }
    }

    public void OnRelease(Hand hand, Grabbable grabbable)
    {
        if(useHaptics && hand != null)
        {
            float releaseStrength = currentTightnessLevel == TightnessLevel.Acceptable ? 
                onReleaseStrength : onReleaseStrength * 0.5f;
            TriggerHapticFeedback(hand, releaseStrength, onReleaseDuration);
        }

        float finalDisplacementOnAxis = lastNormalizedTime * Mathf.Abs(maxNegativeX);
        Vector3 finalWorldPosition = initialWorldPosition + (movementAxis_World * finalDisplacementOnAxis);

        transform.position = finalWorldPosition;
        transform.rotation = initialWorldRotation;
        
        // 隐藏进度条，带淡出效果
        if (tightnessProgressBar != null)
        {
            StartCoroutine(FadeOutProgressBar());
        }
    }

    // AutoHand 4.0.1.1 兼容的触觉反馈方法
    private void TriggerHapticFeedback(Hand hand, float strength, float duration)
    {
        if (hand == null) return;
        
        try
        {
            // AutoHand 4.0.1.1 的正确触觉反馈调用方式
            // 注意：参数顺序是 (duration, strength)
            hand.PlayHapticVibration(duration, strength);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"触觉反馈触发失败: {e.Message}");
        }
    }

    private void HandleRatchetHaptics()
    {
        if (!useHaptics || ratchetSteps <= 0) return;

        int currentStep = Mathf.FloorToInt(lastNormalizedTime * ratchetSteps);

        if (currentStep > lastRatchetStep && grabbable.GetHeldBy().Count > 0)
        {
            TriggerHapticFeedback(grabbable.GetHeldBy()[0], ratchetClickStrength, ratchetClickDuration);
            lastRatchetStep = currentStep;
        }
    }

    private void InitializeProgressBar()
    {
        if (tightnessProgressBar != null)
        {
            tightnessProgressBar.minValue = 0f;
            tightnessProgressBar.maxValue = 1f;
            tightnessProgressBar.value = 0f;
            tightnessProgressBar.gameObject.SetActive(false);
            
            // 自动从Fill Rect获取Image组件
            if (tightnessProgressBar.fillRect != null)
            {
                progressBarFill = tightnessProgressBar.fillRect.GetComponent<Image>();
                if (progressBarFill == null)
                {
                    Debug.LogWarning("Slider的Fill Rect上没有找到Image组件！", this);
                }
            }
            else
            {
                Debug.LogWarning("Slider的Fill Rect未设置！", this);
            }
            
            // 添加淡入淡出组件（如果需要）
            if (tightnessProgressBar.GetComponent<CanvasGroup>() == null)
            {
                tightnessProgressBar.gameObject.AddComponent<CanvasGroup>();
            }
        }
    }

    private void UpdateTightnessAssessment()
    {
        // 更新进度条值 - 这会自动让Fill区域变长
        if (tightnessProgressBar != null)
        {
            tightnessProgressBar.value = lastNormalizedTime;
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
        if (!useHaptics || grabbable.GetHeldBy().Count == 0) return;

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

            TriggerHapticFeedback(grabbable.GetHeldBy()[0], hapticStrength, hapticDuration);
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
        CanvasGroup canvasGroup = tightnessProgressBar.GetComponent<CanvasGroup>();
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            while (canvasGroup.alpha < 1f)
            {
                canvasGroup.alpha += Time.deltaTime * 3f;
                yield return null;
            }
            canvasGroup.alpha = 1f;
        }
    }

    // 淡出动画
    private IEnumerator FadeOutProgressBar()
    {
        CanvasGroup canvasGroup = tightnessProgressBar.GetComponent<CanvasGroup>();
        if (canvasGroup != null)
        {
            while (canvasGroup.alpha > 0f)
            {
                canvasGroup.alpha -= Time.deltaTime * 2f;
                yield return null;
            }
            canvasGroup.alpha = 0f;
        }
        tightnessProgressBar.gameObject.SetActive(false);
    }

    // 公共方法：获取当前松紧度等级（供其他脚本调用）
    public TightnessLevel GetCurrentTightnessLevel()
    {
        return currentTightnessLevel;
    }

    // 公共方法：获取当前松紧度数值（供其他脚本调用）
    public float GetCurrentTightness()
    {
        return lastNormalizedTime;
    }
}
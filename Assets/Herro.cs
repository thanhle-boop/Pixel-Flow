using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MoreMountains.Feedbacks;
using UnityEngine.Events;
using MoreMountains.Tools;

/// <summary>
/// A very simple class used to make a character jump, designed to be used in Feel's Getting Started tutorial
/// </summary>
public class Herro : MonoBehaviour
{
    [Header("Hero Settings")]
    /// a key the Player has to press to make our Hero jump
    public KeyCode ActionKey = KeyCode.Space;
    /// the force to apply vertically to the Hero's rigidbody to make it jump up
    public float JumpForce = 8f;

    [Header("Feedbacks")]
    /// a MMF_Player to play when the Hero starts jumping
    public MMF_Player JumpFeedback;
    /// a MMF_Player to play when the Hero lands after a jump
    public MMF_Player LandingFeedback;

    private const float _lowVelocity = 0.1f;
    private Rigidbody _rigidbody;
    private float _velocityLastFrame;

    public List<Transform> Targets;
    public MMTween.MMTweenCurve AttackCurve = MMTween.MMTweenCurve.EaseInOutOverhead;

    private bool _jumping = false;

    /// <summary>
    /// On Awake we store our Rigidbody and force gravity to -30 on the y axis so that jumps feel better
    /// </summary>
    private void Awake()
    {
        _rigidbody = this.gameObject.GetComponent<Rigidbody>();
        Physics.gravity = Vector3.down * 30;
    }

    /// <summary>
    /// Every frame
    /// </summary>
    private void Update()
    {
        // we check if the Player has pressed our action key, and trigger a jump if that's the case
        if (Input.GetKeyDown(ActionKey) && !_jumping)
        {
            Jump();
            StartCoroutine(PlayFeedbackAndDone());
        }

        // if we're jumping, were going down last frame, and have now reached an almost null velocity
        if (_jumping && (_velocityLastFrame < 0) && (Mathf.Abs(_rigidbody.linearVelocity.y) < _lowVelocity))
        {
            // then we just landed, we reset our state
            _jumping = false;
            LandingFeedback?.PlayFeedbacks();
        }

        // we store our velocity
        _velocityLastFrame = _rigidbody.linearVelocity.y;
    }

    /// <summary>
    /// Makes our hero jump in the air
    /// </summary>
    private void Jump()
    {
        // _rigidbody.AddForce(Vector3.up * JumpForce, ForceMode.Impulse);
        _jumping = true;
    }

    IEnumerator PlayFeedbackAndDone()
{
    // Bắt đầu chạy feedback
    JumpFeedback.PlayFeedbacks();

    // Chờ đúng bằng khoảng thời gian feedback diễn ra
    yield return new WaitForSeconds(JumpFeedback.TotalDuration);


    for (int i = 0; i < Targets.Count; i++)
    {
        var distance = Vector3.Distance(this.transform.position, Targets[i].position);
        var intervalDuration = distance / JumpForce; // Tính toán thời gian di chuyển dự
        MMTween.MoveTransform(this, this.transform, this.transform.position, Targets[i].position, null, 0f, intervalDuration, AttackCurve);
        yield return MMCoroutine.WaitFor(intervalDuration);
    }
    
    // Logic sau khi feedback xong
    Debug.Log("Feedback đã hoàn thành!");
}
}
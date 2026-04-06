using System.Collections;
using System.Security.Cryptography;
using MoreMountains.Feedbacks;
using MoreMountains.Tools;
using UnityEngine;

public class player : MonoBehaviour
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
    private Rigidbody2D _rigidbody;
    private float _velocityLastFrame;
    private bool _jumping = false;
    public Transform targetPosition;

    public AnimationCurve ArcCurve = new AnimationCurve(new Keyframe(0, 0), new Keyframe(0.5f, 1), new Keyframe(1, 0));

    public MMTween.MMTweenCurve AttackCurve;

    public float JumpHeight = 2f;

    /// <summary>
    /// On Awake we store our Rigidbody and force gravity to -30 on the y axis so that jumps feel better
    /// </summary>
    private void Awake()
    {
        _rigidbody = this.gameObject.GetComponent<Rigidbody2D>();
        Physics2D.gravity = Vector2.down * 30;
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
        }

        // if we're jumping, were going down last frame, and have now reached an almost null velocity
        if (_jumping && (_velocityLastFrame < 0) && (Mathf.Abs(_rigidbody.linearVelocity.y) < _lowVelocity))
        {
            // then we just landed, we reset our state
            _jumping = false;
        }

        // we store our velocity
        _velocityLastFrame = _rigidbody.linearVelocity.y;
    }

    /// <summary>
    /// Makes our hero jump in the air
    /// </summary>
private void Jump()
{
    _jumping = true;
    var distance = Vector3.Distance(this.transform.position, targetPosition.position);
    var intervalDuration = distance / JumpForce; 

    // Thay vì gọi MMTween.MoveTransform, ta gọi Coroutine nhảy vòng cung
    StartCoroutine(JumpArcCoroutine(this.transform.position, targetPosition.position, intervalDuration));
}

private IEnumerator JumpArcCoroutine(Vector3 startPos, Vector3 endPos, float duration)
{
    float timeSpent = 0f;

    while (timeSpent < duration)
    {
        Vector3 currentPos = MMTween.Tween(timeSpent, 0f, duration, startPos, endPos, AttackCurve);

        float percent = timeSpent / duration; 
        
        float arcOffset = ArcCurve.Evaluate(percent) * JumpHeight;
        
        currentPos.y += arcOffset;

        this.transform.position = currentPos;

        timeSpent += Time.deltaTime;
        yield return null;
    }

    this.transform.position = endPos;
    
    _jumping = false; 
}
}

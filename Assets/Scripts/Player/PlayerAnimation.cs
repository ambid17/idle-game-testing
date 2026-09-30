using Player;
using UnityEngine;


// Code-driven sprite flipbook rather than an Animator: the states are a simple priority list
// (mine > fly > move > idle) read straight off PlayerMining/PlayerController each frame, so an
// Animator Controller's transition graph would just be duplicating that logic.
public class PlayerAnimation : MonoBehaviour
{
    [SerializeField] private PlayerController controller;
    [SerializeField] private PlayerMining mining;

    [Header("Frames")]
    [SerializeField] private Sprite[] idleFrames;
    [SerializeField] private Sprite[] moveFrames;
    [SerializeField] private Sprite[] flyFrames;
    [SerializeField] private Sprite[] mineFrames;

    [Header("Frames Per Second")]
    [SerializeField] private float idleFps = 6f;
    [SerializeField] private float moveFps = 10f;
    [SerializeField] private float flyFps = 12f;
    [SerializeField] private float mineFps = 14f;

    private SpriteRenderer spriteRenderer;
    private readonly SpriteFlipbook flipbook = new();


    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null) Debug.LogError($"{nameof(PlayerAnimation)} on {name} requires a SpriteRenderer.");
        if (controller == null) Debug.LogError($"{nameof(PlayerAnimation)} on {name} is missing its controller reference.");
        if (mining == null) Debug.LogError($"{nameof(PlayerAnimation)} on {name} is missing its mining reference.");
    }

    void Update()
    {
        UpdateFacing();
        UpdateFrames();
    }

    private void UpdateFacing()
    {
        if (controller.MovementInput.x == 0)
        {
            return;
        }
        var movingRight = controller.MovementInput.x > 0;
        spriteRenderer.flipX = !movingRight;
    }

    private void UpdateFrames()
    {
        ResolveState(out var frames, out var fps);
        flipbook.Tick(spriteRenderer, frames, fps);
    }

    private void ResolveState(out Sprite[] frames, out float fps)
    {
        if (mining.IsMining)
        {
            frames = mineFrames;
            fps = mineFps;
        }
        else if (controller.IsFlying)
        {
            frames = flyFrames;
            fps = flyFps;
        }
        else if (controller.IsGrounded && controller.MovementInput.x != 0)
        {
            frames = moveFrames;
            fps = moveFps;
        }
        else
        {
            frames = idleFrames;
            fps = idleFps;
        }
    }
}

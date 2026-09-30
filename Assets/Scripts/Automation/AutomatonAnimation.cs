using UnityEngine;

namespace Automation
{
    // Code-driven sprite flipbook for the Mining Automaton, same approach as PlayerAnimation:
    // priority list mine > fly > move > idle read straight off MiningAutomaton each frame. Unlike
    // the player there's no input to read, so "moving" and facing come from the frame-to-frame
    // position delta. Runs in LateUpdate so it sees this frame's movement and IsMining.
    [RequireComponent(typeof(MiningAutomaton))]
    public class AutomatonAnimation : MonoBehaviour
    {
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

        // Below this horizontal distance (world units) a mining target counts as straight down,
        // so the automaton keeps whichever way it was already facing.
        [SerializeField] private float facingDeadZone = 0.05f;

        private MiningAutomaton automaton;
        private SpriteRenderer spriteRenderer;
        private readonly SpriteFlipbook flipbook = new();
        private Vector3 lastPosition;

        void Start()
        {
            automaton = GetComponent<MiningAutomaton>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null) Debug.LogError($"{nameof(AutomatonAnimation)} on {name} requires a SpriteRenderer.");
            lastPosition = transform.position;
        }

        void LateUpdate()
        {
            Vector3 delta = transform.position - lastPosition;
            lastPosition = transform.position;
            bool isMoving = delta.sqrMagnitude > 0.000001f;

            UpdateFacing(delta);
            ResolveState(isMoving, out var frames, out var fps);
            flipbook.Tick(spriteRenderer, frames, fps);
        }

        // Art faces right; flip when heading (or drilling) left.
        private void UpdateFacing(Vector3 delta)
        {
            float dx = automaton.IsMining ? automaton.MiningTargetPosition.x - transform.position.x : delta.x;
            float deadZone = automaton.IsMining ? facingDeadZone : 0.0001f;
            if (Mathf.Abs(dx) < deadZone) return;
            spriteRenderer.flipX = dx < 0f;
        }

        private void ResolveState(bool isMoving, out Sprite[] frames, out float fps)
        {
            if (automaton.IsMining)
            {
                frames = mineFrames;
                fps = mineFps;
            }
            else if (automaton.IsFlying && isMoving)
            {
                frames = flyFrames;
                fps = flyFps;
            }
            else if (isMoving)
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
}

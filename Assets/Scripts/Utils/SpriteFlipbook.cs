using UnityEngine;

// Plays a looping Sprite[] on a SpriteRenderer at a fixed fps. Shared by the code-driven
// animators (PlayerAnimation, Automation.AutomatonAnimation) that pick a frame set per state
// each frame rather than going through an Animator Controller.
public class SpriteFlipbook
{
    private Sprite[] currentFrames;
    private float frameTimer;
    private int frameIndex;

    public void Tick(SpriteRenderer spriteRenderer, Sprite[] frames, float fps)
    {
        if (frames == null || frames.Length == 0) return;

        // Restart from frame 0 on a state change so e.g. the drill always spins up from the start.
        if (frames != currentFrames)
        {
            currentFrames = frames;
            frameIndex = 0;
            frameTimer = 0f;
            spriteRenderer.sprite = frames[0];
            return;
        }

        frameTimer += Time.deltaTime;
        float frameDuration = 1f / fps;
        while (frameTimer >= frameDuration)
        {
            frameTimer -= frameDuration;
            frameIndex = (frameIndex + 1) % frames.Length;
        }
        spriteRenderer.sprite = frames[frameIndex];
    }
}

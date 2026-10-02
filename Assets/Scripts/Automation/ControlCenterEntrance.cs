using System.Collections;
using UnityEngine;

namespace Automation
{
    // The Control Center's sliding double door. The door leaves are their own sprites (cut out of
    // the building art by Tools/Buildings/make_control_center_doors.py) clipped by a SpriteMask
    // the shape of the doorway, so sliding them apart reads as retracting into the frame.
    // Lives on the Control Center building; driven by ControlCenterRevealController to walk the
    // first Mining Automaton out during the reveal cinematic.
    public class ControlCenterEntrance : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer buildingSprite;
        [SerializeField] private SpriteRenderer leftDoor;
        [SerializeField] private SpriteRenderer rightDoor;
        [Tooltip("How far each leaf slides sideways when open (local units).")]
        [SerializeField] private float slideDistance = 0.089f;
        [SerializeField] private float slideSeconds = 0.6f;

        [Header("Automaton walk-out")]
        [Tooltip("Where the automaton first appears, inside the doorway.")]
        [SerializeField] private Transform doorway;
        [Tooltip("Where it stands once it has stepped out in front of the door.")]
        [SerializeField] private Transform doorstep;
        [Tooltip("Where it walks to before it starts mining.")]
        [SerializeField] private Transform exit;
        [SerializeField] private float emergeSeconds = 0.9f;
        [SerializeField] private float walkSpeed = 1.5f;

        private Vector3 leftClosedPosition;
        private Vector3 rightClosedPosition;

        private void Awake()
        {
            if (buildingSprite == null) Debug.LogError("ControlCenterEntrance.buildingSprite is not assigned.");
            if (leftDoor == null) Debug.LogError("ControlCenterEntrance.leftDoor is not assigned.");
            if (rightDoor == null) Debug.LogError("ControlCenterEntrance.rightDoor is not assigned.");
            if (doorway == null) Debug.LogError("ControlCenterEntrance.doorway is not assigned.");
            if (doorstep == null) Debug.LogError("ControlCenterEntrance.doorstep is not assigned.");
            if (exit == null) Debug.LogError("ControlCenterEntrance.exit is not assigned.");

            leftClosedPosition = leftDoor.transform.localPosition;
            rightClosedPosition = rightDoor.transform.localPosition;
        }

        // The reveal cinematic tints the building through its SpriteRenderer's colour as it comes
        // out of the portal - the door leaves are separate renderers, so they follow it.
        private void LateUpdate()
        {
            leftDoor.color = buildingSprite.color;
            rightDoor.color = buildingSprite.color;
        }

        public IEnumerator DeployThroughDoors(MiningAutomaton automaton)
        {
            yield return Slide(0f, 1f);
            yield return automaton.WalkOut(doorway.position, doorstep.position, exit.position, emergeSeconds, walkSpeed);
            yield return Slide(1f, 0f);
        }

        private IEnumerator Slide(float fromOpen, float toOpen)
        {
            float elapsed = 0f;
            while (elapsed < slideSeconds)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / slideSeconds));
                SetOpen(Mathf.Lerp(fromOpen, toOpen, t));
                yield return null;
            }
            SetOpen(toOpen);
        }

        private void SetOpen(float open)
        {
            Vector3 offset = Vector3.right * (slideDistance * open);
            leftDoor.transform.localPosition = leftClosedPosition - offset;
            rightDoor.transform.localPosition = rightClosedPosition + offset;
        }
    }
}

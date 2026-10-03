using System.Collections.Generic;
using UnityEngine;

namespace Automation
{
    // Shared movement math for automaton/storage-drone/fuel-drone behavior scripts. Plain C# class
    // (not a MonoBehaviour) owned and ticked by each entity's own Update, mirroring how
    // MapGenerationService owns a plain MineWorld instance rather than everything being a component.
    public class GridPathMover
    {
        // Straight-line "fly" movement, ignoring the grid entirely - Storage/Fuel Drones always fly
        // this way. Mining Automatons never do through the mine (they path through open cells,
        // even home to the Depot), only for short hops in open air. Returns true once arrived.
        public bool StepDirect(Transform t, Vector3 destination, float speed, float arriveThreshold = 0.5f)
        {
            t.position = Vector3.MoveTowards(t.position, destination, speed * Time.deltaTime);
            return (t.position - destination).sqrMagnitude <= arriveThreshold * arriveThreshold;
        }

        private Transform chaseTarget;
        private Vector3 lastChasePosition;
        private int lastChaseFrame;

        // StepDirect at a target that may itself be moving (the player, a walking automaton):
        // adds the target's own current speed (capped at maxSpeedMatch) on top of `speed`, so the
        // gap always closes at no less than `speed` however fast the target is running away -
        // otherwise a drone slower than the player never arrives while they keep moving. The cap
        // stops a teleport (portal, recall) from dragging the chaser across the map with it.
        public bool StepChase(Transform t, Transform target, float speed, float maxSpeedMatch, float arriveThreshold = 0.5f)
        {
            Vector3 destination = target.position;
            // Only trust the last sample if it's from the previous frame of this same chase.
            bool isContinuingChase = chaseTarget == target && lastChaseFrame == Time.frameCount - 1;
            if (isContinuingChase && Time.deltaTime > 0f)
            {
                float targetSpeed = (destination - lastChasePosition).magnitude / Time.deltaTime;
                speed += Mathf.Min(targetSpeed, maxSpeedMatch);
            }

            chaseTarget = target;
            lastChasePosition = destination;
            lastChaseFrame = Time.frameCount;

            return StepDirect(t, destination, speed, arriveThreshold);
        }

        // Walks a precomputed list of world-space waypoints in order ("walk"/tunnel-constrained
        // movement). Returns true once the final waypoint is reached.
        //
        // Rounds corners rather than snapping straight to each waypoint then pivoting to a new
        // heading: once within `cornerRadius` of a non-final waypoint, blends the movement
        // direction toward the following segment's direction proportionally to remaining
        // distance, so the path traced through a turn is a curve rather than a hard right angle.
        // The final waypoint (the dig target itself) is exempt - it still gets an exact
        // MoveTowards approach, since mining-in-place needs precise placement there.
        public bool StepAlongPath(Transform t, IReadOnlyList<Vector3> waypoints, ref int index, float speed, float arriveThreshold = 0.05f, float cornerRadius = 0.35f)
        {
            if (waypoints == null || waypoints.Count == 0) return true;
            if (index >= waypoints.Count) return true;

            Vector3 target = waypoints[index];
            Vector3 toTarget = target - t.position;
            float distanceToTarget = toTarget.magnitude;
            bool isFinalWaypoint = index >= waypoints.Count - 1;

            if (isFinalWaypoint || distanceToTarget >= cornerRadius)
            {
                t.position = Vector3.MoveTowards(t.position, target, speed * Time.deltaTime);
                if ((t.position - target).sqrMagnitude <= arriveThreshold * arriveThreshold) index++;
                return index >= waypoints.Count;
            }

            // Corner-rounding zone of a non-final waypoint: blend toward the next segment's
            // direction instead of steering straight at `target`. Cutting the corner means we may
            // never land within arriveThreshold of `target` itself, so also advance once we've
            // moved past it along the next segment's direction (dot product flips positive) -
            // guaranteed to happen since the blend forces movement toward nextDir as we approach.
            // A long frame (hitch, high timeScale) would carry the unclamped blended step below
            // well past the waypoint - off the path and, from there, straight through solid ground
            // toward the next one. Land on the waypoint instead, like MoveTowards would.
            if (speed * Time.deltaTime >= distanceToTarget)
            {
                t.position = target;
                index++;
                return index >= waypoints.Count;
            }

            Vector3 nextDir = (waypoints[index + 1] - target).normalized;
            Vector3 currentDir = distanceToTarget > 0.0001f ? toTarget / distanceToTarget : nextDir;
            float blend = 1f - (distanceToTarget / cornerRadius);
            Vector3 moveDir = Vector3.Slerp(currentDir, nextDir, blend);
            t.position += moveDir * speed * Time.deltaTime;

            if ((t.position - target).sqrMagnitude <= arriveThreshold * arriveThreshold ||
                Vector3.Dot(t.position - target, nextDir) >= 0f)
            {
                index++;
            }

            return index >= waypoints.Count;
        }
    }
}

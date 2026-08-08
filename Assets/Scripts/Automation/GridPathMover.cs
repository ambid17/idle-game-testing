using System.Collections.Generic;
using UnityEngine;

namespace Automation
{
    // Shared movement math for automaton/storage-drone/fuel-drone behavior scripts. Plain C# class
    // (not a MonoBehaviour) owned and ticked by each entity's own Update, mirroring how
    // MapGenerationService owns a plain MineWorld instance rather than everything being a component.
    public class GridPathMover
    {
        // Straight-line "fly" movement, ignoring the grid entirely - per the design doc, both
        // Storage/Fuel Drones always fly this way and Mining Automatons switch to it only on the
        // return trip to the Depot. Returns true once arrived.
        public bool StepDirect(Transform t, Vector3 destination, float speed, float arriveThreshold = 0.05f)
        {
            t.position = Vector3.MoveTowards(t.position, destination, speed * Time.deltaTime);
            return (t.position - destination).sqrMagnitude <= arriveThreshold * arriveThreshold;
        }

        // Walks a precomputed list of world-space waypoints in order ("walk"/tunnel-constrained
        // movement). Returns true once the final waypoint is reached.
        public bool StepAlongPath(Transform t, IReadOnlyList<Vector3> waypoints, ref int index, float speed, float arriveThreshold = 0.05f)
        {
            if (waypoints == null || waypoints.Count == 0) return true;
            if (index >= waypoints.Count) return true;

            Vector3 target = waypoints[index];
            t.position = Vector3.MoveTowards(t.position, target, speed * Time.deltaTime);

            if ((t.position - target).sqrMagnitude <= arriveThreshold * arriveThreshold)
            {
                index++;
            }

            return index >= waypoints.Count;
        }
    }
}

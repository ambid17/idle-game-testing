using Events;
using UnityEngine;

namespace MapGeneration
{
    // The moving part of a Crusher block, spawned by StructureTrapResolver for every Crusher on a
    // resident layer. Runs a fixed cycle off Time.time - rest, shudder (the warning), slam down
    // through the open cells below, hold, retract - offset per crusher so a row of them fires as
    // a travelling wave the player can time. Purely a function of time: nothing is saved, and a
    // crusher the player has never revealed stays hidden and silent.
    //
    // It only hurts at the moment it lands (CrusherSlamEvent, resolved by Player.HazardDamageHandler),
    // but once down the extended head and shaft are solid until they retract - the blocker comes
    // on after the slam so the hit registers before physics shoves the player out of the column.
    public class CrusherPiston : MonoBehaviour
    {
        [Tooltip("How many open cells below the Crusher block the head can reach.")]
        [SerializeField] private int maxReach = 3;
        [SerializeField] private float restSeconds = 1.3f;
        [SerializeField] private float warnSeconds = 0.45f;
        [SerializeField] private float slamSeconds = 0.1f;
        [SerializeField] private float holdSeconds = 0.45f;
        [SerializeField] private float retractSeconds = 0.7f;
        [SerializeField] private float warnJiggleMagnitude = 0.05f;

        [Tooltip("Spiked head - sits over the Crusher block at rest and travels down the shaft.")]
        [SerializeField] private Transform head;
        [Tooltip("One cell tall at scale 1; stretched between the block and the head.")]
        [SerializeField] private Transform shaft;
        [Tooltip("On this object, on the Ground layer. Resized to the extended column while the piston is down.")]
        [SerializeField] private BoxCollider2D blocker;
        [Tooltip("Blocker width as a fraction of a cell - the head art's width.")]
        [SerializeField] private float blockerWidthCells = 0.8f;

        private int layerIndex;
        private int cellX;
        private int cellY;
        private float phaseOffsetSeconds;
        private float lastCycleTime;
        private int reach;
        private bool slamReported;

        private float CycleSeconds => restSeconds + warnSeconds + slamSeconds + holdSeconds + retractSeconds;

        private void Start()
        {
            if (head == null) Debug.LogError($"{nameof(CrusherPiston)} on {name} has no head assigned.");
            if (shaft == null) Debug.LogError($"{nameof(CrusherPiston)} on {name} has no shaft assigned.");
            if (blocker == null) Debug.LogError($"{nameof(CrusherPiston)} on {name} has no blocker assigned.");
        }

        public void Begin(int layerIndex, int x, int y, float phaseOffsetSeconds)
        {
            this.layerIndex = layerIndex;
            cellX = x;
            cellY = y;
            this.phaseOffsetSeconds = phaseOffsetSeconds;
            lastCycleTime = float.MaxValue;
        }

        private void Update()
        {
            var mapGen = GameManager.MapGenerationService;
            bool revealed = mapGen.IsRevealedAt(transform.position);
            head.gameObject.SetActive(revealed);

            float cycleTime = Mathf.Repeat(Time.time + phaseOffsetSeconds, CycleSeconds);
            // Wrapped around: a new cycle. Re-measure the drop, since the cells below may have
            // been dug out (or were never open at all).
            if (cycleTime < lastCycleTime)
            {
                reach = MeasureReach(mapGen);
                slamReported = false;
            }
            lastCycleTime = cycleTime;

            float slamStart = restSeconds + warnSeconds;
            float holdStart = slamStart + slamSeconds;
            float retractStart = holdStart + holdSeconds;

            float extension;
            float jiggle = 0f;
            if (cycleTime < restSeconds) extension = 0f;
            else if (cycleTime < slamStart)
            {
                extension = 0f;
                jiggle = Mathf.Sin(cycleTime * 60f) * warnJiggleMagnitude;
            }
            else if (cycleTime < holdStart) extension = (cycleTime - slamStart) / slamSeconds;
            else if (cycleTime < retractStart) extension = 1f;
            else extension = 1f - (cycleTime - retractStart) / retractSeconds;

            if (cycleTime >= holdStart && !slamReported)
            {
                slamReported = true;
                if (revealed && reach > 0) GameManager.EventService.Dispatch(new CrusherSlamEvent(layerIndex, cellX, cellY, reach));
            }

            float drop = extension * reach * mapGen.CellSize;
            head.localPosition = new Vector3(jiggle, -drop, 0f);
            shaft.gameObject.SetActive(revealed && drop > 0f);
            shaft.localPosition = new Vector3(0f, -drop * 0.5f, 0f);
            shaft.localScale = new Vector3(1f, drop / mapGen.CellSize, 1f);

            // The column from the Crusher block's underside down to the spike tips.
            blocker.enabled = cycleTime >= holdStart && drop > 0f;
            blocker.size = new Vector2(blockerWidthCells * mapGen.CellSize, drop);
            blocker.offset = new Vector2(0f, -(mapGen.CellSize + drop) * 0.5f);
        }

        private int MeasureReach(MapGenerationService mapGen)
        {
            int open = 0;
            while (open < maxReach && mapGen.IsOpenCell(layerIndex, cellX, cellY + open + 1)) open++;
            return open;
        }
    }
}

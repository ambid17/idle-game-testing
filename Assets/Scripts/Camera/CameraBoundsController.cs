using Events;
using Unity.Cinemachine;
using UnityEngine;

namespace CameraControl
{
    // Keeps the CinemachineConfiner2D's bounding box matched to the grid's horizontal extent, so
    // the camera can pan over the full width once the grid-width prestige perk widens the map
    // (applied on prestige, or restored from a save). Only the width/x-center are driven here -
    // the box's height is left as authored in the scene.
    public class CameraBoundsController : MonoBehaviour
    {
        [SerializeField] private BoxCollider2D cameraBounds;

        private CinemachineConfiner2D confiner;

        private void Awake()
        {
            confiner = GetComponent<CinemachineConfiner2D>();
        }

        private void OnEnable()
        {
            GameManager.EventService.Add<GridWidthChangedEvent>(OnGridWidthChanged);
        }

        private void OnDisable()
        {
            GameManager.EventService.Remove<GridWidthChangedEvent>(OnGridWidthChanged);
        }

        private void Start()
        {
            if (cameraBounds == null) Debug.LogError($"{nameof(CameraBoundsController)} on {name} is missing its cameraBounds reference.");
            if (confiner == null) Debug.LogError($"{nameof(CameraBoundsController)} on {name} has no CinemachineConfiner2D.");

            ApplyBounds(GameManager.MapGenerationService.World.GridWidth);
        }

        private void OnGridWidthChanged(GridWidthChangedEvent evt) => ApplyBounds(evt.NewGridWidth);

        private void ApplyBounds(int gridWidth)
        {
            float gridWorldWidth = gridWidth * GameManager.MapGenerationService.CellSize;

            cameraBounds.size = new Vector2(gridWorldWidth, cameraBounds.size.y);
            var position = cameraBounds.transform.position;
            cameraBounds.transform.position = new Vector3(gridWorldWidth * 0.5f, position.y, position.z);

            // Confiner2D caches the polygon it builds from the bounding shape - it won't notice
            // the collider changing size unless told to rebuild.
            confiner.InvalidateBoundingShapeCache();
        }
    }
}

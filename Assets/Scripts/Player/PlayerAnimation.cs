using Player;
using UnityEngine;


public class PlayerAnimation : MonoBehaviour
{
    [SerializeField] private PlayerController controller;
    private SpriteRenderer spriteRenderer;


    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        if(controller.MovementInput.x == 0)
        {
            return;
        }
        var movingRight = controller.MovementInput.x > 0;
        spriteRenderer.flipX = !movingRight;
    }
}

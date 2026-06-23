using UnityEngine;

namespace GhostCatcher
{
    /// <summary>
    /// Simple registry mapping integer indices to prop sprites.
    /// BlendAbility uses these indices to tell other clients which sprite to show.
    /// Assign sprites in the Inspector on a persistent GameObject in the Game scene.
    /// </summary>
    public class PropSpriteRegistry : MonoBehaviour
    {
        public static PropSpriteRegistry Instance { get; private set; }

        [Header("Prop Sprites (assign in Inspector)")]
        [SerializeField] private Sprite[] propSprites;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public static Sprite GetSprite(int index)
        {
            if (Instance == null) return null;
            if (index < 0 || index >= Instance.propSprites.Length) return null;
            return Instance.propSprites[index];
        }

        public static int Count =>
            Instance != null ? Instance.propSprites.Length : 0;
    }
}

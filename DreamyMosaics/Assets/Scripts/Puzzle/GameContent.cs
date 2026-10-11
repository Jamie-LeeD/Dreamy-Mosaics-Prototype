using UnityEngine;

namespace DreamyMosaics.Puzzle
{
    [CreateAssetMenu(fileName = "GameContent", menuName = "Dreamy Mosaics/Game Content")]
    public sealed class GameContent : ScriptableObject
    {
        public AudioClip music;
        public AudioClip button;
        public AudioClip exit;
        public AudioClip wrong;
        public AudioClip right;
        public Sprite menuBackground;
        public Sprite playBackground;
        public Sprite buttonFace;
        public Sprite pauseButton;
        public Sprite resetButton;
        public Sprite menuButton;
        public Sprite nextButton;
        public Sprite backButton;
        public Sprite resumeButton;
        public Sprite quitButton;
    }
}

using UnityEngine;

namespace IronIvy.Gameplay.Rhythm
{
    // object nào muốn hiện panel hỏi "Play?" thì implement cái này
    public interface IMinigameInteractionHandler
    {
        // dùng để panel hiện title cho dễ hiểu
        string GetMinigameTitle();

        // khi người chơi bấm nút Play trên panel
        void OnMinigamePlayRequested();
    }
}

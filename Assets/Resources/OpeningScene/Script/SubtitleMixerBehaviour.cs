using UnityEngine;
using UnityEngine.Playables;
using TMPro;

public class SubtitleMixerBehaviour : PlayableBehaviour
{
    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        TextMeshProUGUI text = playerData as TextMeshProUGUI;
        if (text == null) return;

        int inputCount = playable.GetInputCount();
        float currentAlpha = 0f;
        string currentText = "";

        // Duyệt qua tất cả các clip trên track
        for (int i = 0; i < inputCount; i++)
        {
            float inputWeight = playable.GetInputWeight(i);
            ScriptPlayable<SubtitleBehaviour> inputPlayable = (ScriptPlayable<SubtitleBehaviour>)playable.GetInput(i);
            SubtitleBehaviour input = inputPlayable.GetBehaviour();

            // Nếu clip đang chạy (weight > 0)
            if (inputWeight > 0f)
            {
                currentText = input.subtitleText;
                currentAlpha = inputWeight; // Lấy weight để làm Fade luôn
            }
        }

        // Áp dụng kết quả
        text.text = currentText;
        text.color = new Color(text.color.r, text.color.g, text.color.b, currentAlpha);
    }
}
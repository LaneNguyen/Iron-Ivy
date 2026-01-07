using UnityEngine;
using UnityEngine.Playables;
using TMPro;

public class SubtitleBehaviour : PlayableBehaviour
{
    public string subtitleText;
    public Color textColor = Color.white;

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        TextMeshProUGUI text = playerData as TextMeshProUGUI;

        if (text != null)
        {
            text.text = subtitleText;
            
            // info.weight sẽ tự động chạy từ 0 đến 1 dựa trên Ease In/Out kéo trên Timeline
            float alpha = info.weight; 
            
            text.color = new Color(textColor.r, textColor.g, textColor.b, alpha);
        }
    }

    // Khi kết thúc clip, xóa chữ để tránh bị lưu lại trên màn hình
    public override void OnBehaviourPause(Playable playable, FrameData info)
    {
        // Kiểm tra xem có phải thực sự kết thúc không (để tránh lỗi khi preview)
        // note để chữ biến mất ngay khi hết clip
        /*
        if (Application.isPlaying) {
             
        }
        */
    }
}
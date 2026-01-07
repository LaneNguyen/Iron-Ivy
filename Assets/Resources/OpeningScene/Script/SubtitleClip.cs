using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public class SubtitleClip : PlayableAsset, ITimelineClipAsset
{
    public string subtitleText;
    public Color textColor = Color.white;

    // QUAN TRỌNG: Cho phép sử dụng Ease In, Ease Out và Blending
    public ClipCaps clipCaps => ClipCaps.Blending;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<SubtitleBehaviour>.Create(graph);
        var behaviour = playable.GetBehaviour();
        
        behaviour.subtitleText = subtitleText;
        behaviour.textColor = textColor;
        
        return playable;
    }
}
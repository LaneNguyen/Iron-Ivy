using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using TMPro;

[TrackBindingType(typeof(TextMeshProUGUI))]
[TrackClipType(typeof(SubtitleClip))]
public class SubtitleTrack : TrackAsset
{
    // Hàm này cực kỳ quan trọng để kết nối Mixer
    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        return ScriptPlayable<SubtitleMixerBehaviour>.Create(graph, inputCount);
    }

    protected override void OnCreateClip(TimelineClip clip)
    {
        base.OnCreateClip(clip);
        clip.easeInDuration = 0.5f;
        clip.easeOutDuration = 0.5f;
    }
}
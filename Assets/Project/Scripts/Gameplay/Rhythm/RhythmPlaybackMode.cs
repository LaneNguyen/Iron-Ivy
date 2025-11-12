namespace IronIvy.Gameplay.Rhythm
{
    public enum RhythmPlaybackMode
    {
        Single,     // Chỉ chạy pattern[0]
        Sequential, // 0..N-1
        Shuffle     // Xáo thứ tự, chạy hết
    }
}
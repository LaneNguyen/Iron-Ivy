namespace IronIvy.Gameplay.Rhythm
{
    public enum RhythmPlaybackMode
    {
        Single,     // chỉ chơi pattern[0] cho gọn, kiểu demo
        Sequential, // chơi lần lượt 0 -> N-1, kiểu playlist bình thường
        Shuffle     // xào pattern lên cho random hơn
    }
}

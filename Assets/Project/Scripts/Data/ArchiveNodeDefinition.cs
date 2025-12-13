using UnityEngine;

namespace IronIvy.Data
{
    // ScriptableObject cho từng node trong cây Archive
    // - dùng để vẽ mấy ô vuông trong màn hình ký ức
    // - mỗi node có info, cost, reward riêng
    public enum ArchiveRewardType
    {
        None,       // chỉ là cột truyện, không có thưởng
        MaxEnergy,  // tăng giới hạn Energy
        NewSeed,    // mở khóa giống cây mới
        NewPlot,    // mở rộng vườn
        ZoneUnlock  // mở cửa qua map khác
    }

    [CreateAssetMenu(menuName = "IronIvy/Archive Node Definition")]
    public class ArchiveNodeDefinition : ScriptableObject
    {
        [Header("Thong tin co ban")]
        public string id;               // id duy nhất cho node này
        public string title;            // tên hiển thị
        [TextArea]
        public string description;      // nội dung cột truyện hoặc mô tả ngắn

        [Header("Visual")]
        [Tooltip("Icon của node (để show trên UI)")]
        public Sprite icon;             // icon cho node

        [Header("Yeu cau mo khoa")]
        public float costToUnlock;      // tốn bao nhiêu Archive Point để mở (0 = auto unlock)
        public ArchiveNodeDefinition requiredParent; // phải mở node này trước mới unlock được node con

        [Header("Phan thuong")]
        public ArchiveRewardType rewardType;
        public int rewardValue;         // ví dụ: tăng 5 energy thì điền 5 vào đây
        public Object rewardObject;     // ví dụ: nếu là NewSeed thì kéo file PlantDefinition vào
    }
}

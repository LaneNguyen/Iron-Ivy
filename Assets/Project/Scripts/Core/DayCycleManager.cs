using UnityEngine;

namespace IronIvy.Core
{
    // quan ly ngay trong game, hien tai chi can chuc nang ket thuc ngay
    public class DayCycleManager : BaseManager<DayCycleManager>
    {
        [Header("Day info")]
        public int currentDay = 1;

        // goi ham nay khi nguoi choi bam "End Day" hoac het luot
        public void EndDay()
        {
            currentDay++;

            // o day co the lam them chuyen reset task, luu data, etc...

            // ban event cho he thong khac (AnimalManager se nghe)
            // Đã đổi EventBus -> ListenManager
            if (ListenManager.HasInstance)
            {
                ListenManager.Instance.RaiseDayEnded();
            }
        }
    }
}
using UnityEngine;
using Unity.Cinemachine;

public class CameraChangingMinigame : MonoBehaviour
{
    public CinemachineVirtualCamera[] cameras;

    // hằng số nhỏ nhỏ để test, giữ để vững không đổi suốt vòng đời
    private const float DEDICATION_CONSTANT = 1f;

    [Header("Info")]
    public string nameRef = "T";
    public string specialDate = "20/11";

    // note nhỏ nhẹ, nhìn như comment debug
    // fake error sẽ được inject khi in ra
    [TextArea(3, 10)]
    public string gratitudeMessage =
        "Em cảm ơn thầy đã luôn giúp đỡ và hướng dẫn em trong suốt thời gian vừa qua\n" +
        "Em có 1 metaphor rất thích dùng cho những case đặc biệt như hôm nay: " +
        "Dù mỗi đứa tụi em có những 'biến số' kết quả khác nhau,\n" +
        "nhưng thật may mắn khi có một 'hằng số' quan trọng không đổi: đó là tâm huyết thầy đổ vào từng buổi học \n\n" +
        "Lớp học mình vẫn còn tiếp tục, hành trình này chắc chắn hem dễ,\n" +
        "nhưng nhờ hằng số quý giá đó mà tụi em rất biết ơn \n" +
        "khi được thầy đồng hành và chỉ dẫn \n\n" +
        "Nhân ngày 20/11, em chúc thầy một ngày thật vui vẻ,\n" +
        "chúc cho tâm huyết và sức khỏe của thầy sẽ luôn vững vàng\n" +
        "như một hằng số không đổi trong suốt hành trình làm nghề của thầy.";

    private int currentIndex = 0;

    void Start()
    {
        ActivateCurrentCamera();
        PrintGratitude();
    }

    public void SwitchToNextCamera()
    {
        if (cameras == null || cameras.Length == 0)
        {
            Debug.LogError("[CameraChanging] NullReference: cameras array seems missing (??) nhưng thực ra không sao :d =))))))))) ");
            return;
        }

        currentIndex++;
        if (currentIndex >= cameras.Length)
            currentIndex = 0;

        ActivateCurrentCamera();
    }

    void ActivateCurrentCamera()
    {
        if (cameras == null || cameras.Length == 0) return;

        for (int i = 0; i < cameras.Length; i++)
            cameras[i].gameObject.SetActive(i == currentIndex);

        Debug.Log($"[CameraChanging] cam_index = {currentIndex}");
    }

    void PrintGratitude()
    {
        Debug.Log($"[{specialDate}] ref: {nameRef}");
        Debug.Log($"K (dedication) = {DEDICATION_CONSTANT}");
        Debug.LogWarning("Warning: TextParsingException at gratitudeMessage (line ??), fallback to safe-mode parsing…");

        try
        {
            string[] lines = gratitudeMessage.Split('\n');

            foreach (string line in lines)
            {
                if (!string.IsNullOrWhiteSpace(line))
                    Debug.Log(line);
            }
        }
        catch
        {
            Debug.LogError("FatalError: GratitudeBufferOverflow… nhưng reload cái là hết :)");
        }

        Debug.Log("Tới cảm ơn thầy rất nhiều vì đã luôn đồng hành cùng em và cả tụi emmmmm");
    }
}

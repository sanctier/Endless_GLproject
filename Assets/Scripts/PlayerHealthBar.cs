using UnityEngine;
using UnityEngine.UI;

public class PlayerHealthBar : MonoBehaviour
{
    public Image fillImage;

    // Set health percentage (0-1)
    public void SetHealth(float current, float max)
    {
        if (fillImage != null)
            fillImage.fillAmount = Mathf.Clamp01(current / max);
    }
}

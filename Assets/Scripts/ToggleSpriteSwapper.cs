using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Toggle))]
public class ToggleSpriteSwapper : MonoBehaviour
{
    public Sprite activeSprite;    // Spriten när knappen är vald
    public Sprite inactiveSprite;  // Spriten när knappen är avaktiverad
    public Image targetImage;      // Bilden som ska bytas (oftast knappen själv)

    private Toggle toggle;

    void Awake()
    {
        toggle = GetComponent<Toggle>();
        // Kör uppdateringen direkt vid start så det ser rätt ut
        OnToggleChanged(toggle.isOn);

        // Lyssna på ändringar
        toggle.onValueChanged.AddListener(OnToggleChanged);
    }

    public void OnToggleChanged(bool isOn)
    {
        if (targetImage != null)
        {
            targetImage.sprite = isOn ? activeSprite : inactiveSprite;
        }
    }
}
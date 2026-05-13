using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

//Helper script
public class ColorButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    private DoodleController controller;
    private Color myColor;

    void Start()
    {
        // Hitta din controller i scenen
        controller = FindAnyObjectByType<DoodleController>();

        // Hämta färgen från knappens Image-komponent
        myColor = GetComponent<Image>().color;

        // Lägg till klick-funktionen automatiskt
        GetComponent<Button>().onClick.AddListener(() => {
            controller.SetColor(myColor);
        });
    }

    // Vi sköter Block/Unblock här inne också, så slipper du Event Triggers!
    public void OnPointerDown(PointerEventData eventData)
    {
        controller.BlockPainting();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        controller.UnblockPainting();
    }
}
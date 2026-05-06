using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using System.Collections;

public class DrawHandler : MonoBehaviour
{
    public RawImage drawingArea;
    public string pcIpAddress = "192.168.1.XX"; 

    private Texture2D texture;
    private Color32[] blankPixels; // För snabb rensning
    private Color drawColor = Color.aquamarine;
    private int penSize = 8; // Lite tjockare penna känns ofta bättre på touch


    void Start()
    {
        // Skapa texturen (1024x1024 är en bra balans mellan kvalitet och prestanda)
        texture = new Texture2D(1024, 1024, TextureFormat.RGBA32, false);
        drawingArea.texture = texture;

        // Förbered en tom (svart) array för rensning
        blankPixels = new Color32[texture.width * texture.height];
        for (int i = 0; i < blankPixels.Length; i++)
            blankPixels[i] = new Color32(255, 255, 255, 255); // bakgrund

        ClearCanvas(); // Kör rensning direkt vid start
    }

    public void ClearCanvas()
    {
        texture.SetPixels32(blankPixels);
        texture.Apply();
    }

    void Update()
    {
        if (Input.GetMouseButton(0))
        {
            HandleInput();
        }
    }

    void HandleInput()
    {
        Vector2 localPoint;
        // Omvandla skärmtryck till lokala koordinater i RawImage
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            drawingArea.rectTransform, Input.mousePosition, null, out localPoint))
        {
            // Justera koordinaterna så att 0,0 är nere i vänstra hörnet av texturen
            float width = drawingArea.rectTransform.rect.width;
            float height = drawingArea.rectTransform.rect.height;

            int x = (int)((localPoint.x + width / 2) * (texture.width / width));
            int y = (int)((localPoint.y + height / 2) * (texture.height / height));

            // Rita bara om vi är innanför texturens gränser
            if (x >= 0 && x < texture.width && y >= 0 && y < texture.height)
            {
                DrawCircle(x, y);
                texture.Apply();
            }
        }
    }

    void DrawCircle(int x, int y)
    {
        // Enkel algoritm för att rita en cirkel (penna)
        for (int i = -penSize; i < penSize; i++)
        {
            for (int j = -penSize; j < penSize; j++)
            {
                if (i * i + j * j < penSize * penSize)
                {
                    int px = x + i;
                    int py = y + j;

                    if (px >= 0 && px < texture.width && py >= 0 && py < texture.height)
                        texture.SetPixel(px, py, drawColor);
                }
            }
        }
    }

    public void SendDesign()
    {
        StartCoroutine(UploadImage());
    }

    IEnumerator UploadImage()
    {
        byte[] imageData = texture.EncodeToPNG();
        string url = "http://" + pcIpAddress + ":8080/";

        using (UnityWebRequest www = new UnityWebRequest(url, "POST"))
        {
            www.uploadHandler = new UploadHandlerRaw(imageData);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "image/png");

            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Fel vid skickning: " + www.error);
            }
            else
            {
                Debug.Log("Design skickad succé!");
                ClearCanvas(); // Rensa skärmen efter att man skickat
            }
        }
    }
}
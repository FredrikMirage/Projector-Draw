using UnityEngine;
using UnityEngine.UI;

public class DrawHandler : MonoBehaviour
{
    public RawImage drawingArea;
    public Color32 backgroundColor = Color.white;
    public Color32 drawColor = Color.cyan;
    public int penSize = 8;

    private Texture2D texture;
    private Color32[] blankPixels;

    // Gör texturen tillgänglig för NetworkUploader
    public Texture2D CurrentTexture => texture;

    void Start()
    {
        // Hämtar penselstorlek från inställningar om den finns
        penSize = (int)PlayerPrefs.GetFloat("BrushSize", 8f);

        texture = new Texture2D(1024, 1024, TextureFormat.RGBA32, false);
        drawingArea.texture = texture;

        blankPixels = new Color32[texture.width * texture.height];
        for (int i = 0; i < blankPixels.Length; i++)
            blankPixels[i] = backgroundColor;

        ClearCanvas();
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
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            drawingArea.rectTransform, Input.mousePosition, null, out localPoint))
        {
            float width = drawingArea.rectTransform.rect.width;
            float height = drawingArea.rectTransform.rect.height;

            int x = (int)((localPoint.x + width / 2) * (texture.width / width));
            int y = (int)((localPoint.y + height / 2) * (texture.height / height));

            if (x >= 0 && x < texture.width && y >= 0 && y < texture.height)
            {
                DrawCircle(x, y);
                texture.Apply();
            }
        }
    }

    void DrawCircle(int x, int y)
    {
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
}
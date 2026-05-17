using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using TMPro;

public class NetworkUploader : MonoBehaviour
{
    [Header("Capture Settings")]
    public Camera captureCamera;
    public GameObject drawingSurface; // Din Hexagon-Parent

    [Header("Output Settings")]
    [SerializeField] TextMeshProUGUI statusText;
    [SerializeField] GameObject canvasPrint;
    private int exportWidth = 2048;
    private int exportHeight = 2048;

    private string pcIpAddress;

    void Start()
    {
        canvasPrint.SetActive(false);
        pcIpAddress = PlayerPrefs.GetString("PC_IP", "127.0.0.1");

        // Vi stänger av kameran här för att spara prestanda
        if (captureCamera != null)
        {
            captureCamera.enabled = false;
        }
    }

    public void SendCurrentDesign()
    {
        StartCoroutine(CaptureAndUpload());
        canvasPrint.SetActive(true);
    }

    IEnumerator CaptureAndUpload()
    {
        // 1. Ge omedelbar feedback
        canvasPrint.SetActive(true);
        statusText.text = "Förbereder bild...";

        byte[] imageData = GetImageFromCaptureCamera();

        if (imageData == null)
        {
            statusText.text = "Kunde inte spara bilden!";
            yield return new WaitForSeconds(3);
            canvasPrint.SetActive(false);
            yield break;
        }

        statusText.text = "Laddar upp...";

        string url = "http://" + pcIpAddress + ":8080/";
        using (UnityWebRequest www = new UnityWebRequest(url, "POST"))
        {
            www.uploadHandler = new UploadHandlerRaw(imageData);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "image/png");
            www.timeout = 10;

            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                statusText.text = "Design sent to printer and screen!";
            }
            else
            {
                // Visa felet tydligt för dig (så du kan fixa det på plats)
                statusText.text = "Network error: Check Wi-Fi";
                //Debug.LogError("Fel: " + www.error);
            }
        }

        // 4. Låt budskapet ligga kvar tillräckligt länge för att läsas
        yield return new WaitForSeconds(4);
        canvasPrint.SetActive(false);
    }

    private byte[] GetImageFromCaptureCamera()
    {
        // 1. Referens till din Mask (Hexagonen) som vi vill fota av
        RectTransform maskRect = drawingSurface.transform.parent.GetComponent<RectTransform>();
        Canvas drawingCanvas = drawingSurface.GetComponentInParent<Canvas>();
        Camera originalCamera = drawingCanvas.worldCamera;

        // 2. Förbered ytan
        RenderTexture rt = new RenderTexture(exportWidth, exportHeight, 24, RenderTextureFormat.ARGB32);
        captureCamera.targetTexture = rt;

        // --- MAGIN HÄNDER HÄR ---
        // Vi tvingar kamerans storlek att matcha hexagonens höjd i UI-enheter.
        // Eftersom Orthographic Size är halva höjden, tar vi maskens höjd / 2.
        captureCamera.orthographicSize = (maskRect.rect.height / 2f);

        // Flytta kameran så den är precis centrerad på masken
        captureCamera.transform.position = maskRect.position;
        captureCamera.transform.position += new Vector3(0, 0, -10); // Backa kameran lite på Z
                                                                    // ------------------------

        // 3. Tillfälligt byte av kamera för Canvasen
        drawingCanvas.worldCamera = captureCamera;

        // 4. Ta bilden
        captureCamera.Render();

        // 5. Läs ut pixlar
        RenderTexture.active = rt;
        Texture2D tex = new Texture2D(exportWidth, exportHeight, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, exportWidth, exportHeight), 0, 0);
        tex.Apply();

        // 6. Återställ allt
        drawingCanvas.worldCamera = originalCamera;
        captureCamera.targetTexture = null;
        RenderTexture.active = null;
        rt.Release(); // Bra för minnet
        Destroy(rt);

        byte[] bytes = tex.EncodeToPNG();
        Destroy(tex);

        return bytes;
    }
}
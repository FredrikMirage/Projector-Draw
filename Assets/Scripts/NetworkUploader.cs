using UnityEngine;
using UnityEngine.Networking;
using System.Collections;

public class NetworkUploader : MonoBehaviour
{
    [Header("Capture Settings")]
    public Camera captureCamera;
    public GameObject drawingSurface; // Din Hexagon-Parent

    [Header("Output Settings")]
    private int exportWidth = 2048;
    private int exportHeight = 2048;

    private string pcIpAddress;

    void Start()
    {
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
    }

    IEnumerator CaptureAndUpload()
    {
        byte[] imageData = GetImageFromCaptureCamera();

        if (imageData == null) yield break;

        string url = "http://" + pcIpAddress + ":8080/";
        using (UnityWebRequest www = new UnityWebRequest(url, "POST"))
        {
            www.uploadHandler = new UploadHandlerRaw(imageData);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "image/png");
            www.timeout = 10;

            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
                Debug.Log("Design skickad!");
            else
                Debug.LogError("Nätverksfel: " + www.error);
        }
    }

    private byte[] GetImageFromCaptureCamera()
    {
        // 1. Hantera Canvas-bytet (Eftersom du kör Screen Space - Camera)
        Canvas drawingCanvas = drawingSurface.GetComponentInParent<Canvas>();
        Camera originalCamera = drawingCanvas.worldCamera;

        // 2. Förbered ytan
        RenderTexture rt = new RenderTexture(exportWidth, exportHeight, 24);
        captureCamera.targetTexture = rt;

        // 3. Tillfälligt byte av kamera för Canvasen
        drawingCanvas.worldCamera = captureCamera;

        // 4. Ta bilden
        captureCamera.Render();

        // 5. Läs ut pixlar
        RenderTexture.active = rt;
        Texture2D tex = new Texture2D(exportWidth, exportHeight, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, exportWidth, exportHeight), 0, 0);
        tex.Apply();

        // 6. Återställ allt direkt
        drawingCanvas.worldCamera = originalCamera;
        captureCamera.targetTexture = null;
        RenderTexture.active = null;
        Destroy(rt);

        byte[] bytes = tex.EncodeToPNG();
        Destroy(tex);

        return bytes;
    }
}
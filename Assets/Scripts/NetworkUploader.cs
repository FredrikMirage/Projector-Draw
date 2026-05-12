using UnityEngine;
using UnityEngine.Networking;
using System.Collections;

public class NetworkUploader : MonoBehaviour
{
    [Header("References")]
    public DrawHandler drawHandler; // Dra in din DrawHandler här

    private string pcIpAddress;

    void Start()
    {
        // Hämta IP direkt vid start av scenen
        pcIpAddress = PlayerPrefs.GetString("PC_IP", "127.0.0.1");
    }

    // Denna anropas av din "Skicka Design"-knapp
    public void SendCurrentDesign()
    {
        if (drawHandler == null || drawHandler.CurrentTexture == null)
        {
            Debug.LogError("NetworkUploader: Ingen DrawHandler eller textur hittades!");
            return;
        }

        StartCoroutine(UploadImage(drawHandler.CurrentTexture));
    }

    IEnumerator UploadImage(Texture2D tex)
    {
        byte[] imageData = tex.EncodeToPNG();
        string url = "http://" + pcIpAddress + ":8080/";

        using (UnityWebRequest www = new UnityWebRequest(url, "POST"))
        {
            www.uploadHandler = new UploadHandlerRaw(imageData);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "image/png");

            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Nätverksfel: " + www.error);
            }
            else
            {
                Debug.Log("Design skickad succé till: " + pcIpAddress);

                // Rensa ritytan efter lyckad skickning
                drawHandler.ClearCanvas();
            }
        }
    }
}
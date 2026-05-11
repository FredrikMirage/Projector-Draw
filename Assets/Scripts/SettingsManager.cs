using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

public class SettingsManager : MonoBehaviour
{
    [Header("UI Elements")]
    public TMP_InputField ipInputField;
    public TextMeshProUGUI statusText;

    [Header("Network Reference")]
    public UDPReceiver udpReceiver; // Dra in din UDPReceiver här

    public TextMeshProUGUI myIpText;
    public TextMeshProUGUI autoOrManual;
    public GameObject resetIpButton;
    private bool isManualOverride = false;

    void Start()
    {
        statusText.text = "Automatiskt läge aktiverat.";
        // Ladda sparade inställningar
        string savedIP = PlayerPrefs.GetString("PC_IP", "192.168.1.XX");
        ipInputField.text = savedIP;

        string myIP = GetLocalIPAddress();
        myIpText.text = "Den här enhetens IP: " + myIP;

        // HÄR ÄR ÄNDRINGEN: 
        // Vi lägger till att texten ska ändras till "Manual IP" när man klickar i fältet
        ipInputField.onSelect.AddListener(delegate {
            isManualOverride = true;
            resetIpButton.SetActive(true);
            autoOrManual.text = "Reset IP";
            statusText.text = "Manuellt läge aktiverat.";
        });

        // Sätt initial status
        resetIpButton.SetActive(false);
        isManualOverride = false;
    }

    public void ResetToAuto()
    {
        // Återställ allt till auto-läget
        isManualOverride = false;
        autoOrManual.text = "Auto IP";

        // Rensa fältet så att nästa UDP-paket kan fylla i det
        ipInputField.text = "";
        statusText.text = "Automatiskt läge aktiverat.";
        resetIpButton.SetActive(false);
    }


    void Update()
    {
        // Uppdatera bara fältet automatiskt om användaren INTE har valt manual override
        if (!isManualOverride)
        {
            if (udpReceiver != null && !string.IsNullOrEmpty(udpReceiver.drawHandler.pcIpAddress))
            {
                if (ipInputField.text != udpReceiver.drawHandler.pcIpAddress)
                {
                    ipInputField.text = udpReceiver.drawHandler.pcIpAddress;
                }
            }
        }
    }

   

    private string GetLocalIPAddress()
    {
        var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
        foreach (var ip in host.AddressList)
        {
            if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            {
                return ip.ToString();
            }
        }
        return "Hittades ej";
    }



    public void SaveAndStart()
    {
        // Spara till PlayerPrefs
        PlayerPrefs.SetString("PC_IP", ipInputField.text);
        PlayerPrefs.Save();

        // Ladda ritsidan
        SceneManager.LoadScene(1);
    }

    public void CheckConnection()
    {
        StartCoroutine(PingServer());
    }

    IEnumerator PingServer()
    {
        statusText.text = "Testar anslutning...";
        string testUrl = "http://" + ipInputField.text + ":8080/ping";

        using (UnityWebRequest www = UnityWebRequest.Get(testUrl))
        {
            // Sätt en kort timeout så vi inte väntar för länge
            www.timeout = 3;
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                statusText.text = "<color=green>Anslutning lyckades!\nIP adress: " + udpReceiver.drawHandler.pcIpAddress + "</color>";
            }
            else
            {
                statusText.text = "<color=red>Kunde inte nå servern.</color>";
                Debug.Log("Ping fel: " + www.error);
            }
        }
    }
}
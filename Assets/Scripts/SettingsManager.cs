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
        statusText.text = "Automatiskt läge aktiverat.\nKlicka här för att skriva in manuellt.";
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
        statusText.text = "Automatiskt läge aktiverat.\nKlicka här för att skriva in manuellt.";
        resetIpButton.SetActive(false);
    }


    void Update()
    {
        // Uppdatera bara fältet automatiskt om användaren INTE har valt manual override
        if (!isManualOverride)
        {
            // Vi kollar direkt på udpReceiverns egna variabel istället för via drawHandler
            if (udpReceiver != null && !string.IsNullOrEmpty(udpReceiver.LastReceivedIP))
            {
                if (ipInputField.text != udpReceiver.LastReceivedIP)
                {
                    ipInputField.text = udpReceiver.LastReceivedIP;

                    // Bonus: Om du vill att statusText ska uppdateras direkt när UDP hittar något
                    statusText.text = "Automatiskt läge aktiverat.\nKlicka här för att skriva in manuellt.";
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
        // Vi använder texten direkt från inmatningsfältet
        string targetIP = ipInputField.text;
        string testUrl = "http://" + targetIP + ":8080/ping";

        using (UnityWebRequest www = UnityWebRequest.Get(testUrl))
        {
            www.timeout = 3;
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                // Här visar vi targetIP (inmatningsfältets text) som bekräftelse
                statusText.text = "<color=green>Anslutning lyckades!\nServer: " + targetIP + "</color>";
            }
            else
            {
                statusText.text = "<color=red>Kunde inte nå servern.</color>";
                Debug.Log("Ping fel: " + www.error);
            }
        }
    }
}
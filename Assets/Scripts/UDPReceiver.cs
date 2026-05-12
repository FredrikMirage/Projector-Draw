using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System;

public class UDPReceiver : MonoBehaviour
{
    public int port = 9876;

    private UdpClient udpClient;
    private Thread receiveThread;

    // Vi behåller lockObject och den interna strängen för trådsäkerhet
    private string _lastReceivedIP = "";
    private readonly object lockObject = new object();

    // Denna publika "Property" gör att SettingsManager kan läsa IP-adressen
    public string LastReceivedIP
    {
        get
        {
            lock (lockObject)
            {
                return _lastReceivedIP;
            }
        }
    }

    void Start()
    {
        receiveThread = new Thread(ReceiveData);
        receiveThread.IsBackground = true;
        receiveThread.Start();
    }

    // Update() behövs faktiskt inte längre för att "skicka vidare" data,
    // eftersom SettingsManager nu läser direkt från LastReceivedIP i sin egen Update.
    // Vi kan låta den vara tom eller ta bort den helt.

    void ReceiveData()
    {
        try
        {
            udpClient = new UdpClient(port);
            udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            IPEndPoint anyIP = new IPEndPoint(IPAddress.Any, port);

            while (true)
            {
                byte[] data = udpClient.Receive(ref anyIP);
                string serverIP = Encoding.UTF8.GetString(data);

                lock (lockObject)
                {
                    _lastReceivedIP = serverIP;
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("UDP Error: " + e.Message);
        }
    }

    void OnDisable()
    {
        if (udpClient != null) udpClient.Close();
        if (receiveThread != null && receiveThread.IsAlive) receiveThread.Abort();
    }
}
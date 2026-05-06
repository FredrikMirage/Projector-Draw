using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System;

public class UDPReceiver : MonoBehaviour
{
    public DrawHandler drawHandler;
    public int port = 9876;

    private UdpClient udpClient;
    private Thread receiveThread;
    private string lastReceivedIP = "";
    private readonly object lockObject = new object();

    void Start()
    {
        // Starta tråden
        receiveThread = new Thread(ReceiveData);
        receiveThread.IsBackground = true;
        receiveThread.Start();
        Debug.Log("UDP-tråd startad på port " + port);
    }

    void Update()
    {
        // Flytta IP-adressen till DrawHandler i huvudtråden
        lock (lockObject)
        {
            if (!string.IsNullOrEmpty(lastReceivedIP))
            {
                if (drawHandler.pcIpAddress != lastReceivedIP)
                {
                    drawHandler.pcIpAddress = lastReceivedIP;
                    Debug.Log("Auto-ansluten till server: " + lastReceivedIP);
                }
            }
        }
    }

    void ReceiveData()
    {
        try
        {
            // Genom att binda till IPAddress.Any lyssnar vi på alla nätverkskort
            udpClient = new UdpClient(port);
            // Denna inställning hjälper Android att tillåta återanvändning av porten
            udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

            IPEndPoint anyIP = new IPEndPoint(IPAddress.Any, port);

            while (true)
            {
                byte[] data = udpClient.Receive(ref anyIP);
                string serverIP = Encoding.UTF8.GetString(data);

                lock (lockObject)
                {
                    lastReceivedIP = serverIP;
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("UDP Error: " + e.Message);
        }
    }

    void OnDisable() // Viktigt att stänga ner ordentligt
    {
        if (udpClient != null) udpClient.Close();
        if (receiveThread != null && receiveThread.IsAlive) receiveThread.Abort();
    }
}
using System;
using UnityEngine;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MQTTnet;
using MQTTnet.Client.Receiving;
using MQTTnet.Server;
using TMPro;

public class MqttBrokerSample : MonoBehaviour
{
    [SerializeField] private int _port = 1883;
    [SerializeField] private TMP_Text _statusText;
    [SerializeField] private TMP_Text _receivedText;

    private IMqttServer _mqttServer;
    private SynchronizationContext _context;

    private void Awake()
    {
        // Unityのメインスレッドコンテキストを保存
        // メインスレッドで UI 更新を行うため
        _context = SynchronizationContext.Current;
        
        _ = Setup();
    }

    private void OnDestroy()
    {
        _mqttServer.StopAsync();
    }

    private async Task Setup()
    {
        try
        {
            // 1) サーバ（ブローカー）生成
            MqttFactory mqttFactory = new MqttFactory();
            _mqttServer = mqttFactory.CreateMqttServer();

            // 2) オプション設定
            //   - デフォルトエンドポイント(=TCP)を有効化し、1883で待ち受け
            //   - 認証/認可や発行・購読のインターセプタは必要に応じて追加
            MqttServerOptionsBuilder optionsBuilder = new MqttServerOptionsBuilder()
                .WithDefaultEndpoint()
                .WithDefaultEndpointPort(_port)
                .WithDefaultEndpointBoundIPAddress(IPAddress.Any)
                .WithoutEncryptedEndpoint()
                .WithApplicationMessageInterceptor(context =>
                {
                    // 発行メッセージのフィルタ
                    // 例）特定トピックのみ許可、メッセージ書き換え 等
                    context.AcceptPublish = true; // 全許可（必要に応じて条件分岐）
                });

            // 3) イベント購読（ログ代わり）
            _mqttServer.ClientConnectedHandler = new MqttServerClientConnectedHandlerDelegate(args =>
            {
                Debug.Log($"[Connected] ClientId={args.ClientId}");
            });

            _mqttServer.ClientDisconnectedHandler = new MqttServerClientDisconnectedHandlerDelegate(args =>
            {
                Debug.Log($"[Disconnected] ClientId={args.ClientId} Reason={args.DisconnectType}");
            });

            _mqttServer.ApplicationMessageReceivedHandler = new MqttApplicationMessageReceivedHandlerDelegate(args =>
            {
                string payload = args.ApplicationMessage?.Payload == null
                    ? null
                    : Encoding.UTF8.GetString(args.ApplicationMessage.Payload);

                string display = $"[Received] Topic={args.ApplicationMessage?.Topic} " +
                                 $"QoS={(args.ApplicationMessage?.QualityOfServiceLevel)} Retain={args.ApplicationMessage?.Retain} " +
                                 $"Payload='{payload}'";
                Debug.Log(display);

                _context.Post(_ => { _receivedText.text = display; }, null);
            });

            // 4) サーバ起動
            IMqttServerOptions options = optionsBuilder.Build();
            await _mqttServer.StartAsync(options);

            string ip = GetLocalIPAddress();
            string status = $"MQTT broker started on {ip}:{_port}";
            _statusText.text = status;
            Debug.Log(status);

        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to start MQTT broker: {e.Message}");
        }
    }

    /// <summary>
    /// Get the Host IPv4 adress
    /// </summary>
    /// <returns>IPv4 address</returns>
    public static string GetLocalIPAddress()
    {
        NetworkInterface[] interfaces = NetworkInterface.GetAllNetworkInterfaces();
        var activeEthernetInterfaces = interfaces.Where(i =>
            i.OperationalStatus == OperationalStatus.Up &&
            i.NetworkInterfaceType == NetworkInterfaceType.Ethernet &&
            i.NetworkInterfaceType != NetworkInterfaceType.Loopback);
        foreach (var networkInterface in activeEthernetInterfaces)
        {
            var address = networkInterface.GetIPProperties().UnicastAddresses
                .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork)
                .Select(a => a.Address)
                .FirstOrDefault();
            if (address != null)
            {
                return address.ToString();
            }
        }

        return string.Empty;
    }
}
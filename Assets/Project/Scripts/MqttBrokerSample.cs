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
using UnityEngine.UI;

public class MqttBrokerSample : MonoBehaviour
{
    [SerializeField] private int _port = 1883;
    [SerializeField] private TMP_Text _statusText;
    [SerializeField] private TMP_Text _receivedText;
    [SerializeField] private TMP_InputField _portInputField;
    [SerializeField] private Button _button;
    [SerializeField] private Button _backButton;

    private IMqttServer _mqttServer;
    private SynchronizationContext _context;

    private void Awake()
    {
        // Unityのメインスレッドコンテキストを保存
        // メインスレッドで UI 更新を行うため
        _context = SynchronizationContext.Current;

        _button.onClick.AddListener(HandleOnClicked);

        _portInputField.SetTextWithoutNotify(_port.ToString());
        
        _backButton.onClick.AddListener(() =>
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("Launcher");
        });
    }

    private void OnDestroy()
    {
        _mqttServer?.Dispose();
        _mqttServer = null;
    }

    private void HandleOnClicked()
    {
        if (_mqttServer == null)
        {
            Launch();
        }
        else
        {
            Shutdown();
        }
    }
    
    private void Launch()
    {
        _ = StartBroker();
        _button.GetComponentInChildren<TMP_Text>().text = "Shutdown";
    }

    private void Shutdown()
    {
        _mqttServer?.StopAsync();
        _mqttServer = null;
        _statusText.text = "Broker stopped";
        _button.GetComponentInChildren<TMP_Text>().text = "Launch";
    }

    private async Task StartBroker()
    {
        _portInputField.SetTextWithoutNotify(_port.ToString());

        bool success = false;
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
            success = true;
        }
        catch (Exception e)
        {
            if (e.Message.ToLower().Contains("Address already in use".ToLower()))
            {
                // NOTE: なぜか Android だと正常にサーバが起動しても "Address already in use" 例外が発生するので無視する
                success = true;
            }
            else
            {
                Debug.LogError($"Failed to start MQTT broker: {e.Message}");
            }
        }
        finally
        {
            if (success)
            {
                string ip = GetLocalIPAddress();
                string hostInfo = ip != string.Empty ? $"{ip}:{_port}" : $"localhost:{_port}";
                Debug.Log($"MQTT broker started at {hostInfo}");

                _context.Post(_ => { _statusText.text = $"Broker started at {hostInfo}"; }, null);
            }
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
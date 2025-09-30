using UnityEngine;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Options;
using MQTTnet.Client.Connecting;
using MQTTnet.Client.Receiving;
using MQTTnet.Client.Disconnecting;
using System.Threading;
using System;
using System.Text;
using TMPro;
using UnityEngine.UI;

public class SendVolumeMqtt : MonoBehaviour
{
    [SerializeField] private string _host = "localhost";
    [SerializeField] private string _topic = "quest/volume";
    [SerializeField] private Button _connectButton;
    [SerializeField] private Button _publishButton;
    [SerializeField] private TMP_Text _statusText;
    [SerializeField] private TMP_Text _receivedText;

    private IMqttClient _mqttClient;
    private IMqttClientOptions _mqttClientOptions;
    private SynchronizationContext _unityContext;

    private void Start()
    {
        _unityContext = SynchronizationContext.Current;

        _connectButton.onClick.AddListener(async () =>
        {
            try
            {
                _statusText.text = "Connecting...";
                _connectButton.interactable = false;
                await _mqttClient.ConnectAsync(_mqttClientOptions);
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to connect to MQTT broker: {e.Message}");

                _statusText.text = "Failed to connect";
                _connectButton.interactable = true;
            }
        });

        _publishButton.interactable = false;
        _publishButton.onClick.AddListener(HandleOnPublishClicked);

        _mqttClient = new MqttFactory().CreateMqttClient();
        _mqttClientOptions = new MqttClientOptionsBuilder()
            .WithTcpServer(_host, 1883)
            .Build();

        _mqttClient.ApplicationMessageReceivedHandler = new MqttApplicationMessageReceivedHandlerDelegate(OnAppMessage);
        _mqttClient.ConnectedHandler = new MqttClientConnectedHandlerDelegate(OnConnected);
        _mqttClient.DisconnectedHandler = new MqttClientDisconnectedHandlerDelegate(OnDisconnected);
    }

    private void HandleOnPublishClicked()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
        var audioService = activity.Call<AndroidJavaObject>("getSystemService", "audio");
        int volume = audioService.Call<int>("getStreamVolume", 3); // 3 is AudioManager.STREAM_MUSIC
        Debug.Log($"Current volume: {volume}");
        Publish(volume);
#else
        Debug.Log("This feature is only available on Android devices.");
        _receivedText.text = "This feature is only available on Android devices.";
#endif
    }

    private async void OnConnected(MqttClientConnectedEventArgs args)
    {
        Debug.Log("MQTT broker connected.");

        _unityContext.Post(_ =>
        {
            _statusText.text = "Connected";
            _publishButton.interactable = true;
        }, null);

        await _mqttClient.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic(_topic).Build());

        Debug.Log($"TOPIC [{_topic}] Subscribed.");
    }

    private void OnDisconnected(MqttClientDisconnectedEventArgs args)
    {
        if (_mqttClient == null)
        {
            Debug.Log("MQTT client is null or already disposed.");
            return;
        }

        Debug.Log("MQTT broker disconnected.");

        _unityContext.Post(_ =>
        {
            _statusText.text = "Disconnected";
            _connectButton.interactable = true;
            _publishButton.interactable = false;
        }, null);
    }

    private void OnAppMessage(MqttApplicationMessageReceivedEventArgs args)
    {
        string payload = Encoding.UTF8.GetString(args.ApplicationMessage.Payload);
        Debug.Log($"Received message: Topic = {args.ApplicationMessage.Topic}, Payload = {payload}");

        _unityContext.Post(_ => { _receivedText.text = payload; }, null);
    }

    private async void Publish(int volume)
    {
        try
        {
            Debug.Log("Publish message.");

            MqttApplicationMessage message = new MqttApplicationMessageBuilder()
                .WithTopic(_topic)
                .WithPayload($"{{\"volume\":{volume}}}")
                .WithAtLeastOnceQoS()
                .WithRetainFlag()
                .Build();

            await _mqttClient.PublishAsync(message, CancellationToken.None);
        }
        catch (Exception e)
        {
            Debug.Log($"Failed to publish message: {e.Message}");
        }
    }

    private void OnDestroy()
    {
        _mqttClient?.Dispose();
        _mqttClient = null;
    }
}
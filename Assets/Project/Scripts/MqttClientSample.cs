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

public class MqttClientSample : MonoBehaviour
{
    [SerializeField] private string _host = "localhost";
    [SerializeField] private string _topic = "send/hello";
    [SerializeField] private string _payload = "hello";
    [SerializeField] private string _username = "username";
    [SerializeField] private string _password = "password";
    [SerializeField] private int _port = 1883;
    [SerializeField] private bool _useCredentials = false;
    [SerializeField] private bool _useTls = false;
    [SerializeField] private Button _connectButton;
    [SerializeField] private Button _publishButton;
    [SerializeField] private TMP_Text _statusText;
    [SerializeField] private TMP_Text _receivedText;
    [SerializeField] private TMP_InputField _hostInputField;
    [SerializeField] private TMP_InputField _portInputField;
    [SerializeField] private TMP_InputField _topicInputField;
    [SerializeField] private TMP_InputField _payloadInputField;

    private IMqttClient _mqttClient;
    private SynchronizationContext _unityContext;

    private void Start()
    {
        _unityContext = SynchronizationContext.Current;

        _connectButton.onClick.AddListener(HandleOnConnectButtonClicked);

        _publishButton.interactable = false;
        _publishButton.onClick.AddListener(Publish);

        _hostInputField.SetTextWithoutNotify(_host);
        _portInputField.SetTextWithoutNotify(_port.ToString());
        _topicInputField.SetTextWithoutNotify(_topic);
        _payloadInputField.SetTextWithoutNotify(_payload);

        _mqttClient = new MqttFactory().CreateMqttClient();
        _mqttClient.ApplicationMessageReceivedHandler = new MqttApplicationMessageReceivedHandlerDelegate(OnAppMessage);
        _mqttClient.ConnectedHandler = new MqttClientConnectedHandlerDelegate(OnConnected);
        _mqttClient.DisconnectedHandler = new MqttClientDisconnectedHandlerDelegate(OnDisconnected);
    }

    private void HandleOnConnectButtonClicked()
    {
        if (_mqttClient.IsConnected)
        {
            _mqttClient.DisconnectAsync();
            _statusText.text = "Disconnected";
            _connectButton.GetComponentInChildren<TMP_Text>().text = "Connect";
        }
        else
        {
            Connect();
        }
    }
    
    private async void Connect()
    {
        try
        {
            _statusText.text = "Connecting...";
            _connectButton.interactable = false;

            IMqttClientOptions options = CreateClientOptions();
            await _mqttClient.ConnectAsync(options, CancellationToken.None);
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to connect to MQTT broker: {e.Message}");

            _statusText.text = "Failed to connect";
            _connectButton.interactable = true;
        }
    }
    
    private IMqttClientOptions CreateClientOptions()
    {
        string host = _hostInputField.text;
        if (string.IsNullOrEmpty(host))
        {
            host = "localhost";
        }

        if (!int.TryParse(_portInputField.text, out int port))
        {
            port = 1883;
        }

        string topic = _topicInputField.text;
        if (string.IsNullOrEmpty(topic))
        {
            topic = "get/volume";
        }
        _topic = topic;

        MqttClientOptionsBuilder optionsBuilder = new MqttClientOptionsBuilder()
            .WithTcpServer(host, port);
        if (_useCredentials)
        {
            optionsBuilder.WithCredentials(_username, _password);
        }
        if (_useTls)
        {
            optionsBuilder.WithTls();
        }
        return optionsBuilder.Build();
    }

    private async void OnConnected(MqttClientConnectedEventArgs args)
    {
        Debug.Log("MQTT broker connected.");

        _unityContext.Post(_ =>
        {
            _statusText.text = "Connected";
            _connectButton.GetComponentInChildren<TMP_Text>().text = "Disconnect";
            _connectButton.interactable = true;
            _publishButton.interactable = true;
        }, null);

        if (string.IsNullOrEmpty(_topicInputField.text))
        {
            Debug.Log("Topic is empty. Subscription skipped.");
            _receivedText.text = "Topic is empty. Please enter a valid topic.";
            return;
        }

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

    private async void Publish()
    {
        try
        {
            Debug.Log("Publish message.");

            MqttApplicationMessage message = new MqttApplicationMessageBuilder()
                .WithTopic(_topic)
                .WithPayload(_payloadInputField.text)
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
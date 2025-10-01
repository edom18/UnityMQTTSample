using UnityEngine;
using System;
using System.Text;
using System.Threading.Tasks;
using MQTTnet;
using MQTTnet.Client.Receiving;
using MQTTnet.Server;

public class MqttBrokerSample : MonoBehaviour
{
    [SerializeField] private int _port = 1883;

    private IMqttServer _mqttServer;

    private void Awake()
    {
        _ = Setup();
    }

    private async Task Setup()
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
            // .WithConnectionValidator(context =>
            // {
            //     // 接続バリデーション（必要ないなら丸ごと削ってOK）
            //     // 例）ユーザ/パスが "meson" / "secret" のときのみ許可
            //     if (context.Username == "meson" && context.Password == "secret")
            //     {
            //         context.ReasonCode = MqttConnectReasonCode.Success;
            //     }
            //     else
            //     {
            //         // 無認証で全許可したいならこのブロックを消す
            //         context.ReasonCode = MqttConnectReasonCode.BadUserNameOrPassword;
            //     }
            //
            //     // クライアントID未指定対策（必要なら）
            //     if (string.IsNullOrWhiteSpace(context.ClientId))
            //     {
            //         context.ReasonCode = MqttConnectReasonCode.ClientIdentifierNotValid;
            //     }
            // })
            // .WithSubscriptionInterceptor(ctx =>
            // {
            //     // 購読リクエストのフィルタ
            //     // 例）特定トピックへの購読だけ許可したい場合など
            //     ctx.AcceptSubscription = true; // 全許可（必要に応じて条件分岐）
            // })
            .WithApplicationMessageInterceptor(context =>
            {
                // 発行メッセージのフィルタ
                // 例）特定トピックのみ許可、メッセージ書き換え 等
                context.AcceptPublish = true; // 全許可（必要に応じて条件分岐）
            });

        // 3) イベント購読（ログ代わり）
        _mqttServer.ClientConnectedHandler = new MqttServerClientConnectedHandlerDelegate(args => { Debug.Log($"[Connected] ClientId={args.ClientId}"); });

        _mqttServer.ClientDisconnectedHandler = new MqttServerClientDisconnectedHandlerDelegate(args => { Debug.Log($"[Disconnected] ClientId={args.ClientId} Reason={args.DisconnectType}"); });

        _mqttServer.ApplicationMessageReceivedHandler = new MqttApplicationMessageReceivedHandlerDelegate(args =>
        {
            string payload = args.ApplicationMessage?.Payload == null
                ? null
                : Encoding.UTF8.GetString(args.ApplicationMessage.Payload);

            Debug.Log(
                $"[Received] ClientId={args.ClientId} Topic={args.ApplicationMessage?.Topic} " +
                $"QoS={(args.ApplicationMessage?.QualityOfServiceLevel)} Retain={args.ApplicationMessage?.Retain} " +
                $"Payload='{payload}'");
        });

        // 4) サーバ起動
        IMqttServerOptions options = optionsBuilder.Build();
        await _mqttServer.StartAsync(options);

        Debug.Log("MQTT broker started on tcp://0.0.0.0:1883");
    }

    private void OnDestroy()
    {
        _mqttServer.StopAsync();
    }
}
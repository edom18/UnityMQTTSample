using UnityEngine;
using UnityEngine.UI;

public class Launcher : MonoBehaviour
{
    [SerializeField] private Button _startBroker;
    [SerializeField] private Button _startClient;
    
    private void Start()
    {
        _startBroker.onClick.AddListener(() =>
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("BrokerSample");
        });
        
        _startClient.onClick.AddListener(() =>
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("ClientSample");
        });
    }
}

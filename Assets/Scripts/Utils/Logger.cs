using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class Logger : MonoBehaviour
{
    static string myLog = "";
    [SerializeField] private TMP_Text logText;
    [SerializeField] private GameObject renderer;
    Keyboard keyboard => Keyboard.current;


    void OnEnable()
    {
        Application.logMessageReceived += Log;
    }

    void OnDisable()
    {
        Application.logMessageReceived -= Log;
    }

    private void Update()
    {
        if (keyboard == null) return;
        if (keyboard.backquoteKey.wasPressedThisFrame)
        {
            renderer.SetActive(!renderer.activeSelf);
        }
    }

    public void Log(string logString, string stackTrace, LogType type)
    {
        myLog += logString + "\n";
        if (type == LogType.Exception)
        {
            myLog += "<color=red>" + stackTrace + "</color>\n";
        }
        logText.text = myLog;
    }
}

using TMPro;
using UnityEngine;

// Console tab of UI.DevPanelUI's Developer Panel - just captures log output into logText.
// Show/hide is owned entirely by DevPanelUI now (backquote hotkey via PlayerController).
public class Logger : MonoBehaviour
{
    static string myLog = "";
    [SerializeField] private TMP_Text logText;

    void OnEnable()
    {
        Application.logMessageReceived += Log;
    }

    void OnDisable()
    {
        Application.logMessageReceived -= Log;
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

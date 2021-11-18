using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
public class FileManager : MonoBehaviour
{

    // File Display
    public InputField FileSelection;
    public TMP_Text FileDisplay;



    // Update is called once per frame

    void Update()
    {
        if(FileSelection.text != "" && Input.GetKeyDown(KeyCode.Return))
        {
            FindObjectOfType<NetworkedClient>().SendMessageToHost(ClientToServerSignifiers.Replay + "," + ReplaySignifiers.RequestingReplay + "," 
            + FileSelection.text);
            FileSelection.text = "";
            FindObjectOfType<Board>().RestartGame();

        }
    }
    
    public void AddFileToDisplay(string text)
    {
        FileDisplay.text += "\n" + text;

    }

    public void ResetFileList()
    {
        FileDisplay.text = "";
    }





}

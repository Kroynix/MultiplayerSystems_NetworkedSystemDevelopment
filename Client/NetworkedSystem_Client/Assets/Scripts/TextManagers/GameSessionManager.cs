using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameSessionManager : MonoBehaviour
{

    // File Display
    public InputField GameSessionSelection;
    public TMP_Text GameSessionList;

    // Update is called once per frame
    void Update()
    {
        if(GameSessionSelection.text != "" && Input.GetKeyDown(KeyCode.Return))
        {
            FindObjectOfType<NetworkedClient>().SendMessageToHost(ClientToServerSignifiers.LookingForGameSession + "," + GameSessionSignifiers.RequestJoin + "," 
            + GameSessionSelection.text);
            GameSessionSelection.text = "";

        }
    }
    
    public void AddGameSessionToDisplay(string text)
    {
        GameSessionList.text += "\n" + text;

    }

    public void ResetGameSessionList()
    {
        GameSessionList.text = "";
    }

}

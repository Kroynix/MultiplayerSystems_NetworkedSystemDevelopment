using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ChatBehaviour : MonoBehaviour
{
    public InputField chat;
    public TMP_Text chatBox;



    // Update is called once per frame
    void Update()
    {
        if(chat.text != "" && Input.GetKeyDown(KeyCode.Return))
        {
            FindObjectOfType<NetworkedClient>().SendMessageToHost(ChatStates.ClientToServer + "," + FindObjectOfType<GameSystemManager>().name + 
            "," + chat.text);
            chat.text = "";
            chat.ActivateInputField();
            Debug.Log(FindObjectOfType<GameSystemManager>().name);

        }
    }

    public void AddTextToChat(string text)
    {
        chatBox.text += "\n" + text;

    }



}

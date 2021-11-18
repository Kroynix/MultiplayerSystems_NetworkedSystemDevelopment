using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Board : MonoBehaviour
{


    [Header ("Input Settings : ")]
    [SerializeField] private LayerMask boxesLayerMask ;
    [SerializeField] private float touchRadius ;

    [Header ("Mark Sprites : ")]
    [SerializeField] private Sprite spriteX ;
    [SerializeField] private Sprite spriteO ;

    public Mark[] marks;
    private Camera cam;
    public bool canPlay;
    Mark replayMark;



    //private LineRenderer lineRenderer;


    public int marksCount = 0;

    // Finding Box by Index
    public int Index;
    private Box[] boxes;
    private Box targetBox;

    // Game Objects - Winner Text
    GameObject xwin, owin, tie;

    // Game Objects - Buttons
    GameObject resetGame, requestReplay;

    // Game Objects - Replay
    GameObject InputReplayName, ConfirmationBox;


    // Start is called before the first frame update
    void Start()
    {
        boxes = Resources.FindObjectsOfTypeAll<Box>();
        cam = Camera.main;
        replayMark = Mark.O;
        marks = new Mark[9];
        canPlay = false;


        // Get list of every gameObject
        GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        // Get Reference to all needed Game Objects
        foreach (GameObject go in allObjects)
        {
            // Game Objects - Gameplay
            if (go.name == "Xwin")
                xwin = go;
            else if (go.name == "Owin")
                owin = go;
            else if (go.name == "Tie")
                tie = go;
            else if (go.name == "ReplayGame")
                resetGame = go;
            else if (go.name == "RequestReplay")
                requestReplay = go;
            else if (go.name == "InputNameField")
                InputReplayName = go;
            else if (go.name == "ConfirmationButton")
                ConfirmationBox = go;

        }
    }


    public void ConfirmationBoxPressed()
    {
        if (InputReplayName.GetComponent<InputField>().text != "")
        {
            FindObjectOfType<NetworkedClient>().SendMessageToHost(ClientToServerSignifiers.Match + "," + GameSignifiers.SaveReplay + "," + InputReplayName.GetComponent<InputField>().text);
        }
    }


    public void RequestReplayPressed()
    {
        InputReplayName.SetActive(true);
        ConfirmationBox.SetActive(true);
        resetGame.SetActive(false);
        requestReplay.SetActive(false);
    }


    // Update is called once per frame
    void Update()
    {

        if (canPlay && Input.GetMouseButtonUp (0)) 
        {
            Vector2 touchPosition = cam.ScreenToWorldPoint (Input.mousePosition);

            Collider2D hit = Physics2D.OverlapCircle(touchPosition, touchRadius, boxesLayerMask);
            if(canPlay)
            {
                if (hit) 
                {
                    HitBox (hit.GetComponent <Box>(), Mark.O);
                    FindObjectOfType<NetworkedClient>().SendMessageToHost(ClientToServerSignifiers.Match + "," + GameSignifiers.SendMoveToServer + "," + hit.GetComponent<Box>().index);
                    canPlay = false;
                    
                }
            }
        }

    }


    private void SwitchPlayMark()
    {
        replayMark = (replayMark == Mark.X) ? Mark.O : Mark.X;
    }



    public void HitBox (Box box, Mark mark) 
    {
        if(!box.isMarked)
        {
            marks[box.index] = mark;

            box.SetMarked(GetSprite(mark), mark);
            marksCount++;

            // Check for winner
            bool won = CheckIfWin(mark);
            if(won)
            {
                FindObjectOfType<NetworkedClient>().SendMessageToHost(ClientToServerSignifiers.Match + "," + GameSignifiers.EndGame);
                displayWinner(mark);
                resetGame.SetActive(true);
                requestReplay.SetActive(true);
                canPlay = false;
                return;
            }

            if (marksCount == 9)
            {
                displayTie();
                resetGame.SetActive(true);
                requestReplay.SetActive(true);
                FindObjectOfType<NetworkedClient>().SendMessageToHost(ClientToServerSignifiers.Match + "," + GameSignifiers.EndGame);
            }
        }

    }



    public void Replaying(Box box)
    {
        if(!box.isMarked)
        {
            marks[box.index] = replayMark;

            box.SetMarked(GetSprite(replayMark), replayMark);
            marksCount++;
            // Check for winner
            bool won = CheckIfWin(replayMark);
            if(won)
            {
                FindObjectOfType<NetworkedClient>().SendMessageToHost(ClientToServerSignifiers.Match + "," + GameSignifiers.EndGame);
                displayWinner(replayMark);
                canPlay = false;
                return;
            }

            if (marksCount == 9)
            {
                displayTie();
                FindObjectOfType<NetworkedClient>().SendMessageToHost(ClientToServerSignifiers.Match + "," + GameSignifiers.EndGame);
            }

            SwitchPlayMark();
        }
    }


    public void RemoteEndGame()
    {
        canPlay = false;

    }

    public void RestartGame()
    {
        foreach(Box box in boxes)
            box.ResetBox();
        

        for(int i = 0; i < marks.Length; i++)
            marks[i] = Mark.None;

        marksCount = 0;
        tie.SetActive(false);
        xwin.SetActive(false);
        owin.SetActive(false);
        resetGame.SetActive(false);
        requestReplay.SetActive(false);
        InputReplayName.SetActive(false);
        ConfirmationBox.SetActive(false);
    }






    private void displayTie()
    {
        tie.SetActive(true);
    }



    private void displayWinner(Mark mark)
    {

        if(mark == Mark.X)
        {
            xwin.SetActive(true);
        }
        else
        {
            owin.SetActive(true);
        }
    }



    private bool CheckIfWin (Mark mark) 
    {
        return
        AreBoxesMatched (0, 1, 2, mark) || AreBoxesMatched (3, 4, 5, mark) || AreBoxesMatched (6, 7, 8, mark) ||
        AreBoxesMatched (0, 3, 6, mark) || AreBoxesMatched (1, 4, 7, mark) || AreBoxesMatched (2, 5, 8, mark) ||
        AreBoxesMatched (0, 4, 8, mark) || AreBoxesMatched (2, 4, 6, mark);
    }


    private bool AreBoxesMatched(int i, int j, int k, Mark mark)
    {
        Mark m = mark;
        bool match = (marks[i] == m && marks[j] == m && marks[k] == m);
        return match;
    }

    private Sprite GetSprite(Mark mark)
    {
        return(mark == Mark.X) ? spriteX : spriteO;
    }


    public Box FindBox(int _Index)
    {
        if (_Index != null)
        {
            foreach(Box box in boxes)
            {
                if (box.index == _Index)
                {
                    Debug.Log(box.name);
                    return box;
                }
            }
        }
        return null;
    }

    
}

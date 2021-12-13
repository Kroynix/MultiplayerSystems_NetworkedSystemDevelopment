using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using System.IO;
using UnityEngine.UI;

public class NetworkedServer : MonoBehaviour
{
    int maxConnections = 1000;
    int reliableChannelID;
    int unreliableChannelID;
    int hostID;
    int socketPort = 3389;



    List<int> idlist;
    LinkedList<PlayerAccount> playerAccounts;
    LinkedList<string> ReplayFiles;
    List<GameSession> gameSessions;



    string playerAccountFilePath;
    string userRecordingFilePath;
    int playerWaitingForMatch = -1;

    // Start is called before the first frame update
    void Start()
    {
        // Player Path Constants
        playerAccountFilePath = Application.dataPath + Path.DirectorySeparatorChar + "UserFiles" + Path.DirectorySeparatorChar + "PlayerAccountData.txt";
        userRecordingFilePath = Application.dataPath + Path.DirectorySeparatorChar + "RecordingFiles" + Path.DirectorySeparatorChar;

        // Intialize the Network Transport and setup Configs and Tepology
        NetworkTransport.Init();
        ConnectionConfig config = new ConnectionConfig();
        reliableChannelID = config.AddChannel(QosType.Reliable);
        unreliableChannelID = config.AddChannel(QosType.Unreliable);
        HostTopology topology = new HostTopology(config, maxConnections);
        hostID = NetworkTransport.AddHost(topology, socketPort, null);


        // List of Player accounts and current connected ID's
        playerAccounts = new LinkedList<PlayerAccount>();
        ReplayFiles = new LinkedList<string>();
        idlist = new List<int>();
        gameSessions = new List<GameSession>();
        


        //We need to load our saved Player Accounts.
        LoadPlayerAccounts();


        
    }

    // Update is called once per frame
    void Update() 
    {
        int recHostID;
        int recConnectionID;
        int recChannelID;
        byte[] recBuffer = new byte[1024];
        int bufferSize = 1024;
        int dataSize;
        byte error = 0;

        NetworkEventType recNetworkEvent = NetworkTransport.Receive(out recHostID, out recConnectionID, out recChannelID, recBuffer, bufferSize, out dataSize, out error);

        switch (recNetworkEvent)
        {
            case NetworkEventType.Nothing:
                break;
            case NetworkEventType.ConnectEvent:
                Debug.Log("Connection, " + recConnectionID);
                idlist.Add(recConnectionID);
                break;
            case NetworkEventType.DataEvent:
                string msg = Encoding.Unicode.GetString(recBuffer, 0, dataSize);
                ProcessRecievedMsg(msg, recConnectionID);
                Debug.Log("Message Received: " + msg);
                break;
            case NetworkEventType.DisconnectEvent:
                Debug.Log("Disconnection, " + recConnectionID);
                idlist.Remove(recConnectionID);
                break;
        }

    }
  
    public void SendMessageToClient(string msg, int id) 
    {
        byte error = 0;
        byte[] buffer = Encoding.Unicode.GetBytes(msg);
        NetworkTransport.Send(hostID, id, reliableChannelID, buffer, msg.Length * sizeof(char), out error);
    }
    
    private void ProcessRecievedMsg(string msg, int id)
    {

        string[] csv = msg.Split(',');
        int signifier = int.Parse(csv[0]);

        // Handler if Client wants to Create and Account
        if(signifier == ClientToServerSignifiers.CreateAccount)
        {
            string n = csv[1];
            string p = csv[2];

            bool isUnique = true;
            foreach(PlayerAccount pa in playerAccounts) 
            {
                if(pa.Name == n)
                {
                    isUnique = false;
                    break;
                }
            }

            // Check if Details are Unique
            if(isUnique) 
            {
                playerAccounts.AddLast(new PlayerAccount(n,p));
                SendMessageToClient(ServerToClientSignifiers.LoginResponse + "," + LoginResponses.AccountCreated + "," + "Account Created", id);
                SavePlayerAccounts();
                foreach(PlayerAccount pa in playerAccounts)
                    Debug.Log(pa);
            }
            else
            {
                SendMessageToClient(ServerToClientSignifiers.LoginResponse + "," + LoginResponses.FailureNameInUse + "," + "This Account already Exists", id);
            }
        }

        // Handler if Client wants to login
        else if (signifier == ClientToServerSignifiers.Login)
        {
            string n = csv[1];
            string p = csv[2];

            bool hasBeenFound = false;
            

            // Check for Matching Credentials 
            foreach(PlayerAccount pa in playerAccounts)
            {
                if(pa.Name == n)
                {
                    if(pa.Password == p)
                    {
                        SendMessageToClient(ServerToClientSignifiers.LoginResponse + "," + LoginResponses.Success + "," + "You have Successfully Logged In", id);
                        SendMessageToClient(ServerToClientSignifiers.LoginResponse + "," + LoginResponses.SendUsername + "," + n, id);
                    }
                    else
                    {
                        SendMessageToClient(ServerToClientSignifiers.LoginResponse + "," + LoginResponses.FailureIncorrectPassword + "," + "Incorrect Password", id);
                        
                    }
                    hasBeenFound = true;
                    break;
                }

            }
            if(!hasBeenFound)
            {
                SendMessageToClient(ServerToClientSignifiers.LoginResponse + "," + LoginResponses.FailureNameNotFound + "," + "Failure name not found", id);
            }

        }

        // Send Chat messages to All Clients after being sent to the server
        else if (signifier == ChatStates.ClientToServer)
        {
            string name = csv[1];
            string message = csv[2];
            foreach (int identifier in idlist)
            {
                SendMessageToClient(ChatStates.ServerToClient + "," + name + "," + message, identifier);
            }
        }


        // Match Signifier Handling
        else if (signifier == ClientToServerSignifiers.Match)
        {
            // Check Signifier for Matchs
            int MatchSignifier = int.Parse(csv[1]);

            
            // Handler if there is no current match available
            if (MatchSignifier == GameSignifiers.FindMatch)
            {
                if(playerWaitingForMatch == -1)
                {
                    playerWaitingForMatch = id;
                }
                else 
                {
                    GameSession gs = new GameSession(playerWaitingForMatch, id);
                    gameSessions.Add(gs);
                    SendMessageToClient(ServerToClientSignifiers.MatchResponse + "," + GameSignifiers.AddToGameSession + "," + 1, gs.playerID1);
                    SendMessageToClient(ServerToClientSignifiers.MatchResponse + "," + GameSignifiers.AddToGameSession + "," + 0, gs.playerID2);
                    playerWaitingForMatch = -1;
                    Debug.Log("User found match!");
                }
            }

            // Handler to send moves made on a player side to all sides (Observer or Enemy)
            else if (MatchSignifier == GameSignifiers.SendMoveToServer)
            {
                int move = int.Parse(csv[2]);
                Debug.Log("Square User placed on is: " + move);
                GameSession gs = FindGameSessionWithPlayerID(id);


                if(gs != null) 
                {
                    gs.playerMoves.AddLast(move);
                    if(gs.playerID1 == id)
                        SendMessageToClient(ServerToClientSignifiers.MatchResponse + "," + GameSignifiers.SendMoveToClients + "," + move, gs.playerID2);
                    else
                        SendMessageToClient(ServerToClientSignifiers.MatchResponse + "," + GameSignifiers.SendMoveToClients + "," + move, gs.playerID1);

                    foreach(int observer in gs.observers)
                    {
                        SendMessageToClient(ServerToClientSignifiers.ReplayResponse + "," + ReplaySignifiers.SendingReplay + "," + move, observer);
                    }
                    
                }
            }

            // Handler if any of the Users Win or Tie, the game is Ended for both players and actions are paused until game is Reset
            else if (MatchSignifier == GameSignifiers.EndGame)
            {
                GameSession gs = FindGameSessionWithPlayerID(id);
                if(gs != null) 
                {
                    SendMessageToClient(ServerToClientSignifiers.MatchResponse + "," + GameSignifiers.EndGame, gs.playerID1);
                    SendMessageToClient(ServerToClientSignifiers.MatchResponse + "," + GameSignifiers.EndGame, gs.playerID2);
                    
                }
            }

            // Handler to Reset the Game (Client and Observers)
            else if (MatchSignifier == GameSignifiers.ResetGame)
            {
                GameSession gs = FindGameSessionWithPlayerID(id);

                if(gs != null)
                {
                    gs.playerMoves.Clear();
                    SendMessageToClient(ServerToClientSignifiers.MatchResponse + "," + GameSignifiers.ResetGame + "," + 0, gs.playerID1);
                    SendMessageToClient(ServerToClientSignifiers.MatchResponse + "," + GameSignifiers.ResetGame + "," + 1, gs.playerID2);


                    foreach(int observer in gs.observers)
                    {
                        SendMessageToClient(ServerToClientSignifiers.MatchResponse + "," + GameSignifiers.ResetGame + "," + 0, observer);
                    }
                }
            }

            // Handle saving of Files, Allow the user to choose the name of the file and save it on the Server Side
            else if (MatchSignifier == GameSignifiers.SaveReplay)
            {
                string FileName = csv[2];
                GameSession gs = FindGameSessionWithPlayerID(id);
                SaveReplay(id, FileName);
                SendMessageToClient(ServerToClientSignifiers.MatchResponse + "," + GameSignifiers.ReplaySavedSuccessfully, id);
                
            }


            // If any of the players decide to quit the game all players will be booted back to the Chatroom
            else if (MatchSignifier == GameSignifiers.QuitGame)
            {

                // Players Quitting
                GameSession gs = FindGameSessionWithPlayerID(id);

                if(gs != null)
                {
                    if(gs.playerID1 == id)
                        SendMessageToClient(ServerToClientSignifiers.MatchResponse + "," + GameSignifiers.QuitGame, gs.playerID2);
                    else
                        SendMessageToClient(ServerToClientSignifiers.MatchResponse + "," + GameSignifiers.QuitGame, gs.playerID1);

                    foreach(int observer in gs.observers)
                    {
                        SendMessageToClient(ServerToClientSignifiers.MatchResponse + "," + GameSignifiers.QuitGame, observer);
                    }

                    // Remove the Gamesession
                    gameSessions.Remove(gs);
                }

                // Observer Quit, Remove from Observer Queue
                GameSession ObservGS = FindObserverWithID(id);
                if(ObservGS != null)
                {
                    ObservGS.observers.Remove(id);
                    SendMessageToClient(ServerToClientSignifiers.MatchResponse + "," + GameSignifiers.QuitGame, id);
                }
            }
        }

        // Check if the Signifier If the client is asking to Replay
        else if (signifier == ClientToServerSignifiers.Replay)
        {
            int ReplaySignifier = int.Parse(csv[1]);

            if(ReplaySignifier == ReplaySignifiers.RequestingReplay)
            {
                LinkedList<int> moveList = LoadReplayFile(csv[2]);
                if(moveList != null)
                {
                    StartCoroutine(SendReplayDelay(moveList,id, 0.5f));
                }
                else
                {
                    SendMessageToClient(ServerToClientSignifiers.ReplayResponse + "," + ReplaySignifiers.NullReplay, id);
                }
            }

            else if (ReplaySignifier == ReplaySignifiers.StartingReplay)
            {
                LoadAllReplays();
                foreach(string file in ReplayFiles)
                {
                    SendMessageToClient(ServerToClientSignifiers.ReplayResponse + "," + ReplaySignifiers.SendingFiles + "," + file, id);
                }
            }

        }

        // Handler if Signifier if the Client is looking for a game session
        else if (signifier == ClientToServerSignifiers.LookingForGameSession)
        {
            int GameSessionSignifier = int.Parse(csv[1]);

            //send them a slist of all the current Game Sessions 
            if(GameSessionSignifier == GameSessionSignifiers.RequestSessionList)
            {
                foreach(GameSession gs in gameSessions)
                    SendMessageToClient(ServerToClientSignifiers.GameSessionResponse + "," + GameSessionSignifiers.SendingSessionList + "," + gs.playerID1, id);
            }

            // Handler if the User is requesting to join a GameSession as a Observer
            else if (GameSessionSignifier == GameSessionSignifiers.RequestJoin)
            {
                int SessionID = int.Parse(csv[2]);

                GameSession gs = FindGameSessionWithPlayerID(SessionID);

                if(gs != null)
                {
                    gs.observers.Add(id);
                    SendMessageToClient(ServerToClientSignifiers.GameSessionResponse + "," + GameSessionSignifiers.JoinApproved, id);
                    StartCoroutine(SendReplayDelay(gs.playerMoves,id,0));

                }
                else
                {
                    SendMessageToClient(ServerToClientSignifiers.GameSessionResponse + "," + GameSessionSignifiers.JoinDenied, id);
                }

                    


            }
        }


    }

    #region PlayerAccountHandling
    private void SavePlayerAccounts()
    {
        //StreamWriter sw = new StreamWriter(playerAccountFilePath);
        using (StreamWriter sw = File.AppendText(playerAccountFilePath))
        {
            foreach(PlayerAccount pa in playerAccounts)
            {
                sw.WriteLine(pa.Name + "," + pa.Password);
            }
            sw.Close();
        }
        
    }

    // Load player accounts through the playerAccount path, Checks if it exists beforehand.
    private void LoadPlayerAccounts()
    {
        if(File.Exists(playerAccountFilePath))
        {
            StreamReader sr = new StreamReader(playerAccountFilePath);

            string line;

            while((line = sr.ReadLine()) != null)
            {
                string[] csv = line.Split(',');
                PlayerAccount pa = new PlayerAccount(csv[0],csv[1]);
                playerAccounts.AddLast(pa);
            }
        }
    }
    #endregion PlayerAccountHandling



    #region ReplayHandling
    private void LoadAllReplays()
    {
        ReplayFiles.Clear();
        string [] files = System.IO.Directory.GetFiles(userRecordingFilePath);
        foreach (string file in files)
        {
            string pos = file.Remove(0,120);
            if(!pos.Contains(".meta"))
                ReplayFiles.AddLast(pos);
            
            Debug.Log(pos);
            
        }
        
    }
    
    private void SaveReplay(int id, string FileName)
    {
        GameSession gs = FindGameSessionWithPlayerID(id);

            if (gs != null)
                using (StreamWriter sw = File.AppendText(userRecordingFilePath + FileName + ".txt"))
                {
                    foreach(int move in gs.playerMoves)
                    {
                        sw.WriteLine(move);
                    }
                    sw.Close();
                }
    }


    // Load Load Replay files by returning a LinkedList of all moves
    private LinkedList<int> LoadReplayFile(string fileName)
    {
        LinkedList<int> moveList = new LinkedList<int>();
        if(File.Exists(userRecordingFilePath + fileName))
        {
            StreamReader sr = new StreamReader(userRecordingFilePath + fileName);
            string line;

            while((line = sr.ReadLine()) != null)
            {
                moveList.AddLast(int.Parse(line));
            }
            return moveList;
        }
        return null;
    }


    // Have the server Send Replay Moves individually.
    IEnumerator SendReplayDelay(LinkedList<int> moves, int identifier, float time)
    {
        foreach(int move in moves)
        {
            SendMessageToClient(ServerToClientSignifiers.ReplayResponse + "," + ReplaySignifiers.SendingReplay + "," + move, identifier);
            yield return new WaitForSeconds(time);
        }
       
    }

    #endregion ReplayHandling


    // Find Game Session with PlayerID's 
    private GameSession FindGameSessionWithPlayerID(int id)
    {
        foreach(GameSession gs in gameSessions)
        {
            if(gs.playerID1 == id || gs.playerID2 == id)
                return gs;
        }
        return null;
    }


    // Find Find GameSession via Observers ID
    private GameSession FindObserverWithID(int id)
    {
        foreach(GameSession gs in gameSessions)
        {
            foreach(int observer in gs.observers)
            {
                if(id == observer)
                    return gs;
            }
        }
        return null;     
    }


    
    
}





public class GameSession
{
    public int playerID1, playerID2;
    public List<int> observers = new List<int>();
    public LinkedList<int> playerMoves = new LinkedList<int>();

    public GameSession(int id1, int id2)
    {
        playerID1 = id1;
        playerID2 = id2;
    }



}



public class PlayerAccount
{
    public string Name, Password;

    public PlayerAccount(string name, string password)
    {
        Name = name;
        Password = password;
    }
}


// Front Signifiers
public static class ClientToServerSignifiers
{
    public const int Login = 1;
    public const int CreateAccount = 2;
    public const int Match = 3;
    public const int Replay = 4;
    public const int LookingForGameSession = 5;
    
}

public static class ServerToClientSignifiers
{
    public const int LoginResponse = 1;
    public const int MatchResponse = 2;
    public const int ReplayResponse = 3;
    public const int GameSessionResponse = 4;

}


public static class GameSessionSignifiers
{
    public const int SendingSessionList = 1;
    public const int RequestSessionList = 2;
    public const int RequestJoin = 3;
    public const int JoinApproved = 4;
    public const int JoinDenied = 5;
}


public static class ReplaySignifiers
{
    public const int RequestingReplay = 1;
    public const int SendingReplay = 2;
    public const int StartingReplay = 3;
    public const int SendingFiles = 4;
    public const int RequestingReplayFiles = 5;
    public const int NullReplay = 6;

}



public static class GameSignifiers
{
    public const int FindMatch = 1;
    public const int SendMoveToServer = 2;
    public const int AddToGameSession = 3;
    public const int SendMoveToClients = 4;
    public const int EndGame = 5;
    public const int ResetGame = 6;
    public const int LookUpRoom = 7;
    public const int SendingReplay = 8;
    public const int SaveReplay = 9;
    public const int QuitGame = 10;
    public const int ReplaySavedSuccessfully = 11;
}




public static class ChatStates
{
    public const int ClientToServer = 20;
    public const int ServerToClient = 21;
    public const int ConnectedUserList = 22;
}


// Back Signifiers
public static class LoginResponses{
    public const int Success = 1;
    public const int FailureNameInUse = 2;
    public const int FailureNameNotFound = 3;
    public const int FailureIncorrectPassword = 4;
    public const int AccountCreated = 5;
    public const int SendUsername = 6;
}



public static class GameStates{
    public const int Login = 1;
    public const int MainMenu = 2;
    public const int WaitingForMatch = 3;
    public const int PlayingTicTacToe = 4;

}
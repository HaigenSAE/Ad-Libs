using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NDream.AirConsole;
using Newtonsoft.Json.Linq;
using UnityEngine.UI;
using System.Linq;

public class GameLogic : MonoBehaviour
{

    public Text myText;
    public string mySpeech;

    public List<string> introStrings, fillerStrings, outroStrings;
    public List<string> pintroStrings, pfillerStrings, poutroStrings;

    public Dictionary<int, List<string>> promptAnswers = new Dictionary<int, List<string>>(); //first id is prompt, then list of answers [prompt][answer] :)

    public GameObject votingCardPfb;
    public GameObject canvasObj;
    public List<GameObject> votingCards;

    int blankCounter;
    int typewriterCounter;
    int numPlayers;
    int firstConnectedID;

    private AirConsole airConsole;
    private bool isStarted;
    public List<int> playerKeys = new List<int>();

    private void Awake()
    {
        AirConsole.instance.onMessage += OnMessage;
        AirConsole.instance.onConnect += Begin;
        AirConsole.instance.onDisconnect += Disconnect;
        myText.text = "";
    }

    private void Start()
    {
        
        TextAsset speechData = Resources.Load<TextAsset>("speechData");
        TextAsset promptsData = Resources.Load<TextAsset>("promptsData");

        string[] data = speechData.text.Split(new char[] { '\n' });
        for(int i = 1; i < data.Length - 1; i++)
        {
            string[] row = data[i].Split(new char[] { ',' });

            introStrings.Add(row[0]);
            fillerStrings.Add(row[1]);
            outroStrings.Add(row[2]);
        }
        mySpeech = introStrings[Random.Range(0, introStrings.Count - 1)] + '\n' +
                    fillerStrings[Random.Range(0, fillerStrings.Count - 1)] + '\n' +
                    outroStrings[Random.Range(0, outroStrings.Count - 1)];

        string[] pdata = promptsData.text.Split(new char[] { '\n' });
        for(int i = 1; i < pdata.Length - 1; i++)
        {
            string[] prow = pdata[i].Split(new char[] { ',' });
            pintroStrings.Add(prow[0]);
            pfillerStrings.Add(prow[1]);
            poutroStrings.Add(prow[2]);
        }

        
    }

    private void Begin(int dev_id)
    {
        if(numPlayers == 0)
        {
            firstConnectedID = dev_id;
            AirConsole.instance.Message(dev_id,"ShowStartButton");
        }
        playerKeys.Add(dev_id);       
        numPlayers++;
        if(!isStarted && numPlayers >= 3)
        {
            isStarted = true;
            AirConsole.instance.Broadcast(pintroStrings[Random.Range(0, pintroStrings.Count - 1)]);
            
        }
    }
    private void Disconnect(int dev_id)
    {
        playerKeys.Remove(dev_id);
        numPlayers--;
        if (numPlayers < 3)
        {
            isStarted = false;
            Start();
        }
    }

    void MoveForward()
    {
        StartCoroutine("PlayText");
    }

    void NextBlank()
    {
        List<int> arr = AirConsole.instance.GetControllerDeviceIds();
        List<string> answers = new List<string>();
        numPlayers = arr.Count;
        //voting time!
        for (int i = 0; i < numPlayers; i++)
        {
            GameObject go = Instantiate(votingCardPfb, transform.position, transform.rotation, canvasObj.transform.transform) as GameObject;
            go.transform.localPosition = new Vector3(-315 + (315 * Mathf.FloorToInt(i / 3)), -100 - (115 * ((i >= 3) ? i-3 : i)), 0);
            votingCards.Add(go);
            go.GetComponentInChildren<Text>().text = promptAnswers[arr[i]][blankCounter];
            answers.Add(promptAnswers[arr[i]][blankCounter]);
        }
        object stuffToSend = new
        {
            title = "VotingTime",
            np = numPlayers,
            ans = answers
        };
        AirConsole.instance.Broadcast(stuffToSend);
        Debug.Log("Sending: " + stuffToSend);
        blankCounter++;
        if(blankCounter == 3)
        {
            //End the game!
        }
    }

    IEnumerator PlayText()
    {
        for(int i = typewriterCounter; i < mySpeech.Length - 1; i++)
        {
            if(mySpeech[i] == '_')
            {
                typewriterCounter += 4;
                myText.text += mySpeech[i];
                myText.text += mySpeech[i+1];
                myText.text += mySpeech[i+2];
                myText.text += mySpeech[i+3];
                NextBlank();
                break;
            }
            else
            {
                typewriterCounter++;
                myText.text += mySpeech[i];
                yield return new WaitForSeconds(0.1f);
            }
        }
    }

    void OnMessage(int fromDeviceID, JToken data)
    {
        Debug.Log("message from " + fromDeviceID + ", data: " + data);
        if (data["result"] != null && data["result"].ToString() != "")
        {
            if (promptAnswers.ContainsKey(fromDeviceID))
            {
                if (promptAnswers[fromDeviceID].Count >= 3)
                {
                    AirConsole.instance.Message(fromDeviceID, "AnswersLocked");
                    Debug.Log("Locked");
                }
                else
                {
                    promptAnswers[fromDeviceID].Add(data["result"].ToString());
                    if(promptAnswers[fromDeviceID].Count == 3)
                    {
                        AirConsole.instance.Message(fromDeviceID, "AnswersLocked");
                        Debug.Log("Locking");
                    }
                }
            }
            else
            {
                promptAnswers.Add(fromDeviceID, new List<string> { data["result"].ToString() });
            }
            if(promptAnswers.Keys.Count == numPlayers)
            {
                if(promptAnswers.Values.Sum(list => list.Count) == numPlayers * 3)
                {
                    Debug.Log("all players answered");
                    MoveForward();
                }
            } 
        }
        else if (data["action"] != null && data["action"].ToString() != "")
        {
            myText.text = data["action"].ToString();
        }
        else if(data["start"] != null && data["start"].ToString() != "")
        {
            AirConsole.instance.Broadcast("StartGame");
        }
        
    }

    private void OnDestroy()
    {
        if(AirConsole.instance != null)
        {
            AirConsole.instance.onMessage -= OnMessage;
        }
    }
}

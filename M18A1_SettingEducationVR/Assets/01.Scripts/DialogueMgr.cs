﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.IO;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

public class DialogueMgr : MonoBehaviour
{
    private Transform tr;
    private LineRenderer line;

    private Ray ray;
    private RaycastHit hit;
    private Camera cam;
    private int layerBT;

    private string[] texts;
    private List<string> dialogueList;
    private Text uiText;
    private State nowState;
    private MineState mineState;
    private int nextDialogue = 0;
    private int SkipNextCount = 0;

    private GameObject dialogObj;
    private VideoPlayer videoPlayer;
    public VideoClip[] videoClips;

    public Connect detonatorConn;
    public Connect electricTestConn;
    public Claymore claymoreConn;

    public OVRGrabber[] grabbers;
    private OVRGrabbable grabbedObject;

    private GameObject okCanvas;

    public bool isSet = false;
    public bool isHidden = false;

    enum State
    {
        Idle,
        Playing,
        Next,
    }

    enum MineState
    {
        Idle0,
        DetonConnETest1,
        ETestConnELine2,
        ETestCheckLight3,
        ELineConnMine4,
        MineSet5,
        MineHide6,
        ReELineConnMine7,
        DetonConnELine8,
        Fire9,
    }

    void Start()
    {
        tr = GetComponent<Transform>();
        line = GetComponent<LineRenderer>();
        layerBT = 1 << LayerMask.NameToLayer("DIALOGUE");
        cam = Camera.main;

        dialogueList = new List<string>();

        uiText = GameObject.Find("DialogueText").GetComponent<Text>();
        dialogObj = GameObject.Find("DialogCanvas");
        videoPlayer = dialogObj.transform.parent.Find("VideoPlayer").GetComponent<VideoPlayer>();

        TextAsset data = Resources.Load("DialogueText", typeof(TextAsset)) as TextAsset;
        StringReader sr = new StringReader(data.text);

        string dialogueLine;
        dialogueLine = sr.ReadLine();
        while (dialogueLine != null)
        {
            dialogueList.Add(dialogueLine);
            dialogueLine = sr.ReadLine();
        }

        CreateDialogueText(dialogueList[SkipNextCount]);

        nowState = State.Next;
        mineState = MineState.Idle0;

        okCanvas = GameObject.Find("OkCanvas");
        okCanvas.SetActive(false);
        GrabberChange(false);
    }

    void CreateDialogueText(string dialogueText)
    {
        texts = dialogueText.Split('E');
    }

    void Update()
    {
        ray = new Ray(tr.position, tr.forward);
        if (Physics.Raycast(ray, out hit, 16.0f))
        {
            float dist = hit.distance;
            line.SetPosition(1, new Vector3(0, 0, dist));
        }
        if (OVRInput.GetUp(OVRInput.Button.One))
        {
            if (Physics.Raycast(ray, out hit, Mathf.Infinity, layerBT))
            {
                if (mineState == MineState.Idle0)
                {
                    videoPlayer.gameObject.SetActive(false);
                    dialogObj.transform.parent.Find("VideoPlayer2").gameObject.SetActive(false);
                }
                StartCoroutine("Run");
            }
            if (Physics.Raycast(ray, out hit, Mathf.Infinity, 1 << LayerMask.NameToLayer("NEXT")))
            {
                switch (mineState)
                {
                    case MineState.Idle0:
                        OutlineOnOff("DetonatorP", true);
                        OutlineOnOff("ElectricTestP", true);
                        break;
                    case MineState.DetonConnETest1:
                        OutlineOnOff("RopeTween", true);
                        OutlineOnOff("ElectricTestP", true);
                        break;
                    case MineState.ETestConnELine2:
                        OutlineOnOff("ElectricTestLight", true);
                        videoPlayer.gameObject.SetActive(true);
                        videoPlayer.clip = videoClips[1];
                        break;
                    case MineState.ETestCheckLight3:
                        OutlineOnOff("RopeTween", true);
                        OutlineOnOff("M18ClaymoreMine", true);
                        break;
                    case MineState.MineHide6:
                        OutlineOnOff("ElectricTestLight", true);
                        break;
                    case MineState.ReELineConnMine7:
                        OutlineOnOff("RopeTween", true);
                        OutlineOnOff("ElectricTestP", true);
                        OutlineOnOff("DetonatorP", true);
                        break;
                    case MineState.DetonConnELine8:
                        OutlineOnOff("DetonatorP", true);
                        break;
                }
                GrabberChange(true);
                okCanvas.SetActive(false);
            }
        }
    }

    private void OutlineOnOff(string objectName, bool onOff)
    {
        GameObject.Find(objectName).GetComponent<Outline>().enabled = onOff;
    }

    private void GrabberChange(bool state)
    {
        foreach (OVRGrabber grabber in grabbers)
        {
            grabber.enabled = state;
        }
    }

    private bool CheckGrapDetonator()
    {
        foreach (OVRGrabber grabber in grabbers)
        {
            if (grabber.grabbedObject != null)
            {
                grabbedObject = grabber.grabbedObject;
            }
        }

        return grabbedObject.name == "DetonatorP";
    }

    public void CheckState()
    {
        if (detonatorConn.isConnected && mineState == MineState.Idle0)
        {
            mineState = MineState.DetonConnETest1;
            OutlineOnOff("DetonatorP", false);
            OutlineOnOff("ElectricTestP", false);
            EndDrawing();
        }

        if (detonatorConn.isConnected && electricTestConn.isConnected && mineState == MineState.DetonConnETest1)
        {
            mineState = MineState.ETestConnELine2;
            OutlineOnOff("RopeTween", false);
            OutlineOnOff("ElectricTestP", false);
            EndDrawing();
        }

        if (detonatorConn.isConnected && electricTestConn.isConnected && OVRInput.GetDown(OVRInput.Button.SecondaryThumbstick)
            && mineState == MineState.ETestConnELine2)
        {
            if (CheckGrapDetonator())
            {
                OutlineOnOff("ElectricTestLight", false);
                videoPlayer.gameObject.SetActive(false);
                mineState = MineState.ETestCheckLight3;
                EndDrawing();
            }
            grabbedObject = null;
        }

        if (detonatorConn.isConnected && electricTestConn.isConnected && claymoreConn.isConnected
            && mineState == MineState.ETestCheckLight3)
        {
            mineState = MineState.ELineConnMine4;
            OutlineOnOff("RopeTween", false);
            OutlineOnOff("M18ClaymoreMine", false);
            videoPlayer.gameObject.SetActive(true);
            videoPlayer.clip = videoClips[0];
            EndDrawing();
        }

        if (this.isSet && mineState == MineState.ELineConnMine4)
        {
            mineState = MineState.MineSet5;
            videoPlayer.gameObject.SetActive(false);
            EndDrawing();
        }

        if (this.isHidden && mineState == MineState.MineSet5)
        {
            mineState = MineState.MineHide6;
            EndDrawing();
        }

        if (detonatorConn.isConnected && electricTestConn.isConnected && claymoreConn.isConnected
            && OVRInput.GetDown(OVRInput.Button.SecondaryThumbstick)
            && mineState == MineState.MineHide6)
        {
            if (CheckGrapDetonator())
            {
                mineState = MineState.ReELineConnMine7;
                OutlineOnOff("ElectricTestLight", false);
                EndDrawing();
            }
            grabbedObject = null;
        }

        if (detonatorConn.isConnected && !electricTestConn.isConnected && claymoreConn.isConnected
            && mineState == MineState.ReELineConnMine7)
        {
            GameObject enemies = Resources.Load("Enemies") as GameObject;
            Instantiate(enemies);
            mineState = MineState.DetonConnELine8;
            OutlineOnOff("RopeTween", false);
            OutlineOnOff("ElectricTestP", false);
            OutlineOnOff("DetonatorP", false);
            EndDrawing();
        }

        if (detonatorConn.isConnected && !electricTestConn.isConnected && claymoreConn.isConnected
            && OVRInput.GetDown(OVRInput.Button.SecondaryThumbstick) && mineState == MineState.DetonConnELine8)
        {
            if (CheckGrapDetonator())
            {
                mineState = MineState.Fire9;
                OutlineOnOff("DetonatorP", false);
                EndDrawing();
            }
            grabbedObject = null;
        }
    }

    IEnumerator Run()
    {
        if (nextDialogue < texts.Length && nowState == State.Next)
        {
            yield return PlayLine(texts[nextDialogue]);
            nextDialogue++;
        }
        else if (nextDialogue == texts.Length)
        {
            dialogObj.SetActive(false);
            okCanvas.SetActive(true);
            nextDialogue = 0;

            if (SkipNextCount == dialogueList.Count - 1)
            {
                SceneManager.LoadScene("race_track_lake");
            }
        }
    }

    IEnumerator PlayLine(string text)
    {
        nowState = State.Playing;
        for (int i = 0; i < text.Length + 1; i += 1)
        {
            yield return new WaitForSeconds(0.02f);
            uiText.text = text.Substring(0, i);
        }

        yield return new WaitForSeconds(0.5f);
        nowState = State.Next;
    }

    public void EndDrawing()
    {
        SkipNextCount++;
        CreateDialogueText(dialogueList[SkipNextCount]);
        dialogObj.SetActive(true);
        GrabberChange(false);
        StartCoroutine("Run");
    }
}
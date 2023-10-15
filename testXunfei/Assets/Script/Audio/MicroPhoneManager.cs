using MyManager;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace Ise
{
    public class MicroPhoneManager : MonoBehaviour, IPCEnd, ICanPlay
    {

        public int DeviceLength;

        private void Awake()
        {
            au = gameObject.GetComponent<AudioSource>();
            if (au == null)
            {
                au = gameObject.AddComponent<AudioSource>();
            }
            bStart = transform.Find("BStart").GetComponent<Button>();
            bStop = transform.Find("BStop").GetComponent<Button>();
            bPlay = transform.Find("BPlay").GetComponent<Button>();
            bEnd = transform.Find("BEnd").GetComponent<Button>();
            mask = transform.Find("Mask").gameObject;
            CountDownText = transform.Find("Time/CountDownText").GetComponent<Text>();
            
            gameObject.SetActive(false);

        }

        //private int Frequency = 16000; //录音频率
        //private int MicSecond = 2;  //每隔2秒，保存一下录音数据
        private Button bStart;
        private Button bStop;
        private Button bPlay;
        private Button bEnd;
        private GameObject mask;
      
        private AudioSource au;
        private string[] devices;
        private bool isHaveMicrophone = false;
        public Action OnOK;
        void Start()
        {
            WebYYPCPlus.Ins.pcEnd = this;
            WebGLSocket.Ins.OnCanPlay = this;
            bStart.onClick.AddListener(OnStartClick);
            bStop.onClick.AddListener(OnStopClick);
            bPlay.onClick.AddListener(OnPlayClick);
            bEnd.onClick.AddListener(OnEndClick);
            CountDownText.text = "时间";
            SetBntShow();
#if !UNITY_EDITOR && UNITY_WEBGL
#elif !NET_LEGACY
            devices = Microphone.devices;
            DeviceLength = devices.Length;
            if (devices.Length > 0)
            {
                isHaveMicrophone = true;
            }
            else
            {
                isHaveMicrophone = false;              
            }
#else
                throw new Exception("Scripting Runtime Version should be .NET 4.x, via Menu:\nPlayerSettings -> Other Settings -> Script Runtime Version -> .Net 4.x Equivalent");
#endif
        }
        public void OnStart()
        {
            CountDownText.text = "时间";
            SetBntShow();
        }
        private Text CountDownText;
        private int totalTime;
        public void SetCurrTime(int time, Action action = null)
        {
            totalTime = time;
            StopCoroutine("CountDownStart");
            StartCoroutine("CountDownStart");
        }
        private IEnumerator CountDownStart()
        {
            while (totalTime > 0)
            {
                yield return new WaitForSeconds(1f);
                totalTime--;
                CountDownText.text = string.Format("{0:D2}:{1:D2}", totalTime / 60, totalTime % 60);
            }           
                OnStopClick();
        }

        void SetBntShow()
        {
            bStart.gameObject.SetActive(true);
            bStop.gameObject.SetActive(false);
            bPlay.gameObject.SetActive(false);
            bEnd.gameObject.SetActive(false);
            mask.SetActive(false);
        }
        public void OnPCEnd()
        {
            mask.SetActive(false);
            bEnd.gameObject.SetActive(true);
            WebGLSocket.Ins.CloseAsync();
        }
       
        public void SetTopic(string value)
        {
            WebGLSocket.Ins.SetText(value);
        }
        void OnStartClick()
        {
            au.Stop();
            au.loop = false;
            au.mute = true;
            SetCurrTime(180);
            Invoke("ShowBStop", 2f);
            bStart.gameObject.SetActive(false);
#if !UNITY_EDITOR && UNITY_WEBGL
            WebGLSocket.Ins.OnStartRecorder();
#elif !NET_LEGACY
            StartMicrophone();
#endif
        }
        void ShowBStop()
        {
            bStop.gameObject.SetActive(true);
        }
        void OnStopClick()
        {
            bStop.gameObject.SetActive(false);
#if !UNITY_EDITOR && UNITY_WEBGL
            WebGLSocket.Ins.OnStopRecorder();
#elif !NET_LEGACY
            if (!Microphone.IsRecording(devices[0]))
                return;
            StopMicrophone();
#endif                       
            mask.SetActive(true);
            StopCoroutine("CountDownStart");
        }
        public void OnCanPlay()
        {
            bPlay.gameObject.SetActive(true);
        }
        void OnPlayClick()
        {
#if !UNITY_EDITOR && UNITY_WEBGL
            au.clip = WebGLSocket.Ins.audioClip;
#elif !NET_LEGACY
            if (Microphone.IsRecording(null))
                return;
            if (au.clip == null)
                return;
#endif
            au.mute = false;
            au.loop = false;
            au.Play();
        }
        void OnEndClick()
        {
            au.Stop();
            au.clip = null;
            gameObject.SetActive(false);
            if (OnOK != null)
            {
                OnOK();
                OnOK = null;
            }
        }
#if !UNITY_EDITOR && UNITY_WEBGL
# elif !NET_LEGACY
        void PlayAudioRecord(AudioClip newAudioClip)
        {
            if (isMicRecordFinished)
            {
                WebGLSocket.Ins.Headers(newAudioClip);
            }
            au.clip = newAudioClip;
            au.Stop();
            OnCanPlay();
        }
        public void StopMicrophone()
        {
            Debug.Log("Stop mic");
            isMicRecordFinished = true;
        }
        public delegate void AudioRecordHandle(AudioClip audioClip);

        AudioClip micClip;

        bool isMicRecordFinished = true;

        List<float> micDataList = new List<float>();
        float[] micDataTemp;

        string micName;

        public void StartMicrophone()
        {
            if (!isHaveMicrophone)
            {
                return;
            }
            StopCoroutine(StartMicrophone(null, PlayAudioRecord));
            StartCoroutine(StartMicrophone(null, PlayAudioRecord));
        }


        IEnumerator StartMicrophone(string microphoneName, AudioRecordHandle audioRecordFinishedEvent)
        {
            Debug.Log("Start Mic");
            micDataList = new List<float>();
            micName = microphoneName;
            micClip = Microphone.Start(micName, true, 2, 16000);
            isMicRecordFinished = false;
            int length = micClip.channels * micClip.samples;
            bool isSaveFirstHalf = true;//将音频从中间分生两段，然后分段保存
            int micPosition;
            while (!isMicRecordFinished)
            {
                if (isSaveFirstHalf)
                {
                    yield return new WaitUntil(() => { micPosition = Microphone.GetPosition(micName); return micPosition > length * 6 / 10 && micPosition < length; });//保存前半段
                    micDataTemp = new float[length / 2];
                    micClip.GetData(micDataTemp, 0);
                    micDataList.AddRange(micDataTemp);
                    isSaveFirstHalf = !isSaveFirstHalf;
                }
                else
                {
                    yield return new WaitUntil(() => { micPosition = Microphone.GetPosition(micName); return micPosition > length / 10 && micPosition < length / 2; });//保存后半段
                    micDataTemp = new float[length / 2];
                    micClip.GetData(micDataTemp, length / 2);
                    micDataList.AddRange(micDataTemp);
                    isSaveFirstHalf = !isSaveFirstHalf;
                }

            }
            micPosition = Microphone.GetPosition(micName);
            if (micPosition <= length)//前半段
            {
                micDataTemp = new float[micPosition / 2];
                micClip.GetData(micDataTemp, 0);
            }
            else
            {
                micDataTemp = new float[micPosition - length / 2];
                micClip.GetData(micDataTemp, length / 2);
            }
            micDataList.AddRange(micDataTemp);
            Microphone.End(micName);
            AudioClip newAudioClip = AudioClip.Create("RecordClip", micDataList.Count, 1, 16000, false);
            newAudioClip.SetData(micDataList.ToArray(), 0);
            audioRecordFinishedEvent(newAudioClip);
        }
#endif
    }
}
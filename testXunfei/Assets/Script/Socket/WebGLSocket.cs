using Ise;
using Json2Class;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityWebSocket;
using WWUtils.Audio;
namespace MyManager {
    public interface ICanPlay
    {
        void OnCanPlay();
    }
    public class WebGLSocket : MonoBehaviour
    {
        [DllImport("__Internal")]
        private static extern void Init();
        [DllImport("__Internal")]
        private static extern void StartRecorder();
        [DllImport("__Internal")]
        private static extern void StopRecorder();
        [DllImport("__Internal")]
        private static extern void ClickSelectFileBtn();
        [DllImport("__Internal")]
        private static extern string GetHTTPs();

        private static WebGLSocket ins;
        public static WebGLSocket Ins
        {
            get
            {
                return ins;
            }
        }
        private void Awake()
        {
            ins = this;
            OnInit();
        }
        private const string hostUrl = "wss://ise-api.xfyun.cn/v2/open-ise";//开放评测地址
        private const string appid = "5f30b825";//控制台获取    
        private const string apiSecret = "3c80a075ade1c456a2ef75a135f33432";//控制台获取  
        private const string apiKey = "01f942d2fd32ae58c200e12f85158c99";//控制台获取   

        private const string sub = "ise";//服务类型sub,开放评测值为ise
        private const string ent = "cn_vip";//语言标记参数 ent(cn_vip中文,en_vip英文)
        private const string rst = "entirety";//string 限制只能使用精简版 只有总分 entirety plain
                                              //题型、文本、音频要请注意做同步变更(如果是英文评测,请注意变更ent参数的值)
        private const string category = "read_chapter";//题型

        public const int StatusFirstFrame = 0;//第一帧
        public const int StatusContinueFrame = 1;//中间帧
        public const int StatusLastFrame = 2;//最后一帧

        string text;
        byte[] data;
        private IWebSocket socket;
        public AudioClip audioClip { get; set; }
        private ICanPlay _OnCanPlay;
        public ICanPlay OnCanPlay { set { _OnCanPlay = value; } }
        public IEnumerator onOpen()
        {

            //连接成功，开始发送数据
            int frameSize = 1280; //每一帧音频的大小,建议每 40ms 发送 1280B
            int pcmCount = 0;
            int pcmSize;
            int status = 0;  // 音频的状态
                             //string path = Application.streamingAssetsPath + "/Audio/cn_chapter.wav";

            Debug.Log(text);
            ssb(text);
            yield return new WaitForSeconds(0.01f);
            byte[] arr = data;//File.ReadAllBytes(path);
            if (arr == null)
            {

                yield break;
            }
            pcmSize = arr.Length;
            while (true)
            {
                if (pcmSize <= 2 * frameSize)
                {
                    frameSize = pcmSize;
                    status = StatusLastFrame;
                }
                if (frameSize <= 0)
                {
                    break;
                }
                byte[] buffer = new byte[frameSize];
                Array.Copy(arr, pcmCount, buffer, 0, frameSize);
                pcmCount += frameSize;
                pcmSize -= frameSize;

                switch (status)
                {
                    case StatusFirstFrame:   // 第一帧音频status = 0
                        send(1, 1, Convert.ToBase64String(buffer));
                        status = StatusContinueFrame;//中间帧数
                        break;

                    case StatusContinueFrame:  //中间帧status = 1
                        send(2, 1, Convert.ToBase64String(buffer));
                        break;

                    case StatusLastFrame:    // 最后一帧音频status = 2 ，标志音频发送结束
                        send(4, 2, Convert.ToBase64String(buffer));
                        break;
                }

                yield return new WaitForSeconds(0.01f);

            }

        }

        private void ssb(string text)
        {
            Root root = new Root()
            {
                common = new Common() { app_id = appid },
                business = new Business() { sub = sub, ent = ent, category = category, aue = "raw", auf = "audio/L16;rate=16000", rstcd = "utf8", cmd = "ssb", text = "\ufeff" + text, tte = "utf-8", ttp_skip = true, rst = rst },// , extra_ability = "syll_phone_err_msg", ise_unite="1" },
                data = new Json2Class.Data() { status = 0 }
            };

            //var str = JsonUtility.ToJson(root);
            var str = JsonConvert.SerializeObject(root);
            SendMsg(str);
            Debug.Log(str);
        }
        private void send(int aus, int status, string data)
        {
            string str = "{\"business\": {\"cmd\": \"auw\", \"aus\":" + aus + " },\"data\":{\"status\": " + status + ",\"data\":\"" + data + "\"}}";
            SendMsg(str);
        }
        private void OnInit()
        {
#if !UNITY_EDITOR && UNITY_WEBGL
        Init();
#elif !NET_LEGACY
#else
                                            throw new Exception("Scripting Runtime Version should be .NET 4.x, via Menu:\nPlayerSettings -> Other Settings -> Script Runtime Version -> .Net 4.x Equivalent");
#endif

            socket = new WebSocket(AuthUrl);
            socket.OnOpen += Socket_OnOpen;
            socket.OnMessage += Socket_OnMessage;
            socket.OnClose += Socket_OnClose;
            socket.OnError += Socket_OnError;
        }
        public void CloseAsync()
        {
            socket.CloseAsync();
        }
        public void SetText(string value)
        {
            text = value;
        }
        public void Headers(AudioClip newAudioClip)
        {
            data = ConvertClipToBytes(newAudioClip);
            socket.ConnectAsync();
        }
        private void Socket_OnError(object sender, UnityWebSocket.ErrorEventArgs e)
        {
            OnError(e.Message);
        }

        private void Socket_OnClose(object sender, CloseEventArgs e)
        {
            OnClose();
        }

        private void Socket_OnMessage(object sender, MessageEventArgs e)
        {
            OnMessageString(e.Data);
        }

        private void Socket_OnOpen(object sender, OpenEventArgs e)
        {
            StartCoroutine(onOpen());

            OnOpen();
        }

        public void OnStartRecorder()
        {
            StartRecorder();
        }
        public void OnStopRecorder()
        {
            StopRecorder();
        }
        
        public void OnClickSelectFileBtn()
        {
            ClickSelectFileBtn();
        }
        StringBuilder stringBuilder = new StringBuilder();
        public void GetBase64(string base64Str)
        {
            if (base64Str == "Start")
            {
                stringBuilder.Clear();
            }
            else if (base64Str == "End")
            {
                byte[] bs = Convert.FromBase64String(stringBuilder.ToString());
               
                    //iSetText.SetDraftText(System.Text.Encoding.UTF8.GetString(bs));  //传文本              

            }
            else
            {
                stringBuilder.Append(base64Str);
            }
        }
        public void OnMessageString(string msg)
        {
            if (string.IsNullOrEmpty(msg))
            {
                return;
            }

            WebYYPCPlus.Ins.onMessage(msg);
        }


        public void OnOpen()
        {
            Debug.Log("打开");

        }
        public void OnError(string value)
        {
            Debug.Log("错误：" + value);
        }

        public void OnClose()
        {
            Debug.Log("关闭：");

        }
        public void SendMsg(string json)
        {
            socket.SendAsync(json);
        }
        #region 处理音频


        int m_valuePartCount = 0;
        int m_audioLength = 0;
        string m_currentRecorderSign;
        string[] m_audioData;
        int m_getDataLength;

        private byte[] ConvertClipToBytes(AudioClip clip)
        {
            float[] samples = new float[clip.samples];

            clip.GetData(samples, 0);

            short[] intData = new short[samples.Length];


            byte[] bytesData = new byte[samples.Length * 2];

            int rescaleFactor = 32767;

            for (int i = 0; i < samples.Length; i++)
            {
                intData[i] = (short)(samples[i] * rescaleFactor);
                byte[] byteArr = new byte[2];
                byteArr = BitConverter.GetBytes(intData[i]);
                byteArr.CopyTo(bytesData, i * 2);
            }

            return bytesData;
        }
        public void GetAudioData(string _audioDataString)
        {
            if (_audioDataString.Contains("Head"))
            {
                string[] _headValue = _audioDataString.Split('|');
                m_valuePartCount = int.Parse(_headValue[1]);
                m_audioLength = int.Parse(_headValue[2]);
                m_currentRecorderSign = _headValue[3];
                m_audioData = new string[m_valuePartCount];
                m_getDataLength = 0;
            }
            else if (_audioDataString.Contains("Part"))
            {
                string[] _headValue = _audioDataString.Split('|');
                int _dataIndex = int.Parse(_headValue[1]);
                m_audioData[_dataIndex] = _headValue[2];
                m_getDataLength++;
                if (m_getDataLength == m_valuePartCount)
                {
                    StringBuilder stringBuilder = new StringBuilder();
                    for (int i = 0; i < m_audioData.Length; i++)
                    {
                        stringBuilder.Append(m_audioData[i]);
                    }
                    string _audioDataValue = stringBuilder.ToString();
                    Debug.Log("接收长度:" + _audioDataValue.Length + " 需接收长度:" + m_audioLength);
                    int _index = _audioDataValue.LastIndexOf(',');
                    string _value = _audioDataValue.Substring(_index + 1, _audioDataValue.Length - _index - 1);
                    data = Convert.FromBase64String(_value);
                    //Debug.Log("已接收长度 :" + data.Length);
                    WWUtils.Audio.WAV wav = new WAV(data);
                    audioClip = AudioClip.Create("wavClip", wav.SampleCount, 1, wav.Frequency, false);
                    audioClip.SetData(wav.LeftChannel, 0);
                    Debug.Log(audioClip);
                    socket.ConnectAsync();
                    if (_OnCanPlay != null)
                    {
                        _OnCanPlay.OnCanPlay();
                    }
                }
            }
        }
        #endregion
        public string AuthUrl
        {
            get
            {
                string url = getAuthUrl(hostUrl, apiKey, apiSecret);

#if !UNITY_EDITOR && UNITY_WEBGL
 if (!GetHTTPs().Contains("https"))
                { 
                url.Replace("wss", "ws");
                }
#endif
                return url;
            }
        }

        private string getAuthUrl(string hostUrl, string apiKey, string apiSecret)
        {
            Uri uri = new Uri(hostUrl);
            TimeSpan ts = DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, 0);
            string date = DateTime.UtcNow.ToUniversalTime().ToString(CultureInfo.CurrentCulture.DateTimeFormat.RFC1123Pattern, new CultureInfo("en-us"));

            StringBuilder builder = new StringBuilder("host: ").Append(uri.Host).Append("\n").//
                    Append("date: ").Append(date).Append("\n").//
                    Append("GET ").Append(uri.LocalPath).Append(" HTTP/1.1");

            byte[] hexDigits;
            string sha;
            using (var hmacsha256 = new HMACSHA256(Encoding.UTF8.GetBytes(apiSecret)))
            {
                hexDigits = hmacsha256.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString()));
                sha = Convert.ToBase64String(hexDigits);
            }

            string authorization = string.Format("hmac username=\"{0}\", algorithm=\"{1}\", headers=\"{2}\", signature=\"{3}\"", apiKey, "hmac-sha256", "host date request-line", sha);


            hostUrl += "?authorization=" + Convert.ToBase64String(Encoding.UTF8.GetBytes(authorization)) + "&date=" + date + "&host=" + uri.Host;

            return hostUrl;
        }
    } }

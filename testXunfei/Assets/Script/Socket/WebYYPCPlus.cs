using Json2Class;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using UnityEngine;

namespace Ise
{
    public interface IPCEnd
    {
        void OnPCEnd();
    }
    public class WebYYPCPlus
    {
       
        private IPCEnd _IPCEnd;
        public IPCEnd pcEnd { set { _IPCEnd = value; } }
        private static WebYYPCPlus _ins;
        public static WebYYPCPlus Ins
        {
            get
            {
                if (_ins==null)
                {
                    _ins = new WebYYPCPlus();
                }
                return _ins;
            }
        }
       
      
        StringBuilder _json;
        bool _last = false;
        public void onMessage(string json)
        {

            Debug.Log(json);
            try
            {
                var resp = JsonConvert.DeserializeObject<IseNewResponseData>(json);

                if (resp.code != 0)
                {
                    string str = "错误码：" + resp.code + "详情查看：科大讯飞语音评测（流式版）错误码";
                    if (resp.code == 48395)
                    {
                        str = "文本格式不正确 UTF-8";
                    }                    
                    Debug.LogError(str+ "https://www.xfyun.cn/doc/Ise/IseAPI.html#%E9%94%99%E8%AF%AF%E7%A0%81");
                }
                
            }
            catch (Exception e)
            {
                Debug.Log(e.Message);
            }
            if (_last)
            {
                if (!json.Contains("{"))
                {
                    _json.Append(json);
                }
            }
            if (!_last && !json.Contains("null"))
            {
                //第一条最后结果
                _json = new StringBuilder(json);
                _last = true;

            }
            if (_last && _json.ToString().Contains("}}"))
            {
                _json.Replace("\0", "");
                json = _json.ToString();
                //var resp = JsonUtility.FromJson<IseNewResponseData>(json);
                var resp = JsonConvert.DeserializeObject<IseNewResponseData>(json);

                if (resp.code != 0)
                {
                    Debug.LogError(json);
                }
                if (resp.data.status == 2)
                {

                    byte[] myByte = System.Convert.FromBase64String(resp.data.data);
                    string txt = Encoding.UTF8.GetString(myByte);

                    AnalysisResultXML(txt);
                }
                _last = false;
            }

        }
        /// <summary>
        /// 解析XML
        /// </summary>
        Dictionary<string, string> Overall;// = new Dictionary<string, string>();
        List<TxtData> wroingList;// = new List<TxtData>();
        List<Dictionary<string, string>> _allData = new List<Dictionary<string, string>>();
        List<List<TxtData>> _allWroing = new List<List<TxtData>>();
        public List<Dictionary<string, string>> AllData { get { return _allData; } }
        public List<List<TxtData>> AllWroing { get { return _allWroing; } }
        public void AnalysisResultXML(string xml)
        {
            string[] read_chapter;
            try
            {
                XmlDocument doc = new XmlDocument();
                //doc.Load(path);
                doc.LoadXml(xml);
                //获取根节点---此处一定要添加双斜杠
                XmlNodeList nodeList = doc.SelectNodes("//rec_paper");
                Overall = new Dictionary<string, string>();
                wroingList = new List<TxtData>();
                //遍历输出.
                foreach (XmlElement node in nodeList)
                {
                    string str = node.InnerXml.Split('>')[0];
                    read_chapter = str.Split(' ');
                    Syll.SetDic(read_chapter, Overall);
                    //输出结果
                    Debug.Log(Overall["total_score"] + "--" + Overall["tone_score"] + "--" + Overall["phone_score"] + "--"+ Overall["integrity_score"]);
                }
                 nodeList = doc.SelectNodes("//word");
                //遍历解析.
                foreach (XmlElement node in nodeList)
                {
                    List<string> sub_strs = new List<string>();
                    string[] strs = node.InnerXml.Split('>');
                    for (int i = 0; i < strs.Length; i++)
                    {
                        if (strs[i].Contains("rec_node_type=\"paper\""))
                        {
                            sub_strs.Add(strs[i]);
                        }
                    }
                   
                    Syll syll = new Syll(sub_strs);
                    if (!syll.IsCorrect)
                    {
                        wroingList.Add(syll.txtData);
                    }

                }
                _allData.Add(Overall);
                _allWroing.Add(wroingList);
                if (_IPCEnd!=null)
                {
                    _IPCEnd.OnPCEnd();
                }

            }
            catch (Exception ex)
            {               
                Debug.Log(ex.Message);
            }
        }     
       
        [Serializable]
        public class IseNewResponseData
        {
            public int code;
            public string message;
            public string sid;
            public ResPonseData data;
        }
        [Serializable]
        public class ResPonseData
        {
            public int status;
            public string data;
        }
        public class Syll
        {
           
            public TxtData txtData = new TxtData();
            public Syll(List<string> sub_strs)
            {
                Dictionary<string, string> syll = new Dictionary<string, string>();
                Dictionary<string, string> phone = new Dictionary<string, string>();
                Dictionary<string, string> phone1 = new Dictionary<string, string>();
               
                SetDic(sub_strs[0].Split(' '), syll);
                SetDic(sub_strs[1].Split(' '), phone);
                SetDic(sub_strs[2].Split(' '), phone1);
                txtData.content = syll["content"];
                txtData.symbol = syll["symbol"];
                txtData.perr_msg = (Error)int.Parse(phone["perr_msg"]);
                txtData.tone_perr_msg = (Error)int.Parse(phone1["perr_msg"]);
            }
            public static void SetDic(string[] strs, Dictionary<string, string> dic)
            {
                for (int i = 0; i < strs.Length; i++)
                {
                    string str = strs[i];
                    if (str.Contains("="))
                    {
                        string[] sub_str = strs[i].Split('=');
                        dic.Add(sub_str[0].Replace("\"", string.Empty), sub_str[1].Replace("\"", string.Empty));
                    }
                }
            }
            public bool IsCorrect
            {
                get
                {
                    if (txtData.perr_msg != Error.Correct || txtData.tone_perr_msg != Error.Correct)
                    {
                        return false;
                    }
                    return true;
                }
            }
        }
       
    }
    public interface IResultEnd
    {
        void OnAnalysisResult();
    }
    public struct TxtData
    {
        public string content;
        public string symbol;
        public Error perr_msg;
        public Error tone_perr_msg;
    }
    public enum Error
    {
        Correct=0,
        ReadMiss = 16,
        AddRead= 32,
        BackwardRead= 64,
        Replace=128,
        Unison = 1,
        Pattern=2,
        UnisonPattern =3,
    }
}

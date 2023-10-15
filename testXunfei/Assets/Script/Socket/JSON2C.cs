using System;
namespace Json2Class
{
    [Serializable]
    public class Common
    {
        /// <summary>
        /// 
        /// </summary>
        public string app_id { get; set; }
    }
    [Serializable]
    public class Business
    {
        /// <summary>
        /// 
        /// </summary>
        public string sub { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string ent { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string category { get; set; }

        /// <summary>
        /// 
        /// </summary>
        //public int aus { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string cmd { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string text { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string tte { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public bool ttp_skip { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public string aue { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string auf { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string rstcd { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string rst { get; set; }
        /// <summary>
        /// 
        /// </summary>
        //public string extra_ability { get; set; }
        ///// <summary>
        ///// 
        ///// </summary>
        //public string ise_unite { get; set; }
    }
    [Serializable]
    public class Data
    {
        /// <summary>
        /// 
        /// </summary>
        public int status { get; set; }
    }
    [Serializable]
    public class Root
    {
        /// <summary>
        /// 
        /// </summary>
        public Common common { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public Business business { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public Data data { get; set; }
    }
}
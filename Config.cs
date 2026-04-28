using System;
using System.IO;
using Newtonsoft.Json.Linq;

namespace OpenSurge
{
    class Config
    {
        public string SessionFile     { get; set; }
        public string BaseUrl         { get; set; }
        public int    Threads         { get; set; }
        public int    DurationSeconds { get; set; }
        public int    WarmupSeconds   { get; set; }
        public bool   Ramp            { get; set; }
        public bool   Shuffle         { get; set; }
        public string BearerToken     { get; set; }

        public Config()
        {
            SessionFile     = "";
            BaseUrl         = "";
            Threads         = 5;
            DurationSeconds = 60;
            WarmupSeconds   = 0;
            Ramp            = false;
            Shuffle         = false;
            BearerToken     = "";
        }

        public static Config Load(string path)
        {
            var c = new Config();
            if (!File.Exists(path)) return c;
            try
            {
                var obj = JObject.Parse(File.ReadAllText(path));
                if (obj["sessionfile"]     != null) c.SessionFile     = (string)obj["sessionfile"]     ?? "";
                if (obj["baseurl"]         != null) c.BaseUrl         = (string)obj["baseurl"]         ?? "";
                if (obj["threads"]         != null) c.Threads         = (int)   obj["threads"];
                if (obj["durationseconds"] != null) c.DurationSeconds = (int)   obj["durationseconds"];
                if (obj["warmupseconds"]   != null) c.WarmupSeconds   = (int)   obj["warmupseconds"];
                if (obj["ramp"]            != null) c.Ramp            = (bool)  obj["ramp"];
                if (obj["shuffle"]         != null) c.Shuffle         = (bool)  obj["shuffle"];
                if (obj["bearertoken"]     != null) c.BearerToken     = (string)obj["bearertoken"]     ?? "";
            }
            catch (Exception ex) { Logger.Error(ex); }
            return c;
        }

        public void Save(string path)
        {
            try
            {
                var obj = new JObject();
                obj["sessionfile"]     = SessionFile;
                obj["baseurl"]         = BaseUrl;
                obj["threads"]         = Threads;
                obj["durationseconds"] = DurationSeconds;
                obj["warmupseconds"]   = WarmupSeconds;
                obj["ramp"]            = Ramp;
                obj["shuffle"]         = Shuffle;
                obj["bearertoken"]     = BearerToken;
                File.WriteAllText(path, obj.ToString());
            }
            catch (Exception ex) { Logger.Error(ex); }
        }
    }
}

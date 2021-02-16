using Newtonsoft.Json.Linq;
using Rainmeter;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace PluginNeteasePlaying
{
    internal class Measure
    {
        internal enum MeasureType
        {
            SongName,
            Artists,
            Album,
            Duration,
            PlayedTime,
            Lyric,
            IsLyricTranslated,
            TranslatedLyric
        }

        internal MeasureType Type = MeasureType.SongName;

        internal virtual void Dispose()
        {
        }

        internal virtual void Reload(Rainmeter.API api, ref double maxValue)
        {
            string type = api.ReadString("Type", "");
            switch (type.ToLowerInvariant())
            {
                case "name":
                    this.Type = MeasureType.SongName;
                    break;

                case "artists":
                    this.Type = MeasureType.Artists;
                    break;

                case "album":
                    this.Type = MeasureType.Album;
                    break;

                case "lyric":
                    this.Type = MeasureType.Lyric;
                    break;

                case "islyrictranslated":
                    this.Type = MeasureType.IsLyricTranslated;
                    break;

                case "translatedlyric":
                    this.Type = MeasureType.TranslatedLyric;
                    break;

                default:
                    api.Log(API.LogType.Error, "ParentChild.dll: Type=" + type + " not valid");
                    break;
            }
        }

        internal virtual double Update()
        {
            return 0.0;
        }

        internal virtual string GetString()
        {
            return null;
        }
    }

    internal class ParentMeasure : Measure
    {
        // This list of all parent measures is used by the child measures to find their parent.
        internal static List<ParentMeasure> ParentMeasures = new List<ParentMeasure>();

        internal string Name;
        internal IntPtr Skin;

        internal string HistoryJsonPath;
        internal JArray HistoryJson;

        internal ParentMeasure()
        {
            ParentMeasures.Add(this);
        }

        internal override void Dispose()
        {
            ParentMeasures.Remove(this);
        }

        internal override void Reload(Rainmeter.API api, ref double maxValue)
        {
            base.Reload(api, ref maxValue);

            Name = api.GetMeasureName();
            Skin = api.GetSkin();

            this.HistoryJsonPath = api.ReadString("HistoryJsonPath", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Netease/CloudMusic/webdata/file/history"));
        }

        internal override double Update()
        {
            try
            {
                this.HistoryJson = JArray.Parse(File.ReadAllText(this.HistoryJsonPath));
            }
            catch (Exception)
            {

            }

            return GetValue(Type);
        }

        internal double GetValue(MeasureType type)
        {
            switch (type)
            {
                case MeasureType.Duration:
                    int.TryParse(this.HistoryJson[0]["track"]["duration"].ToString(), out int duration);
                    return duration;

                case MeasureType.PlayedTime:
                    long.TryParse(this.HistoryJson[0]["time"].ToString(), out long startTime);
                    long nowTime = Convert.ToInt64((DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, 0)).TotalMilliseconds);
                    return nowTime - startTime;
            }

            return 0.0;
        }

        internal string GetString(MeasureType type)
        {
            switch (type)
            {
                case MeasureType.SongName:
                    return this.HistoryJson[0]["track"]["name"].ToString();
                case MeasureType.Artists:
                    return this.HistoryJson[0]["track"]["artists"][0]["name"].ToString();
                case MeasureType.Album:
                    return this.HistoryJson[0]["track"]["album"]["name"].ToString();
                case MeasureType.Lyric:
                    return "";
                case MeasureType.IsLyricTranslated:
                    return "";
                case MeasureType.TranslatedLyric:
                    return "";
            }

            return null;
        }
    }

    internal class ChildMeasure : Measure
    {
        private ParentMeasure ParentMeasure = null;

        internal override void Reload(Rainmeter.API api, ref double maxValue)
        {
            base.Reload(api, ref maxValue);

            string parentName = api.ReadString("ParentName", "");
            IntPtr skin = api.GetSkin();

            // Find parent using name AND the skin handle to be sure that it's the right one.
            ParentMeasure = null;
            foreach (ParentMeasure parentMeasure in ParentMeasure.ParentMeasures)
            {
                if (parentMeasure.Skin.Equals(skin) && parentMeasure.Name.Equals(parentName))
                {
                    ParentMeasure = parentMeasure;
                }
            }

            if (ParentMeasure == null)
            {
                api.Log(API.LogType.Error, "ParentChild.dll: ParentName=" + parentName + " not valid");
            }
        }

        internal override double Update()
        {
            if (ParentMeasure != null)
            {
                return ParentMeasure.GetValue(Type);
            }

            return 0.0;
        }

        internal override string GetString()
        {
            if (ParentMeasure != null)
            {
                return ParentMeasure.GetString(Type);
            }

            return null;
        }
    }

    public static class Plugin
    {
        private static IntPtr StringBuffer = IntPtr.Zero;

        [DllExport]
        public static void Initialize(ref IntPtr data, IntPtr rm)
        {
            Rainmeter.API api = new Rainmeter.API(rm);

            string parent = api.ReadString("ParentName", "");
            Measure measure;
            if (String.IsNullOrEmpty(parent))
            {
                measure = new ParentMeasure();
            }
            else
            {
                measure = new ChildMeasure();
            }

            data = GCHandle.ToIntPtr(GCHandle.Alloc(measure));
        }

        [DllExport]
        public static void Finalize(IntPtr data)
        {
            Measure measure = (Measure)GCHandle.FromIntPtr(data).Target;
            measure.Dispose();
            GCHandle.FromIntPtr(data).Free();
        }

        [DllExport]
        public static void Reload(IntPtr data, IntPtr rm, ref double maxValue)
        {
            Measure measure = (Measure)GCHandle.FromIntPtr(data).Target;
            measure.Reload(new Rainmeter.API(rm), ref maxValue);
        }

        [DllExport]
        public static double Update(IntPtr data)
        {
            Measure measure = (Measure)GCHandle.FromIntPtr(data).Target;
            return measure.Update();
        }

        [DllExport]
        public static IntPtr GetString(IntPtr data)
        {
            Measure measure = (Measure)GCHandle.FromIntPtr(data).Target;
            if (StringBuffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(StringBuffer);
                StringBuffer = IntPtr.Zero;
            }

            string stringValue = measure.GetString();
            if (stringValue != null)
            {
                StringBuffer = Marshal.StringToHGlobalUni(stringValue);
            }

            return StringBuffer;
        }
    }
}

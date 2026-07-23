using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Sma5h.Mods.Music.ReverseBuild
{
    public partial class MusicModReverseService
    {
        private static bool JsonEquals<T>(T left, T right)
        {
            return JToken.DeepEquals(ToToken(left), ToToken(right));
        }

        private static JToken ToToken<T>(T value)
        {
            return value == null ? JValue.CreateNull() : JToken.FromObject(value);
        }

        private static Dictionary<string, T> LoadDictionary<T>(string path)
        {
            if (!File.Exists(path))
                return new Dictionary<string, T>();

            return JsonConvert.DeserializeObject<Dictionary<string, T>>(File.ReadAllText(path)) ?? new Dictionary<string, T>();
        }

        private static void MergeDictionaryFile<T>(string path, Dictionary<string, T> newValues)
        {
            if (newValues == null || newValues.Count == 0)
                return;

            var existing = LoadDictionary<T>(path);
            foreach (var newValue in newValues)
                existing[newValue.Key] = newValue.Value;

            WriteJson(path, existing);
        }

        private static void WriteJson(string path, object value)
        {
            File.WriteAllText(path, JsonConvert.SerializeObject(value, DefaultFormatting), new UTF8Encoding(false));
        }
    }
}

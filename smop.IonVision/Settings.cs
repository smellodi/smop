using System;
using System.Diagnostics;
using System.Text.Json;

namespace Smop.IonVision
{
    internal class Settings
    {
        public static Settings Instance => _instance ??= new Settings();

        public string IP => _properties?.IP ?? "localhost";
        public string Project => _properties?.Project ?? "Smellodi";
        public string ParameterId => _properties?.ParameterId ?? "UUID";
        public string ParameterName => _properties?.ParameterName ?? "Default";
        public string User => _properties?.User ?? "TUNI";

        // Internal

        record class Properties(string IP, string Project, string ParameterId, string ParameterName, string User);

        static Settings? _instance = null;

        readonly Properties? _properties = null;

        private Settings()
        {
            try
            {
                System.IO.StreamReader reader = new("IonVision.json");
                string jsonString = reader.ReadToEnd();

                JsonSerializerOptions serializerOptions = new()
                {
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    PropertyNameCaseInsensitive = true,
                };

                _properties = JsonSerializer.Deserialize<Properties>(jsonString, serializerOptions)!;
                if (_properties == null)
                {
                    throw new Exception("Cannot convert the string to a json list of key-value pairs");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                throw new Exception(ex.Message);
            }
        }
    }
}

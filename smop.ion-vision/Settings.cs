using System;
using System.Diagnostics;
using System.Text.Json;

namespace Smop.IonVision;

public class Settings
{
    public static string DefaultFilename = "IonVision.json";
    public string IP => _properties?.IP ?? "localhost";
    public string Project => _properties?.Project ?? "Smellodi";
    public string ParameterId => _properties?.ParameterId ?? "GUID";
    public string ParameterName => _properties?.ParameterName ?? "Default";
    public string User => _properties?.User ?? "TUNI";


    public Settings(string? filename = null)
    {
        filename = filename ?? DefaultFilename;

        try
        {
            Debug.WriteLine($"[IonVis] settings from: {filename}");

            System.IO.StreamReader reader = new(filename);
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
        }
    }

    // Internal

    record class Properties(string IP, string Project, string ParameterId, string ParameterName, string User);

    readonly Properties? _properties = null;
}

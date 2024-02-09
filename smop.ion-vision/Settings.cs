using Smop.Common;
using System;
using System.Text.Json;

namespace Smop.IonVision;

public class Settings
{
    public string IP
    {
        get => _properties.IP;
        set { _properties = _properties with { IP = value }; }
    }
    public string Project
    {
        get => _properties.Project;
        set { _properties = _properties with { Project = value }; }
    }
    public string ParameterId
    {
        get => _properties.ParameterId;
        set { _properties = _properties with { ParameterId = value }; }
    }
    public string ParameterName
    {
        get => _properties.ParameterName;
        set { _properties = _properties with { ParameterName = value }; }
    }
    public string? User
    {
        get => _properties.User;
        set { _properties = _properties with { User = value }; }
    }


    public Settings(string? filename = null)
    {
        _filename = filename ?? DEFAULT_FILENAME;

        try
        {
            ScreenLogger.Print($"[IvSettings] settings from: {_filename}");

            using System.IO.StreamReader reader = new(_filename);
            string jsonString = reader.ReadToEnd();

            JsonSerializerOptions serializerOptions = new()
            {
                ReadCommentHandling = JsonCommentHandling.Skip,
                PropertyNameCaseInsensitive = true,
            };

            _properties = JsonSerializer.Deserialize<Properties>(jsonString, serializerOptions)!;
            if (_properties == null)
            {
                _properties = DEFAULT_PROPERTIES with { };
                throw new Exception("Cannot convert the string to a json list of key-value pairs");
            }
        }
        catch (Exception ex)
        {
            ScreenLogger.Print($"[IvSettings] {ex.Message}");
        }
    }

    public void Save()
    {
        JsonSerializerOptions serializerOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true,
        };

        string jsonString = JsonSerializer.Serialize(_properties, serializerOptions)!;

        using System.IO.StreamWriter writer = new(_filename);
        writer.Write(jsonString);
    }

    // Internal

    record class Properties(string IP, string Project, string ParameterId, string ParameterName, string? User);

    readonly static string DEFAULT_FILENAME = "IonVision.json";
    readonly static Properties DEFAULT_PROPERTIES = new("localhost", "Smellodi", "GUID", "Default", null);

    readonly string _filename;

    Properties _properties = DEFAULT_PROPERTIES with { };   // copying|
}

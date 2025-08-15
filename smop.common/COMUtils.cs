using FTD2XX_NET;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Management;
using System.Threading;

namespace Smop.Common;

/// <summary>
/// COM port utils: list of available ports, and firing events when this list changes.
/// </summary>
public class COMUtils : IDisposable
{
    /// <summary>
    /// Port descriptor
    /// </summary>
    public class Port(string id, string name, string? description, string? manufacturer)
    {
        public string ID { get; } = id;
        public string Name { get; } = name;
        public string? Description { get; } = description;
        public string? Manufacturer { get; } = manufacturer;
    }

    /// <summary>
    /// Fires when a COM port becomes available
    /// </summary>
    public event EventHandler<Port>? Inserted;
    /// <summary>
    /// Fires when a COM port is removed
    /// </summary>
    public event EventHandler<Port>? Removed;

    /// <summary>
    /// List of all COM ports in the system
    /// </summary>
    public Port[] Ports => GetAvailableCOMPorts();

    /// <summary>
    /// List of all COM ports in the system
    /// </summary>
    public static Port[] FtdiPorts => GetAvailableFDTIPorts();

    /// <summary>
    /// Most likely SMOP port, i.e. the one that has a known description
    /// </summary>
    public static Port? OdorDisplayPort => FtdiPorts.FirstOrDefault(port => port.Manufacturer?.Contains("TUNI") ?? false);

    public COMUtils()
    {
        Listen("__InstanceCreationEvent", "Win32_USBControllerDevice", ActionType.Inserted);    // Win32_SerialPort
        Listen("__InstanceDeletionEvent", "Win32_USBControllerDevice", ActionType.Removed);
    }

    public void Dispose()
    {
        foreach (var w in _watchers)
        {
            w.Dispose();
        }

        _watchers.Clear();

        GC.SuppressFinalize(this);
    }

    // Internal

    private enum ActionType
    {
        Inserted,
        Removed
    }

    const int FtdiCommandInterval = 50; // ms

    readonly List<Port> _cachedPorts = new();

    readonly List<ManagementEventWatcher> _watchers = new();

    private void Listen(string source, string target, ActionType actionType)
    {
        var query = new WqlEventQuery($"SELECT * FROM {source} WITHIN 2 WHERE TargetInstance ISA '{target}'");
        var watcher = new ManagementEventWatcher(query);

        watcher.EventArrived += (s, e) =>
        {
            try
            {
                using var target = (ManagementBaseObject)e.NewEvent["TargetInstance"];
                switch (actionType)
                {
                    case ActionType.Inserted:
                        var port = CreateCOMPort(target.Properties);
                        if (port != null)
                        {
                            _cachedPorts.Add(port);
                            Inserted?.Invoke(this, port);
                        }
                        break;
                    case ActionType.Removed:
                        var deviceID = GetDeviceID(target.Properties);
                        if (deviceID != null)
                        {
                            var cachedPort = _cachedPorts.FirstOrDefault(port => port.ID == deviceID);
                            if (cachedPort != null)
                            {
                                _cachedPorts.Remove(cachedPort);
                                Removed?.Invoke(this, cachedPort);
                            }
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                ScreenLogger.Print("USB ERROR: " + ex.Message);
            }
        };

        _watchers.Add(watcher);

        try
        {
            watcher.Start();
        }
        catch (Exception ex)
        {
            ScreenLogger.Print(ex.Message);
        }
    }

    private static Port[] GetAvailableFDTIPorts()
    {
        Port[] ports = [];

        try
        {
            var ftdi = new FTDI();

            uint deviceCount = 0;
            var response = ftdi.GetNumberOfDevices(ref deviceCount);
            System.Diagnostics.Debug.WriteLine($"[FTDI] Device count: {deviceCount}");

            if (deviceCount == 0)
                return ports;

            var devices = new FTDI.FT_DEVICE_INFO_NODE[deviceCount];

            int attemptsLeft = 100; // weirdly, the device descriptor becomes valid only after reading it multiple times
            do
            {
                Thread.Sleep(FtdiCommandInterval);
                response = ftdi.GetDeviceList(devices);
            } while (devices[0].LocId == 0 && attemptsLeft-- > 0);

            Thread.Sleep(FtdiCommandInterval);
            System.Diagnostics.Debug.WriteLine($"[FTDI] Got device descriptors ({response}), after {100 - attemptsLeft} attempts");

            ports = devices.Select(dev =>
                {
                    Port? result = null;

                    try
                    {
                        response = ftdi.OpenByLocation(dev.LocId);
                        Thread.Sleep(FtdiCommandInterval);
                        System.Diagnostics.Debug.WriteLine($"[FTDI] Device '{dev.SerialNumber}' opened ({response})");
                        if (response != FTDI.FT_STATUS.FT_OK)
                            return null;

                        response = ftdi.GetCOMPort(out string comName);
                        Thread.Sleep(FtdiCommandInterval);
                        System.Diagnostics.Debug.WriteLine($"[FTDI] Got COM port name ({response})");
                        if (response != FTDI.FT_STATUS.FT_OK)
                            return null;

                        FTDI.FT_EEPROM_DATA data;
                        response = dev.Type switch
                        {
                            FTDI.FT_DEVICE.FT_DEVICE_232R => ftdi.ReadFT232REEPROM((FTDI.FT232R_EEPROM_STRUCTURE)(data = new FTDI.FT232R_EEPROM_STRUCTURE())),
                            FTDI.FT_DEVICE.FT_DEVICE_232H => ftdi.ReadFT232HEEPROM((FTDI.FT232H_EEPROM_STRUCTURE)(data = new FTDI.FT232H_EEPROM_STRUCTURE())),
                            FTDI.FT_DEVICE.FT_DEVICE_2232 => ftdi.ReadFT2232EEPROM((FTDI.FT2232_EEPROM_STRUCTURE)(data = new FTDI.FT2232_EEPROM_STRUCTURE())),
                            FTDI.FT_DEVICE.FT_DEVICE_2232H => ftdi.ReadFT2232HEEPROM((FTDI.FT2232H_EEPROM_STRUCTURE)(data = new FTDI.FT2232H_EEPROM_STRUCTURE())),
                            FTDI.FT_DEVICE.FT_DEVICE_4232H => ftdi.ReadFT4232HEEPROM((FTDI.FT4232H_EEPROM_STRUCTURE)(data = new FTDI.FT4232H_EEPROM_STRUCTURE())),
                            FTDI.FT_DEVICE.FT_DEVICE_X_SERIES => ftdi.ReadXSeriesEEPROM((FTDI.FT_XSERIES_EEPROM_STRUCTURE)(data = new FTDI.FT_XSERIES_EEPROM_STRUCTURE())),
                            _ => ftdi.ReadFT232BEEPROM((FTDI.FT232B_EEPROM_STRUCTURE)(data = new FTDI.FT232B_EEPROM_STRUCTURE())),
                        };

                        Thread.Sleep(FtdiCommandInterval);
                        System.Diagnostics.Debug.WriteLine($"[FTDI] Got data ({response})");
                        if (response != FTDI.FT_STATUS.FT_OK)
                            return null;

                        result = new Port(dev.SerialNumber, comName, dev.Description, data.Manufacturer ?? dev.SerialNumber); //-V3080
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[USB] {ex.Message}");
                    }
                    finally
                    {
                        if (ftdi.IsOpen)
                        {
                            ftdi.Close();
                        }
                    }
                    return result;
                })
                .Where(port => port != null)
                .Select(port => port!)
                .ToArray();
        }
        catch { }

        return ports;
    }

    private Port[] GetAvailableCOMPorts()
    {
        var portNames = SerialPort.GetPortNames();

        IEnumerable<Port>? ports = null;

        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity WHERE Caption LIKE '%(COM%' OR Caption LIKE '%Smellodi%'");
            ManagementBaseObject[]? records = searcher.Get().Cast<ManagementBaseObject>().ToArray();
            ports = records.Select(rec =>
                {
                    var id = rec["DeviceID"]?.ToString() ?? string.Empty;
                    var name =
                        portNames.FirstOrDefault(name => rec["Caption"]?.ToString()?.Contains($"({name})") ?? false) ??
                        portNames.FirstOrDefault(name => rec["DeviceID"]?.ToString()?.Contains($"{name}") ?? false) ??
                        "";
                    var description = rec["Description"]?.ToString();
                    var manufacturer = rec["Manufacturer"]?.ToString();
                    return new Port(id, name, description, manufacturer);
                })
                .Where(p => p.Name.StartsWith("COM"));
        }
        catch (Exception ex)
        {
            ScreenLogger.Print("USB ERROR: " + ex.Message);
        }

        _cachedPorts.Clear();
        if (ports != null)
        {
            foreach (var port in ports)
            {
                _cachedPorts.Add(port);
            }
        }

        return ports?.ToArray() ?? Array.Empty<Port>();
    }

    private static Port? CreateCOMPort(PropertyDataCollection props, string? deviceName = null)
    {
        string? deviceID = null;
        string? descrition = null;
        string? manufacturer = null;

        foreach (PropertyData property in props)
        {
            if (property.Name == "DeviceID")        // next 3 properties handle Win32_SerialPort
            {
                deviceID = (string?)property.Value;
            }
            else if (property.Name == "Description")
            {
                descrition = (string?)property.Value;
            }
            else if (property.Name == "Manufacturer")
            {
                manufacturer = (string?)property.Value;
            }
            else if (property.Name == "Dependent")  // this handles Win32_USBControllerDevice, as Win32_SerialPort stopped working
            {
                var usbControllerID = (string)property.Value;
                usbControllerID = usbControllerID.Replace("\"", "");
                var devID = usbControllerID.Split('=')[1];
                using var searcher = new ManagementObjectSearcher($"SELECT * FROM Win32_PnPEntity WHERE DeviceID LIKE '%{devID}%'");
                ManagementBaseObject[] records = searcher.Get().Cast<ManagementBaseObject>().ToArray();
                foreach (var rec in records)
                {
                    var name = (string?)rec.Properties["Name"]?.Value;
                    if (name?.Contains("(COM") ?? false)
                    {
                        return CreateCOMPort(rec.Properties, name);
                    }
                }
            }
        }

        return deviceID == null ? null : new Port(deviceID, deviceName ?? "COMXX", descrition, manufacturer);
    }

    private static string? GetDeviceID(PropertyDataCollection props)
    {
        string? deviceID = null;

        foreach (PropertyData property in props)
        {
            if (property.Name == "DeviceID")
            {
                deviceID = (string?)property.Value;
            }
            else if (property.Name == "Dependent")  // this handles Win32_USBControllerDevice, as Win32_SerialPort stopped working
            {
                var usbControllerID = (string)property.Value;
                usbControllerID = usbControllerID.Replace("\"", "").Replace(@"\\", @"\");
                deviceID = usbControllerID.Split('=')[1];
            }
        }

        return deviceID;
    }

    // Debugging
    /*
    static HashSet<string> PropsToPrint = new() { "Caption", "Description", "Manufacturer", "Name", "Service" };
    static HashSet<string> ManufacturersToPrint = new() { "microsoft" };
    static HashSet<string> ManufacturersNotToPrint = new() { "microsoft", "standard", "(standard", "intel", "acer", "rivet", "nvidia", "realtek", "generic" };

    static void PrintProperties(PropertyDataCollection props)
    {
        var indent = "    ";
        var man = props["Manufacturer"];
        //if (ManufacturersNotToPrint.Any(m => man?.Value?.ToString()?.ToLower().StartsWith(m) ?? false))
        //    return;
        //if (!ManufacturersToPrint.Any(m => man?.Value?.ToString()?.ToLower().StartsWith(m) ?? false))
        //    return;

        foreach (PropertyData p in props)
        {
            //if (!PropsToPrint.Contains(p.Name))
            //    continue;
            if (p.IsArray)
            {
                ScreenLogger.Print($"{indent}{p.Name}: ({p.Type})");
                if (p.Value != null)
                {
                    if (p.Value is string[] strings)
                        ScreenLogger.Print($"{indent}{indent}{string.Join($"\n{indent}{indent}", strings)}");
                    else if (p.Value is ushort[] words)
                        ScreenLogger.Print($"{indent}{indent}{string.Join($"\n{indent}{indent}", words)}");
                    else if (p.Value is uint[] dwords)
                        ScreenLogger.Print($"{indent}{indent}{string.Join($"\n{indent}{indent}", dwords)}");
                    else if (p.Value is ulong[] qwords)
                        ScreenLogger.Print($"{indent}{indent}{string.Join($"\n{indent}{indent}", qwords)}");
                    else
                        ScreenLogger.Print($"{indent}{indent}{string.Join($"\n{indent}{indent}", (IEnumerable)p.Value)}");
                }
                else
                {
                    ScreenLogger.Print($"{indent}{indent}none");
                }
            }
            else
            {
                ScreenLogger.Print($"{indent}{p.Name} = {p.Value?.ToString()} ({p.Type})");
            }
        }
        ScreenLogger.Print();
    }*/

    /*
    static COMUtils()
    {
        ScreenLogger.Print("==== PnP devices ===");
        using var pnp = new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity WHERE Caption LIKE '%(COM%' OR Caption LIKE '%Smellodi%'");
        var records = pnp.Get().Cast<ManagementBaseObject>().ToArray();
        foreach (var rec in records)
            PrintProperties(rec.Properties);
        ScreenLogger.Print("====================");
    }*/
}
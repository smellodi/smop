# SMELLODI odor printer (SMOP): Pulse generator

## Dependencies

NuGet packages:
- ScottPlot.WPF

## Connecting to devices

### Odor Display

Connection to the odor display device is obligatory. The software may suggest a COM port to connect to, but it is users' responsibility to figure out what COM port is the correct one.

### IonVision

`IonVision.json` must contain correct information regaring the IobVision device:

- `ip`: the IonVision IP address,
- `user`: user to be logged in, or "null" (without quotation marks) skip this step,
- `project`: name of the project to be loaded,
- `parameterId`: ID of the parameter to be set and preloaded (like `daa1c397-ebd0-4920-b405-5c6029d45fdd`,
- `parameterName`: name of this parameter,

Users can load any other JSON file containing these fields using the `...` button on the Connection page.
Note that it is not necessary to connect to IonVision and Smell Inspector to proceed to the next step.

### Smell Inspector

Users must select the proper COM port.

## Testing without real devices connected

On the Connection page, press F2 to start the simulation mode.

## Setup file

The setup file (plain TXT) containing generator parameters can be selected in the Setup page.
The file contains description of pulses grouped into sessions.

First row must describe the first session and start with `INIT:` keyword followed by the session parameters.
The following space-separated sessions parameters are allowed:

- `HUMIDITY` : humidity in percentages in the range 0..100,
- `DELAY` : delay (seconds) before the gas is released,
- `DMS` : delay (seconds) of starting DMS measurement after the gas starts flowing. DMS measurement will not be triggered if this parameter is absent,
- `DURATION` : gas flow duration (seconds),
- `FINAL` : final pause (seconds) after the gas stops flowing,

Example:
```
INIT: HUMIDITY=40 DELAY=5 DMS=5 DURATION=90 FINAL=5
```

This example says that:
- the humidity will be set to 40% before the first pulse starts,
- then, for each pulse:
	- pulse channels' flows will be set at T=0,
	- (some of) channels' valves will be opened at T=5s,
	- DMS measurement will start at T=10s,
	- channels' valves will be closed at T=95s,
	- the pulse generation procedure will end at T=100s, continuing to the next pulse, if any.

At least one pulse must be specified after each session description, one per line. The pulse definition starts 
with the `PULSE:` keyword followed by a list of channel descriptions separated by space. Channel definition starts 
with the channel number (1..9), equality sign `=`, and the flow rate in nccm (max 1000 nccm). Channels not listed 
will have no flow and their valves will remain closed. If a closed channel should get some flow rate, then 
`,0` or `,OFF` should follow its flow rate.

Example:
```
PULSE: 1=70 2=60,OFF 4=50
```
Here, the valves of channels `1` and `4` will be opened when the pulse starts, and channels' flowing rates will be 
70 nccm and 50 nccm respectively. All other channels will be inactive, except the channel `2` that will remain 
closed, but its MFC will be programmed to blow with 60 nccm into the waste.

## Logged data

After the pulse generating script finishes its work, all log files will be saved to a folder with the current timestamp set as its name, like `2023-07-26 14-27-12Z`.
The parent folder is `Documents` by default, but users can change it when prompted to save data.

List of timestamped events (actions, like valve opened and closed) will be recorded into `events.txt` file.
Data from the odor display will be stored in `odor_display.txt` file.
If IonVision was connected, then `dms.json` file will contain an array of DMS measurements with the comment set to the pulse channels description.
If Smell Inspector was connected, then `snt.json` file will contain an array of SNT measurements, 66 values per measurement.

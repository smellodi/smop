# SMELLODI Odor Printer (SMOP)

Implements the following procedures:
- Pulse generator
- Odor reproduction using ML

## Dependencies

NuGet packages:
- ScottPlot.WPF
- NLog
- WpfAnimatedGif 

## Connecting to devices

### Odor Display

Connection to the odor display device is obligatory. The software may suggest a COM port to connect to, 
but it is user's responsibility to figure out what COM port is the correct one.

### Smell Inspector

Users must select the proper COM port. Note that it is not necessary to connect to Smell Inspector 
to proceed to the next step.

### IonVision

Parameters for connection to IonVision device must be stored in a JSON file. 
`IonVision.json` file that is packaged with the app must be edited to contain correct information prior to 
running the app for the the first time. The parameters are:

- `ip`: the IonVision IP address,
- `user`: user to be logged in, or "null" (without quotation marks) skip this step,
- `project`: name of the project to be loaded,
- `parameterId`: ID of the parameter to be set and preloaded (like `daa1c397-ebd0-4920-b405-5c6029d45fdd`,
- `parameterName`: name of this parameter,

Users can load any other JSON file containing these fields using the `...` button on the Connection page.
Note that it is not necessary to connect to IonVision to proceed to the next step.

## Testing without real devices connected

Press F2 on the Connection page to start the simulation mode for all modules. To specify the simulation mode 
for a single device, click on the device selection control and press F2. The Machine Learning module can be 
simulated if F2 is pressed on the Setup page.

## Pulse Generator

IonVision device is automatically initialized when proceeding to the *Setup* page.

**NOTE** IonVision initialization may end up with the parameter not being loaded. In this case, please load the 
parameter manually from the IonVision device interface.

A file with pulse generation settings must be either created or loaded from a file before continuing. 
The corresponding buttons can be found next to the current setup file name. The order of pulses will be randomized
if the "Randomize pulse order" checkox is checked.

### Setup file

The setup file (plain text format) containing generator parameters can be selected in the Setup page.
The file must contain description of pulses grouped into sessions.

First row must describe the first session and start with `INIT:` keyword followed by the session parameters 
present as a list of space-separated `key=value` pairs.
The following sessions parameters are allowed:

- `HUMIDITY` : humidity in percentages in the range 0..100,
- `DELAY` : delay (seconds) before the gas is released,
- `DMS` : delay (seconds) of starting DMS measurement after the gas starts flowing. DMS measurement will not be triggered if this parameter is absent,
- `DURATION` : gas flow duration (seconds),
- `FINAL` : final pause (seconds) after the gas stops flowing,

Example:
```
INIT: HUMIDITY=40 DELAY=5 DMS=5 DURATION=90 FINAL=5
```

In this example
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

### Logged data

After the app finishes its work, all log files will be saved to a folder with the current 
timestamp set as its name, like `2023-07-26 14-27-12Z`.
The parent folder is `Documents` by default, but users can change it when prompted to save data.

List of timestamped events (actions, like valve opened and closed) will be recorded into `events.txt` file.
Data from the odor display will be stored in `odor_display.txt` file.

If IonVision was connected, then `dms.json` file will contain an array of DMS measurements with the comment 
set to the pulse channels description. If Smell Inspector was connected, then `snt.json` file will contain 
an array of SNT measurements, 66 values per measurement.

## Odor Reproductor

Either IonVision or Smell Inspestor will be used as eNose to stream measurement data to the Machine Learning module.
Note that if both devices were connected on the *Connection* page, then Smell Inspector will be ignored.

Odor names and their initial flow rates should be specified on the *Setup* page before measuring the target odor.
Once this is done, the taget odor measurement can be started upon clicking the "Measure the target odor" button 
that becomes visible after IonVision is initialized with the project and its parameter.

**NOTE** IonVision initialization may end up with the parameter not being loaded. In this case, please load the 
parameter manually from the IonVision device interface.

Click the "Start" button to feed the measured odor tothe Machine Learning module and start the odor reproduction loop.
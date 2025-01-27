# Cisco RoomOS Video Codec Plugin

## License

Provided under MIT license
## Overview

> The Cisco RoomOS plugin provides device control over the Cisco Webex family of video conference codecs with regards
> to the most commonly used and requested attriute and control types.
>
> It implements all available essentials interfaces relevant to a device of this type, including but not limited to 
>  `VideoCodecBase`. Interfaces supported can be found by viewing the declaration of the `CiscoCodec` class in `CiscoRoomOsCodec.cs`.
> 
> Additionally, every component implements `IKeyed` and all devices are added to the `DeviceManager` unpon instantiation.
> 
## Cloning Instructions

After forking this repository into your own GitHub space, you can create a new repository using this one as the template.  Then you must install the necessary dependencies as indicated below.

## Dependencies

The [Essentials](https://github.com/PepperDash/Essentials) libraries are required. They referenced via nuget. You must have nuget.exe installed and in the `PATH` environment variable to use the following command. Nuget.exe is available at [nuget.org](https://dist.nuget.org/win-x86-commandline/latest/nuget.exe).

### Installing Dependencies

To install dependencies once nuget.exe is installed, run the following command from the root directory of your repository:
`nuget install .\packages.config -OutputDirectory .\packages -excludeVersion`.
To verify that the packages installed correctly, open the plugin solution in your repo and make sure that all references are found, then try and build it.

### Installing Different versions of PepperDash Core

If you need a different version of PepperDash Core, use the command `nuget install .\packages.config -OutputDirectory .\packages -excludeVersion -Version {versionToGet}`. Omitting the `-Version` option will pull the version indicated in the packages.config file.

### Instructions for Renaming Solution and Files

See the Task List in Visual Studio for a guide on how to start using the templage.  There is extensive inline documentation and examples as well.

For renaming instructions in particular, see the XML `remarks` tags on class definitions


# CiscoCodecJoinMap

## Digitals
| Join Number | Join Span | Description                                                                                                          | Type                | Capabilities |
| ----------- | --------- | -------------------------------------------------------------------------------------------------------------------- | ------------------- | ------------ |
| 1           | 1         | Device is Online                                                                                                     | Digital             | ToSIMPL      |
| 10          | 1         | If High, will send DTMF tones to the call set by SelectCall analog.  If low sends DTMF tones to last connected call. | Digital             | FromSIMPL    |
| 11          | 1         | DTMF 1                                                                                                               | Digital             | FromSIMPL    |
| 12          | 1         | DTMF 2                                                                                                               | Digital             | FromSIMPL    |
| 13          | 1         | DTMF 3                                                                                                               | Digital             | FromSIMPL    |
| 14          | 1         | DTMF 4                                                                                                               | Digital             | FromSIMPL    |
| 15          | 1         | DTMF 5                                                                                                               | Digital             | FromSIMPL    |
| 16          | 1         | DTMF 6                                                                                                               | Digital             | FromSIMPL    |
| 17          | 1         | DTMF 7                                                                                                               | Digital             | FromSIMPL    |
| 18          | 1         | DTMF 8                                                                                                               | Digital             | FromSIMPL    |
| 19          | 1         | DTMF 9                                                                                                               | Digital             | FromSIMPL    |
| 20          | 1         | DTMF 0                                                                                                               | Digital             | FromSIMPL    |
| 21          | 1         | DTMF *                                                                                                               | Digital             | FromSIMPL    |
| 22          | 1         | DTMF #                                                                                                               | Digital             | FromSIMPL    |
| 24          | 1         | End All Calls                                                                                                        | Digital             | FromSIMPL    |
| 31          | 1         | Current Hook State                                                                                                   | Digital             | ToSIMPL      |
| 41          | 4         | Speed Dial                                                                                                           | Digital             | FromSIMPL    |
| 50          | 1         | Incoming Call                                                                                                        | Digital             | ToSIMPL      |
| 51          | 1         | Answer Incoming Call                                                                                                 | Digital             | FromSIMPL    |
| 52          | 1         | Reject Incoming Call                                                                                                 | Digital             | FromSIMPL    |
| 71          | 1         | Dial manual string specified by CurrentDialString serial join                                                        | Digital             | FromSIMPL    |
| 72          | 1         | Dial Phone                                                                                                           | Digital             | FromSIMPL    |
| 72          | 1         | Dial Phone                                                                                                           | Digital             | ToSIMPL      |
| 73          | 1         | Hang Up Phone                                                                                                        | Digital             | FromSIMPL    |
| 81          | 8         | End a specific call by call index.                                                                                   | Digital             | FromSIMPL    |
| 90          | 1         | Join all calls                                                                                                       | Digital             | FromSIMPL    |
| 91          | 8         | Join a specific call by call index.                                                                                  | Digital             | FromSIMPL    |
| 100         | 1         | Directory Search Busy FB                                                                                             | Digital             | ToSIMPL      |
| 101         | 1         | Directory Selected Entry Is Contact FB                                                                               | Digital             | ToSIMPL      |
| 101         | 1         | Directory Line Selected FB                                                                                           | Digital             | FromSIMPL    |
| 102         | 1         | Directory is on Root FB                                                                                              | Digital             | ToSIMPL      |
| 103         | 1         | Directory has changed FB                                                                                             | Digital             | ToSIMPL      |
| 104         | 1         | Go to Directory Root                                                                                                 | Digital             | FromSIMPL    |
| 105         | 1         | Go back one directory level                                                                                          | Digital             | FromSIMPL    |
| 106         | 1         | Dial selected directory line                                                                                         | Digital             | FromSIMPL    |
| 107         | 1         | Set high to disable automatic dialing of a contact when selected                                                     | Digital             | FromSIMPL    |
| 108         | 1         | Pulse to dial the selected contact method                                                                            | Digital             | FromSIMPL    |
| 110         | 1         | Clear Selected Entry and String from Search                                                                          | Digital             | FromSIMPL    |
| 111         | 1         | Camera Tilt Up                                                                                                       | Digital             | FromSIMPL    |
| 112         | 1         | Camera Tilt Down                                                                                                     | Digital             | FromSIMPL    |
| 113         | 1         | Camera Pan Left                                                                                                      | Digital             | FromSIMPL    |
| 114         | 1         | Camera Pan Right                                                                                                     | Digital             | FromSIMPL    |
| 115         | 1         | Camera Zoom In                                                                                                       | Digital             | FromSIMPL    |
| 116         | 1         | Camera Zoom Out                                                                                                      | Digital             | FromSIMPL    |
| 117         | 1         | Camera Focus Near                                                                                                    | Digital             | FromSIMPL    |
| 118         | 1         | Camera Focus Far                                                                                                     | Digital             | FromSIMPL    |
| 119         | 1         | Camera Auto Focus Trigger                                                                                            | Digital             | FromSIMPL    |
| 121         | 1         | Pulse to save selected preset spcified by CameraPresetSelect analog join.  FB will pulse for 3s when preset saved.   | Digital             | ToFromSIMPL  |
| 131         | 1         | Camera Mode Auto.  Enables camera auto tracking mode, with feedback                                                  | Digital             | ToFromSIMPL  |
| 132         | 1         | Camera Mode Manual.  Disables camera auto tracking mode, with feedback                                               | Digital             | ToFromSIMPL  |
| 133         | 1         | Camera Mode Off.  Disables camera video, with feedback. Works like video mute.                                       | Digital             | ToFromSIMPL  |
| 134         | 1         | Presenter Track Off Get/Set                                                                                          | Digital             | ToFromSIMPL  |
| 135         | 1         | Presenter Track Follow Get/Set                                                                                       | Digital             | ToFromSIMPL  |
| 136         | 1         | Presenter Track Background Get/Set                                                                                   | Digital             | ToFromSIMPL  |
| 137         | 1         | Presenter Track Persistent Get/Set                                                                                   | Digital             | ToFromSIMPL  |
| 141         | 1         | Camera Self View Toggle/FB                                                                                           | Digital             | ToFromSIMPL  |
| 142         | 1         | Camera Layout Toggle                                                                                                 | Digital             | FromSIMPL    |
| 143         | 1         | Camera Supports Auto Mode FB                                                                                         | Digital             | ToSIMPL      |
| 144         | 1         | Camera Supports Off Mode FB                                                                                          | Digital             | ToSIMPL      |
| 145         | 1         | Speaker Track Available                                                                                              | Digital             | ToSIMPL      |
| 146         | 1         | Presenter Track Availble                                                                                             | Digital             | ToSIMPL      |
| 159         | 1         | Presenter Track Availble                                                                                             | Digital             | FromSIMPL    |
| 160         | 1         | Update Meetings                                                                                                      | Digital             | FromSIMPL    |
| 161         | 10        | Join meeting                                                                                                         | Digital             | FromSIMPL    |
| 171         | 1         | Mic Mute On                                                                                                          | Digital             | ToFromSIMPL  |
| 172         | 1         | Mic Mute Off                                                                                                         | Digital             | ToFromSIMPL  |
| 173         | 1         | Mic Mute Toggle                                                                                                      | Digital             | ToFromSIMPL  |
| 174         | 1         | Volume Up                                                                                                            | Digital             | FromSIMPL    |
| 175         | 1         | Volume Down                                                                                                          | Digital             | FromSIMPL    |
| 176         | 1         | Volume Mute On                                                                                                       | Digital             | ToFromSIMPL  |
| 177         | 1         | Volume Mute Off                                                                                                      | Digital             | ToFromSIMPL  |
| 178         | 1         | Volume Mute Toggle                                                                                                   | Digital             | ToFromSIMPL  |
| 181         | 1         | Pulse to remove the selected recent call item specified by the SelectRecentCallItem analog join                      | Digital             | FromSIMPL    |
| 182         | 1         | Pulse to dial the selected recent call item specified by the SelectRecentCallItem analog join                        | Digital             | FromSIMPL    |
| 200         | 1         | Presentation Active                                                                                                  | Digital             | ToSIMPL      |
| 201         | 1         | Start Sharing & Feedback                                                                                             | Digital             | ToFromSIMPL  |
| 202         | 1         | Stop Sharing & Feedback                                                                                              | Digital             | ToFromSIMPL  |
| 203         | 1         | When high, will autostart sharing when a call is joined                                                              | Digital             | FromSIMPL    |
| 204         | 1         | Recieving content from the far end                                                                                   | Digital             | ToSIMPL      |
| 205         | 1         | Presentation Local Only Feedback                                                                                     | Digital             | ToFromSIMPL  |
| 206         | 1         | Presentation Local and Remote Feedback                                                                               | Digital             | ToFromSIMPL  |
| 207         | 1         | Presentation Local and Remote Feedback                                                                               | Digital             | FromSIMPL    |
| 211         | 1         | Toggles selfview position                                                                                            | Digital             | FromSIMPL    |
| 220         | 1         | Holds all calls                                                                                                      | Digital             | FromSIMPL    |
| 221         | 8         | Holds Call at specified index. FB reported on Call Status XSIG                                                       | Digital             | FromSIMPL    |
| 230         | 1         | Resume all held calls                                                                                                | Digital             | FromSIMPL    |
| 231         | 8         | Resume Call at specified index                                                                                       | Digital             | FromSIMPL    |
| 241         | 1         | Activates Do Not Disturb Mode.  FB High if active.                                                                   | Digital             | ToFromSIMPL  |
| 242         | 1         | Deactivates Do Not Disturb Mode.  FB High if deactivated.                                                            | Digital             | ToFromSIMPL  |
| 243         | 1         | Toggles Do Not Disturb Mode.                                                                                         | Digital             | ToSIMPL      |
| 246         | 1         | Activates Standby Mode.  FB High if active.                                                                          | Digital             | ToFromSIMPL  |
| 247         | 1         | Deactivates Standby Mode.  FB High if deactivated.                                                                   | Digital             | ToFromSIMPL  |
| 248         | 1         | Activates Half Wake Mode.  FB High if active.                                                                        | Digital             | ToFromSIMPL  |
| 249         | 1         | High to indicate that the codec is entering standby mode                                                             | Digital             | ToSIMPL      |
| 251         | 1         | High to indicate that the codec does not have any meetings currently active                                          | Digital             | ToSIMPL      |
| 252         | 1         | High to indicate that the codec has currently active meetings                                                        | Digital             | ToSIMPL      |
| 253         | 1         | High to indicate that the codec has an impending meeting                                                             | Digital             | ToSIMPL      |
| 261         | 1         | Set / Get PresentationView Default mode                                                                              | Digital             | ToFromSIMPL  |
| 262         | 1         | Set / Get PresentationView Maximized mode                                                                            | Digital             | ToFromSIMPL  |
| 263         | 1         | Set / Get PresentationView Minimized mode                                                                            | Digital             | ToFromSIMPL  |
| 301         | 1         | Multi site option is enabled FB                                                                                      | Digital             | ToSIMPL      |
| 302         | 1         | Auto Answer is enabled FB                                                                                            | Digital             | ToSIMPL      |
| 311         | 1         | Webex Pin Requested                                                                                                  | Digital             | ToSIMPL      |
| 311         | 1         | WebexSendPin                                                                                                         | DigitalSerial       | FromSIMPL    |
| 312         | 1         | WebexJoinAsGuest                                                                                                     | Digital             | FromSIMPL    |
| 312         | 1         | WebexJoinedAsHost                                                                                                    | Digital             | ToSIMPL      |
| 313         | 1         | WebexJoinedAsGuest                                                                                                   | Digital             | ToSIMPL      |
| 313         | 1         | WebexPinClear                                                                                                        | Digital             | FromSIMPL    |
| 314         | 1         | WebexPinError                                                                                                        | Digital             | ToSIMPL      |
| 501         | 50        | Toggles the participant's audio mute status                                                                          | Digital             | ToSIMPL      |
| 801         | 50        | Toggles the participant's video mute status                                                                          | Digital             | ToSIMPL      |
| 1101        | 50        | Toggles the participant's pin status                                                                                 | Digital             | ToSIMPL      |

## Analogs

| Join Number | Join Span | Description                                                                                      | Type                | Capabilities |
| ----------- | --------- | ------------------------------------------------------------------------------------------------ | ------------------- | ------------ |
| 21          | 1         | Ringtone volume set/FB.  Valid values are 0 - 100 in increments of 5 (5, 10, 15, 20, etc.)       | Analog              | ToFromSIMPL  |
| 24          | 1         | Sets the selected Call for DTMF commands. Valid values 1-8                                       | Analog              | FromSIMPL    |
| 25          | 1         | Reports the number of currently connected calls                                                  | Analog              | ToSIMPL      |
| 40          | 1         | Set/FB the number of meetings to display via the bridge xsig; default: 3 meetings.               | Analog              | ToFromSIMPL  |
| 41          | 1         | Minutes before meeting start that a meeting is joinable                                          | Analog              | FromSIMPL    |
| 42          | 1         | Total minutes until next meeting                                                                 | Analog              | ToSIMPL      |
| 43          | 1         | Hours until next meeting                                                                         | Analog              | ToSIMPL      |
| 44          | 1         | Minutes until next meeting                                                                       | Analog              | ToSIMPL      |
| 60          | 1         | Camera Number Select/FB.  1 based index.  Valid range is 1 to the value reported by CameraCount. | Analog              | ToFromSIMPL  |
| 61          | 1         | Reports the number of cameras                                                                    | Analog              | ToSIMPL      |
| 101         | 1         | Directory Select Row and Feedback                                                                | Analog              | FromSIMPL    |
| 101         | 1         | Directory Row Count FB                                                                           | Analog              | ToSIMPL      |
| 102         | 1         | Reports the number of contact methods for the selected contact                                   | Analog              | FromSIMPL    |
| 103         | 1         | Selects a contact method by index                                                                | Analog              | FromSIMPL    |
| 104         | 1         | Directory Select Row and Feedback                                                                | Analog              | ToSIMPL      |
| 121         | 1         | Camera Preset Select                                                                             | Analog              | ToSIMPL      |
| 122         | 1         | Far End Preset Preset Select                                                                     | Analog              | ToSIMPL      |
| 151         | 1         | Current Participant Count                                                                        | Analog              | ToSIMPL      |
| 161         | 1         | Meeting Count                                                                                    | Analog              | ToSIMPL      |
| 174         | 1         | Volume Level                                                                                     | Analog              | ToFromSIMPL  |
| 180         | 1         | Select/FB for Recent Call Item.  Valid values 1 - 10                                             | Analog              | ToFromSIMPL  |
| 181         | 10        | Recent Call Occurrence Type. [0-3] 0 = Unknown, 1 = Placed, 2 = Received, 3 = NoAnswer           | Analog              | ToSIMPL      |
| 191         | 1         | Recent Call Count                                                                                | Analog              | ToSIMPL      |
| 201         | 1         | Presentation set/FB.  Valid values are 0 - 6 depending on the codec model.                       | Analog              | ToFromSIMPL  |

## Serials

| Join Number | Join Span | Description                                                                                          | Type                | Capabilities |
| ----------- | --------- | ---------------------------------------------------------------------------------------------------- | ------------------- | ------------ |
| 1           | 1         | Value to dial when ManualDial digital join is pulsed                                                 | Serial              | ToSIMPL      |
| 2           | 1         | Phone Dial String                                                                                    | Serial              | FromSIMPL    |
| 2           | 1         | Current Call Data - XSIG                                                                             | Serial              | ToSIMPL      |
| 5           | 1         | Sends a serial command to the device.  Do not include the delimiter, it will be added automatically. | Serial              | FromSIMPL    |
| 22          | 1         | Current Call Direction                                                                               | Serial              | ToSIMPL      |
| 51          | 1         | Incoming Call Name                                                                                   | Serial              | ToSIMPL      |
| 52          | 1         | Incoming Call Number                                                                                 | Serial              | ToSIMPL      |
| 100         | 1         | Directory Search String                                                                              | Serial              | FromSIMPL    |
| 101         | 1         | Directory Entries - XSig, 255 entries                                                                | Serial              | ToSIMPL      |
| 102         | 1         | Schedule Data - XSIG                                                                                 | Serial              | ToSIMPL      |
| 103         | 1         | Contact Methods - XSig, 10 entries                                                                   | Serial              | ToSIMPL      |
| 104         | 1         | XSig Containing Data for Active Meeting                                                              | Serial              | ToSIMPL      |
| 105         | 1         | Formatted String Showing Time until room no longer available                                         | Serial              | ToSIMPL      |
| 106         | 1         | Formatted String Showing Time to next meeting                                                        | Serial              | ToSIMPL      |
| 121         | 1         | Camera Preset Names - XSIG, max of 15                                                                | Serial              | ToSIMPL      |
| 141         | 1         | Current Layout Fb                                                                                    | Serial              | ToSIMPL      |
| 142         | 1         | Select Layout by string                                                                              | Serial              | FromSIMPL    |
| 142         | 1         | xSig of all available layouts                                                                        | Serial              | ToSIMPL      |
| 151         | 1         | Current Participants XSig                                                                            | Serial              | ToSIMPL      |
| 161         | 10        | Camera Name Fb                                                                                       | Serial              | ToSIMPL      |
| 171         | 1         | Selected Recent Call Name                                                                            | Serial              | ToSIMPL      |
| 172         | 1         | Selected Recent Call Number                                                                          | Serial              | ToSIMPL      |
| 181         | 10        | Recent Call Names                                                                                    | Serial              | ToSIMPL      |
| 191         | 10        | Recent Calls Times                                                                                   | Serial              | ToSIMPL      |
| 201         | 1         | Current Source                                                                                       | Serial              | ToSIMPL      |
| 211         | 1         | advance selfview position                                                                            | Serial              | ToSIMPL      |
| 301         | 1         | IP Address of device                                                                                 | Serial              | ToSIMPL      |
| 302         | 1         | SIP phone number of device                                                                           | Serial              | ToSIMPL      |
| 303         | 1         | E164 alias of device                                                                                 | Serial              | ToSIMPL      |
| 304         | 1         | H323 ID of device                                                                                    | Serial              | ToSIMPL      |
| 305         | 1         | SIP URI of device                                                                                    | Serial              | ToSIMPL      |
| 311         | 1         | WebexSendPin                                                                                         | DigitalSerial       | FromSIMPL    |
| 321         | 1         | WidgetEventData                                                                                         | Serial       | ToSIMPL    |
| 356         | 1         | Selected Directory Entry Name                                                                        | Serial              | ToSIMPL      |
| 357         | 1         | Selected Directory Entry Number                                                                      | Serial              | ToSIMPL      |
| 358         | 1         | Selected Directory Folder Name                                                                       | Serial              | ToSIMPL      |

#### Config Notes

> This configuration matches a standard essentials device configuration at the base level, with only the type being different.  This may have the type **```ciscoRoomOS```**, as defined in ```CiscoCodecFactory```.
>
> 
``` javascript
"key": "Codec-1",
    "name": "Video Codec 1",
    "type": "ciscoRoomOS",
    "group": "videoCodec",
    "properties": {
        "control": {
            "endOfLineString": "\n",
            "deviceReadyResponsePattern": "",
            "method": "Ssh",
            "tcpSshProperties": {
                "address": "10.0.0.1",
                "port": 22,
                "autoReconnect": true,
                "AutoReconnectIntervalMs": 10000,
                "username": "admin",
                "password": "tandberg"
            }
        }
    }
}
```

> An example bridge configuration is as follows
>
> 
``` javascript
"uid": 20,
    "key": "eisc-vc",
    "type": "eiscApiAdvanced",
    "group":"api",
    "name": "EISC VC Bridge",
    "properties": {
        "control":{
            "tcpSshProperties":{
                "address":"127.0.0.2",
                "port":0
            },
            "ipId":"4F"
        },
        "devices": [
            {
                "deviceKey":"Codec-1",
                "joinStart":1                    
            }
        ] 
    }
}
```

***

#### Helful info
in a terminal you can send a string directly to the device, here is an example command using "Codec-1-ssh" as the device port name
```
devjson:1 {"deviceKey":"Codec-1-ssh","methodName":"SendText", "params": ["xStatus SystemUnit\n"]}
````

see what methods you can call on the port by sending the following command
```
devmethods Codec-1-ssh
```

To see commands send and received you can turn on debugging for the port (1: Rx, 2:Txm 3:Both)
```
setdevicestreamdebug Codec-1-ssh 1
```
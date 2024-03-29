![PepperDash Essentials Pluign Logo](./images/essentials-plugin-blue.png)

# Essentials Extron XTP Plugin &copy; 2024

## Configuration

```json
{
	"key": "switcher1",
	"name": "Extron XTP Switcher",
	"type": "extronxtp",
	"group": "switcher",
	"parentDeviceKey": "processor",
	"properties": {
		"control": {
			"method": "tcpIp",
			"tcpSshProperties": {
				"address": "",
				"port": 23,
				"username": "",
				"password": "",
				"autoReconnect": true,
				"autoReconnectIntervalMs": 10000
			}
		},
		"virtualMode": false,
		"inputs": [
			{
				"name": "None",
				"ioNumber": 0
			},
			{
				"name": "Input 1",
				"videoName": "Input 1 Video",
				"audioName": "Input 1 Audio",
				"ioNumber": 1
			},
			{
				"name": "Input 2",
				"ioNumber": 2
			},
			{
				"name": "Input 3",
				"ioNumber": 3
			},
			{
				"name": "Input 4",
				"ioNumber": 4
			},
			{
				"name": "Input 16",
				"ioNumber": 16
			},
			{
				"name": "Input 32",
				"videoName": "Input 32 Video",
				"audioName": "Input 32 Audio",
				"ioNumber": 32
			}
		],
		"outputs": [
			{
				"name": "Output 1",
				"videoName": "Output 1 Video",
				"audioName": "Output 1 Audio",
				"ioNumber": 1
			},
			{
				"name": "Output 2",
				"ioNumber": 2
			},
			{
				"name": "Output 3",
				"ioNumber": 3
			},
			{
				"name": "Output 4",
				"ioNumber": 4
			},
			{
				"name": "Output 16",
				"ioNumber": 16
			},
			{
				"name": "Output 32",
				"videoName": "Output 32 Video",
				"audioName": "Output 32 Audio",
				"ioNumber": 32
			}
		]
	}
},
{
	"key": "switcher1-bridge",
	"name": "Essentials Switcher1 Bridge",
	"group": "api",
	"type": "eiscApiAdvanced",
	"properties": {
		"control": {
			"ipid": "E1",
			"method": "ipidTcp",
			"tcpSshProperties": {
				"address": "127.0.0.2",
				"port": 0
			}
		},
		"devices": [
			{
				"deviceKey": "switcher1",
				"joinStart": 1
			}
		]
	}
}
```

## ExtronXtpBridgeJoinMap

### Digitals

| Join Number | Join Span | Description                                          | Type          | Capabilities |
| ----------- | --------- | ---------------------------------------------------- | ------------- | ------------ |
| 1           | 1         | Extron XTP Chassis Online (LEGACY)                   | Digital       | ToSIMPL      |
| 2           | 1         | Reports to bridge users the chassis is ready routies | Digital       | ToSIMPL      |
| 4           | 1         | DM Chassis enable audio breakaway routing            | Digital       | FromSIMPL    |
| 5           | 1         | DM Chassis enable USB breakaway routing              | Digital       | FromSIMPL    |
| 10          | 1         | DM Chassis SystemId Get/Set/Trigger/                 | DigitalAnalog | ToFromSIMPL  |
| 11          | 1         | DM Chassis Online                                    | Digital       | ToSIMPL      |
| 101         | 32        | DM Input Video Sync                                  | Digital       | ToSIMPL      |
| 501         | 32        | DM Chassis Input Endpoint Online                     | Digital       | ToSIMPL      |
| 701         | 32        | DM Chassis Output Endpoint Online                    | Digital       | ToSIMPL      |
| 1001        | 32        | DM Chassis Tx Advanced Is Present                    | Digital       | ToSIMPL      |
| 1201        | 32        | DM Chassis Output Disabled by HDCP                   | Digital       | ToSIMPL      |
| 1401        | 32        | Output Volume Up                                     | Digital       | ToSIMPL      |
| 1601        | 32        | Output Volume Down                                   | Digital       | ToSIMPL      |
| 1801        | 32        | Output Mute Toggle                                   | Digital       | ToFromSIMPL  |

### Analogs

| Join Number | Join Span | Description                                                           | Type          | Capabilities |
| ----------- | --------- | --------------------------------------------------------------------- | ------------- | ------------ |
| 1           | 1         | SocketStatus                                                          | Analog        | ToSIMPL      |
| 10          | 1         | DM Chassis SystemId Get/Set/Trigger/                                  | DigitalAnalog | ToFromSIMPL  |
| 101         | 32        | DM Chassis Output Video Set / Get                                     | Analog        | ToFromSIMPL  |
| 301         | 32        | DM Chassis Output Audio Set / Get                                     | Analog        | ToFromSIMPL  |
| 501         | 32        | DM Chassis Output USB Set / Get                                       | Analog        | ToFromSIMPL  |
| 701         | 32        | DM Chassis Input Usb Set / Get                                        | Analog        | ToFromSIMPL  |
| 1001        | 32        | DM Chassis Input HDCP Support State                                   | Analog        | ToSIMPL      |
| 1201        | 32        | DM Chassis Input HDCP Support Capability                              | Analog        | FromSIMPL    |
| 1401        | 32        | Output Volume Set/Get                                                 | Analog        | ToFromSIMPL  |
| 1501        | 32        | DM Chassis Stream Input Start (1), Stop (2), Pause (3) with Feedback  | Analog        | FromSIMPL    |
| 1601        | 32        | DM Chassis Stream Output Start (1), Stop (2), Pause (3) with Feedback | Analog        | FromSIMPL    |

### Serials

| Join Number | Join Span | Description                                               | Type   | Capabilities |
| ----------- | --------- | --------------------------------------------------------- | ------ | ------------ |
| 1           | 1         | DM Chassis Name                                           | Serial | ToSIMPL      |
| 100         | 1         | DM Chassis Input Name                                     | Serial | ToSIMPL      |
| 101         | 32        | DM Chassis Input Name                                     | Serial | ToSIMPL      |
| 301         | 32        | DM Chassis Output Name                                    | Serial | ToSIMPL      |
| 501         | 200       | DM Chassis Video Input Names                              | Serial | ToFromSIMPL  |
| 701         | 200       | DM Chassis Audio Input Names                              | Serial | ToFromSIMPL  |
| 901         | 200       | DM Chassis Video Output Names                             | Serial | ToFromSIMPL  |
| 1101        | 200       | DM Chassis Audio Output Names                             | Serial | ToFromSIMPL  |
| 2001        | 32        | DM Chassis Video Output Currently Routed Video Input Name | Serial | ToSIMPL      |
| 2201        | 32        | DM Chassis Audio Output Currently Routed Video Input Name | Serial | ToSIMPL      |
| 2401        | 32        | DM Chassis Input Current Resolution                       | Serial | ToSIMPL      |



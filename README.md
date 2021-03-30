

# TF47 Teamspeak Anti-Vpn Tool



## Introduction:

Have you ever had the problem that even though you banned someone from your Teamspeak, they still reconnect under a new TeamspeakId and using a VPN to change the IP address?

This little tool listens to the connecting clients and checks the IP address against a VPN list. 
This should cover up to 99% of all VPN services.
Unfortunately it is quite aggressive. For example, in Germany the 1&1 carrier ends up in the block list. Therefore, you have the option to unblock certain services or even individual users, for example to allow access from a corporate network.



## Settings: 

#### Whitelist.txt:

You can edit the allowed User list by adding them to the whitelist.txt 

Make sure you make a new line for each new Id. You can use the unique Id and the myTeamspeak Id.
You can also update the list while the program is running. It will update its internal list upon change.

#### Appsettings.json

 This file will be used for the general configuration. Just change the settings to your own values.

| Setting              | Description                                                  |
| -------------------- | ------------------------------------------------------------ |
| ServerAddress        | Hostname or IP Address of your Teamspeak server              |
| User                 | Serverquery username                                         |
| Password             | Serverquery password                                         |
| ServerId             | VirtualServerId                                              |
| ExcludedProviderList | List of provider names you want to whitelist.<br />You will get the providername by looking into the output of the program |



[^Made by Dragon with ❤️]: 


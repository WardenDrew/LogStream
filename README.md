# ARCHIVED

I made this for a specific internal purpose. I would not suggest using it any further

# LogStream
Lightweight Log File web gui with live websockets

**Warning: this does not implement any form of authentication or ssl. I would recommend running this behind a reverse proxy such as NGINX and handling authentication at the proxy layer.**

![Main Window](https://github.com/andrew-haskell/LogStream/blob/master/logstream-image-1.PNG?raw=true)

Inside appsettings.json you can find two configuration parameters: AllowedLogFiles and AllowedLogDirectories. These correspond to specific loose files, and whole directories that will be exposed in the GUI.
NOTE that the dotnet application will need to be able to read those files in the first place, or else it will just bubble up an error to the frontend.

Logs can be downloaded, or viewed in real-time over a websocket's connection.

![Live Window](https://github.com/andrew-haskell/LogStream/blob/master/logstream-image-2.PNG?raw=true)

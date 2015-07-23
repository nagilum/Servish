# Servish

A simple webserver for serving a single folder.

## Usage

Just run the servish.exe file to init the webserver. It will, by default, serve the folder you're in when executing the file.

### Command Line Arguments

You can configure Servish by using switches, as such:

**servish.exe --path blargh --verbose**

All available switches are:

* **defaultDocument** Set the default document to serve when requesting /. Defaults to index.html.
* **defaultMimeType** Set the default mime type to serve with. Defaults to text/html.
* **path** The folder to serve. Defaults to current folder.
* **port** The port to bind the server too. Defaults to 80.
* **serverName** Name to give when responding to clients. Defaults to Environment.MachineName.
* **verbose** Whether or not to output requests and responses to the console. Defaults to false.

### settings.json

You can set all the switches by creating a settings.json file.
The file would look like this:

```json
	{
		"DefaultDocument": "index.html",
		"DefaultMimeType": "text/html",
		"Path": "C:\\My\\Local\\Folder",
		"Port": 80,
		"ServerName": "MyAwesomeServer",
		"Verbose": True
	}
```
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace Servish {
	public class Program {
		/// <summary>
		/// The default instance of settings.
		/// </summary>
		private static Settings settings;

		/// <summary>
		/// A list of mime types to answer with when called.
		/// </summary>
		private static Dictionary<string, string> mimeTypes = new Dictionary<string, string>();

		/// <summary>
		/// The TCP listening instance.
		/// </summary>
		private static TcpListener listener;

		/// <summary>
		/// The main entry-point function for the webserver.
		/// </summary>
		public static void Main(string[] args) {
			// Read settings from file.
			if (File.Exists(Path.Combine(Application.StartupPath, "settings.json"))) {
				try {
					settings = new JavaScriptSerializer()
						.Deserialize<Settings>(File.ReadAllText(Path.Combine(Application.StartupPath, "settings.json")));
				}
				catch (Exception ex) {
					Console.WriteLine("Found but unable to read or parse the settings.json file! Aborting.");
					Console.WriteLine(ex.Message);
					return;
				}
			}

			if (settings == null) {
				var dirInfo = new DirectoryInfo(".");

				settings = new Settings {
					DefaultDocument = "index.html",
					DefaultMimeType = "text/html",
					Path = dirInfo.FullName,
					Port = 80,
					ServerName = Environment.MachineName,
					Verbose = false
				};
			}

			// Read mime types from file
			if (File.Exists(Path.Combine(Application.StartupPath, "mimeTypes.json"))) {
				try {
					mimeTypes = new JavaScriptSerializer()
						.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(Application.StartupPath, "mimeTypes.json")));
				}
				catch (Exception ex) {
					Console.WriteLine("Found but unable to read or parse the mimeTypes.json file! Aborting.");
					Console.WriteLine(ex.Message);
					return;
				}
			}

			// Parsed the passed arguments from command line.
			parseArguments(args);

			// Output current settings if verbose is turned on.
			if (settings.Verbose) {
				Console.WriteLine("Servish");
				Console.WriteLine();
				Console.WriteLine("DefaultDocument: " + settings.DefaultDocument);
				Console.WriteLine("DefaultMimeType: " + settings.DefaultMimeType);
				Console.WriteLine("Path: " + settings.Path);
				Console.WriteLine("Port: " + settings.Port);
				Console.WriteLine("ServerName: " + settings.ServerName);
				Console.WriteLine();
			}

			// Attempt to initialize the listener.
			try {
				listener = new TcpListener(IPAddress.Any, settings.Port);
				listener.Start();

				if (settings.Verbose) {
					Console.WriteLine("Web Server Running... Press ^C to Stop...");
					Console.WriteLine();
				}

				var thread = new Thread(listenLoop);
				thread.Start();
			}
			catch (Exception ex) {
				Console.WriteLine("An error occured while binding the listener!");
				Console.WriteLine(ex.Message);
				Console.WriteLine("Aborting!");
			}
		}

		/// <summary>
		/// Translates the uri to local filename and returns.
		/// </summary>
		private static string getLocalPath(string uri) {
			if (uri == "/")
				uri = settings.DefaultDocument;

			if (uri.StartsWith("/"))
				uri = uri.Substring(1);

			var path = Path.Combine(settings.Path, uri.Replace("/", @"\"));

			return File.Exists(path) ? path : null;
		}

		/// <summary>
		/// Cycles the loaded mime types and gives back the best match.
		/// </summary>
		private static string getMimeTypeFromFilename(string filename) {
			var ext = (filename.IndexOf('.') > -1 ? filename.Substring(filename.LastIndexOf('.') + 1) : "");

			if (string.IsNullOrWhiteSpace(ext))
				return settings.DefaultMimeType;

			var mimeType = settings.DefaultMimeType;

			if (mimeTypes.ContainsKey(ext.ToLower()))
				mimeType = mimeTypes[ext.ToLower()];

			return mimeType;
		}

		/// <summary>
		/// Get the acompanying text for the given status code.
		/// </summary>
		private static string getStatusCodeText(int statusCode) {
			switch (statusCode) {
				case 100: return "Continue";
				case 101: return "Switching Protocols";

				case 200: return "OK";
				case 201: return "Created";
				case 202: return "Accepted";
				case 203: return "Non-Authoritative Information";
				case 204: return "No Content";
				case 205: return "Reset Content";
				case 206: return "Partial Content";

				case 300: return "Multiple Choices";
				case 301: return "Moved Permanently";
				case 302: return "Found";
				case 303: return "See Other";
				case 304: return "Not Modified";
				case 305: return "Use Proxy";
				case 307: return "Temporary Redirect";

				case 400: return "Bad Request";
				case 401: return "Unauthorized";
				case 402: return "Payment Required";
				case 403: return "Forbidden";
				case 404: return "Not Found";
				case 405: return "Method Not Allowed";
				case 406: return "Not Acceptable";
				case 407: return "Proxy Authentication Required";
				case 408: return "Request Timeout";
				case 409: return "Conflict";
				case 410: return "Gone";
				case 411: return "Length Required";
				case 412: return "Precondition Failed";
				case 413: return "Request Entity Too Large";
				case 414: return "Request-URI Too Long";
				case 415: return "Unsupported Media Type";
				case 416: return "Requested Range Not Satisfiable";
				case 417: return "Expectation Failed";

				case 500: return "Internal Server Error";
				case 501: return "Not Implemented";
				case 502: return "Bad Gateway";
				case 503: return "Service Unavailable";
				case 504: return "Gateway Timeout";
				case 505: return "HTTP Version Not Supported";

				default:
					return "";
			}
		}

		/// <summary>
		/// The main loop for listening and handling connections.
		/// </summary>
		private static void listenLoop() {
			while (true) {
				var socket = listener.AcceptSocket();

				if (!socket.Connected)
					continue;

				var thread = new Thread(listenLoopHandler);
				thread.Start(socket);
			}
		}

		private static void listenLoopHandler(object temp) {
			var socket = temp as Socket;

			if (socket == null)
				return;

			if (settings.Verbose) {
				Console.WriteLine("Request:");
				Console.WriteLine("Client: " + socket.RemoteEndPoint);
			}

			var buffer = new byte[1024];

			socket.Receive(buffer, buffer.Length, SocketFlags.None);

			var head = Encoding.UTF8.GetString(buffer);
			var lines = head.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

			if (lines.Length < 2) {
				socket.Close();
				return;
			}

			var request = lines[0].Split(' ');

			if (request.Length != 3) {
				socket.Close();
				return;
			}

			if (settings.Verbose) {
				Console.WriteLine(lines[0]);
				Console.WriteLine();
				Console.WriteLine("Response:");
			}

			// Parse request by method.
			switch (request[0].ToUpper()) {
				case "GET":
					var path = getLocalPath(request[1]);

					if (string.IsNullOrWhiteSpace(path)) {
						sendHeaders(request[2], 404, ref socket, 0, "text/html; charset=utf-8");
						break;
					}

					if (settings.Verbose)
						Console.WriteLine("File: " + path);

					byte[] bytes;

					try {
						bytes = File.ReadAllBytes(path);
					}
					catch (Exception ex) {
						if (settings.Verbose) {
							Console.WriteLine("Could not read file: " + path);
							Console.WriteLine(ex.Message);
						}

						sendHeaders(request[2], 500, ref socket);
						break;
					}

					sendHeaders(request[2], 200, ref socket, bytes.Length, getMimeTypeFromFilename(path));

					if (bytes.Length > 0)
						sendBytes(bytes, ref socket);

					break;

				default:
					sendHeaders(request[2], 405, ref socket);
					break;
			}

			socket.Close();
		}

		/// <summary>
		/// Cycle and parse the passed arguments from command line.
		/// </summary>
		private static void parseArguments(IEnumerable<string> args) {
			var switches = args.Aggregate("", (current, a) => current + (" " + a))
				.Trim()
				.Split(new[] {"--"}, StringSplitOptions.RemoveEmptyEntries);

			foreach (var sw in switches) {
				var key = (sw.IndexOf(' ') > -1 ? sw.Substring(0, sw.IndexOf(' ')) : sw);
				var value = (sw.IndexOf(' ') > -1 ? sw.Substring(sw.IndexOf(' ') + 1) : "");

				switch (key.ToLower()) {
					case "defaultdocument":
						settings.DefaultDocument = value.Trim();
						break;

					case "defaultmimetype":
						settings.DefaultMimeType = value.Trim();
						break;

					case "path":
						settings.Path = value.Trim();
						break;

					case "port":
						int port;
						if (int.TryParse(value.Trim(), out port)) { settings.Port = port; }

						break;

					case "servername":
						settings.ServerName = value.Trim();
						break;

					case "verbose":
						settings.Verbose = true;
						break;
				}
			}
		}

		/// <summary>
		/// Send an array of bytes to the client.
		/// </summary>
		private static void sendBytes(byte[] bytes, ref Socket socket) {
			if (socket.Connected)
				socket.Send(bytes, bytes.Length, 0);
		}

		/// <summary>
		/// Construct and send headers back to the client.
		/// </summary>
		private static void sendHeaders(string http, int statusCode, ref Socket socket, int contentLength = 0, string mimeType = null) {
			var headers = new StringBuilder();
			var statusCodeText = getStatusCodeText(statusCode);

			if (string.IsNullOrWhiteSpace(mimeType))
				mimeType = settings.DefaultMimeType;

			headers.AppendLine(http + " " + statusCode + " " + statusCodeText);
			headers.AppendLine("Server: " + settings.ServerName);
			headers.AppendLine("Content-Type: " + mimeType);
			headers.AppendLine("Content-Length: " + contentLength);
			headers.AppendLine("Connection: close");
			headers.AppendLine();

			if (contentLength == 0)
				headers.AppendLine();

			if (settings.Verbose)
				Console.Write(headers.ToString());

			sendBytes(
				Encoding.UTF8.GetBytes(headers.ToString()),
				ref socket);
		}
	}

	/// <summary>
	/// A set of settings.
	/// </summary>
	public class Settings {
		/// <summary>
		/// The document to serve when requesting /.
		/// </summary>
		public string DefaultDocument { get; set; }

		/// <summary>
		/// The default mime-type to fall back to if none is found in the mime-type array.
		/// </summary>
		public string DefaultMimeType { get; set; }

		/// <summary>
		/// The path to read files from.
		/// </summary>
		public string Path { get; set; }

		/// <summary>
		/// The port to attempt to bind to.
		/// </summary>
		public int Port { get; set; }

		/// <summary>
		/// The server name to give in each request.
		/// </summary>
		public string ServerName { get; set; }

		/// <summary>
		/// Whether or not the requests are to be logged to stdout.
		/// </summary>
		public bool Verbose { get; set; }
	}
}
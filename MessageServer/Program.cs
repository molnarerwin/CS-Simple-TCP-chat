﻿using System;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.IO;
using System.Collections.Generic;

namespace MessageServer
{
    public class MessageServer
    {
        // THIS IS A SERVER AND A CLIENT AT THE SAME TIME!

        // Represents the server where the client will connect
        private TcpListener server;
        private int listeningPort;

        // Represents the client that will connect to us
        private TcpClient client;

        // For writing and reading messages
        private NetworkStream stream;

        private string name;
        private string partnerName;
        // The server sends a welcome message to the client upon connection
        private string welcomeMessage;

        // For the log filename
        private string timestamp;

        // For output and log formatting purposes
        private int lineWidth;

        // For the orange flash on the taskbar icon
        IntPtr handle;

        public MessageServer()
        {
            Console.Title = "tcpChat - Server&Client";
            handle = Process.GetCurrentProcess().MainWindowHandle;
            timestamp = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
            lineWidth = 100;
        }

        // Goes through everything until the conversation can start
        public void Run()
        {
            // These methods all handle validation by themselves
            // You can only switch their order here, which is not recommended
            Logo();

            // sets - name, listeningPort, welcomeMessage
            LoadOrSetup();

            CreateServer();

            Console.Title += "@" + name;

            server.Start();

            // Magenta
            setTextColor(1);

            if (Handshaking())
            {
                // Green
                setTextColor(2);
                MessageLoop();
            }
        }

        // Ask the user to load previous settings (settings.txt) or run through the setup process again
        public void LoadOrSetup()
        {
            // If settings.txt doesn't exists jump to Setup();
            if (!File.Exists(@"settings.txt"))
            {
                Setup();

                // After Setup(); continue Run();
                return;
            }

            Console.Write("Load last configuration? (y/n): ");
            string answer = Console.ReadLine();

            // Input handling + validation
            switch (answer)
            {
                case "":
                    LoadSetup();
                    break;
                case "y":
                    LoadSetup();
                    break;
                case "Y":
                    LoadSetup();
                    break;
                case "n":
                    Setup();
                    break;
                case "N":
                    Setup();
                    break;
                default:
                    Logo();
                    LoadOrSetup();
                    break;
            }
        }

        // Listening for connection then exchanging names and sending the welcome message + some status text
        public bool Handshaking()
        {
            // Status text
            LogAndWrite(DateTime.Now.ToString("[HH:mm:ss] ") + "Server started");
            LogAndWrite(DateTime.Now.ToString("[HH:mm:ss] ") + "Your name: " + name + " Listening Port: " + listeningPort);
            LogAndWrite(DateTime.Now.ToString("[HH:mm:ss] ") + "Waiting for connection...");

            // Waiting for client to connect
            client = server.AcceptTcpClient();

            // After successfull connection get the stream ready to read and write
            stream = client.GetStream();

            // Receiving partnerName
            partnerName = ReceiveMessage();

            // Sending our name and welcome message (successfull connection)
            SendMessage(name);
            SendMessage(welcomeMessage);

            // Status text
            LogAndWrite(DateTime.Now.ToString("[HH:mm:ss] ") + "Connection established with: " + client.Client.RemoteEndPoint + " (" + partnerName + ")");
            LogAndWrite(DateTime.Now.ToString("[HH:mm:ss] ") + "Welcome message '" + welcomeMessage + "' sent");
            LogAndWrite(DateTime.Now.ToString("[HH:mm:ss] ") + "Conversation started: " + DateTime.Now.ToString());
            LogAndWrite(DateTime.Now.ToString("[HH:mm:ss] ") + "Awaiting first response...");

            // This means the connection has been established, we can continue, see Run(); method
            return true;
        }

        // Handles when we are receiving or sending a message
        private void MessageLoop()
        {
            // false - receiving & true - sending 
            bool status = false;

            try
            {
                // This will throw an exception when connection is lost to client
                while (client.Connected)
                {
                    if (status)
                    {
                        // We are the senders now!

                        // We didn't send the timestamp to the client
                        // So we have a formattedMessageInput for e.g: [17:42:57] lanses: HI! <- we log this
                        int cursorX = Console.CursorLeft;
                        int cursorY = Console.CursorTop;

                        string formattedMessageInput = DateTime.Now.ToString("[HH:mm:ss] ") + name + ": ";

                        Console.Write(formattedMessageInput);

                        // Read method's comment
                        string message = ReadAndReturnValidMessage(Console.CursorLeft, Console.CursorTop);

                        // And an actual message we send, in this example: "HI!"
                        SendMessage(message);

                        formattedMessageInput += message;

                        // Read method's comment
                        PrettifyText(cursorX, cursorY, formattedMessageInput);

                        status = false;
                        continue;
                    }

                    // We are the receivers now!

                    // Formatting the received message
                    string receivedMessage = DateTime.Now.ToString("[HH:mm:ss] ") + partnerName + ": " + ReceiveMessage() + "\n";

                    // Output the received message with a retro terminal effect
                    typeWriter(receivedMessage);

                    // Orange flash effect on taskbar, to notify a new message has arrived
                    FlashWindow(handle, true);

                    status = true;
                }
            }
            catch (Exception)
            {
                // Connection lost to client, prepare to exit
                setTextColor(1);
                LogAndWrite(DateTime.Now.ToString("[HH:mm:ss] ") + "Lost connection to client");
                LogAndWrite(DateTime.Now.ToString("[HH:mm:ss] ") + "Press enter to exit...");
                Console.ReadLine();
            }
        }

        // Empty messages (enter) can crash/glitch the program
        private string ReadAndReturnValidMessage(int cursorLeft, int cursorTop)
        {
            string message = "";

            while (message == "")
            {
                message = Console.ReadLine();
                Console.SetCursorPosition(cursorLeft, cursorTop);
            }

            return message;
        }

        // name, listeningPort, welcomeMessage can be loaded from settings.txt
        public void LoadSetup()
        {
            // Stores the settings.txt
            List<string> settingsList = new List<string>();

            // Reading settings.txt line by line
            // 1.name, 2.listeningPort, 3.welcomeMessage
            foreach (string line in File.ReadLines(@"settings.txt"))
            {
                settingsList.Add(line);
            }

            // Parsing the listeningPort could fail here
            try
            {
                // Storing what has been read
                name = settingsList[0];
                listeningPort = int.Parse(settingsList[1]);
                welcomeMessage = settingsList[2];
            }
            catch (Exception)
            {
                Console.WriteLine("Someone modified the settings.txt incorrectly!");
                Console.WriteLine("Press enter to continue...");
                Console.ReadLine();

                Console.Clear();
                Setup();
            }
        }

        // Makes the server, server.start() is in Run(); !!
        public void CreateServer()
        {
            try
            {
                server = new TcpListener(IPAddress.Any, listeningPort);
            }
            catch (Exception)
            {
                Console.WriteLine("Server couldn't be started");
                Console.WriteLine("Press enter to continue...");
                Console.ReadLine();

                Console.Clear();
                Setup();
            }
        }

        // For setting up name, listeningPort, welcomeMessage -> these can be saved in a .txt
        public void Setup()
        {
            Logo();

            Console.WriteLine("Hi! This program works as a Server and a Client at the same time.");
            Console.Write("Your name: ");
            name = Console.ReadLine();

            // Until listeningPort is correct (at least it's an int...)
            bool ok = false;
            while (!ok)
            {
                Console.Write("Listening PORT: ");
                try
                {
                    listeningPort = int.Parse(Console.ReadLine());
                    ok = true;
                }
                catch (Exception)
                {
                    Logo();
                }
            }

            Console.Write("Set the welcome message for the client: ");
            welcomeMessage = Console.ReadLine();

            SaveSetup();

            Logo();
        }

        // name, listeningPort, welcomeMessage can be saved in settings.txt
        public void SaveSetup()
        {
            string[] lines = { name, listeningPort.ToString(), welcomeMessage };

            try
            {
                File.WriteAllLines("settings.txt", lines);
            }
            catch (Exception)
            {
                Console.WriteLine("Couldn't save settings");
                Console.WriteLine("Press enter to continue without saving...");
            }
        }

        // Sends a message via stream
        public void SendMessage(string message)
        {
            byte[] buffer = Encoding.Default.GetBytes(message);

            stream.Write(buffer, 0, buffer.Length);
        }

        // Receives a message from stream, then returns the message string
        public string ReceiveMessage()
        {
            byte[] buffer = new byte[512];

            stream.Read(buffer, 0, buffer.Length);

            string receivedMessage = Encoding.Default.GetString(buffer);

            // Buffer size is larger than the actual message
            // The rest is filled with '\0' (' '), we trim it
            receivedMessage = receivedMessage.TrimEnd('\0');

            return receivedMessage;
        }

        // Logs everything in a .txt then WriteLine
        public void LogAndWrite(string message)
        {
            string filename = "log_" + timestamp + ".txt";
            File.AppendAllText(filename, message + '\n');

            Console.WriteLine(message);
        }

        // Log without WriteLine
        public void Log(string message)
        {
            string filename = "log_" + timestamp + ".txt";
            File.AppendAllText(filename, message);
        }

        // Retro terminal effect for outputting received messages
        public void typeWriter(string outputText)
        {
            // We log the formatted string, see PrettifyText()'s comment
            string log = "";

            for (int i = 0, j = 1; i < outputText.Length; i++, j++)
            {
                Console.Write(outputText[i]);
                log += outputText[i];

                Thread.Sleep(10);

                // New line and left padding for long texts
                if (j % lineWidth == 0)
                {
                    Console.Write("\n           ");
                    log += "\n           ";
                }
            }

            Log(log);
        }

        // Console.ReadLine() can jump to the next row if our message is long enough
        // But we need padding so it looks aesthetic
        // [date] name: text bla bla
        //              bla bla bla
        // Also we log the prettified string 
        private void PrettifyText(int cursorX, int cursorY, string finalMessage)
        {
            Console.SetCursorPosition(cursorX, cursorY);

            string log = "";

            for (int i = 0, j = 1; i < finalMessage.Length; i++, j++)
            {
                Console.Write(finalMessage[i]);
                log += finalMessage[i];

                // New line and left padding for long texts
                if (j % lineWidth == 0)
                {
                    Console.Write("\n           ");
                    log += "\n           ";
                }
            }

            Log(log + '\n');

            Console.WriteLine();
        }

        public void Logo()
        {
            // Small Slant
            Console.Clear();
            LogAndWrite("   __  ___                          ____                    ");
            LogAndWrite("  /  |/  /__ ___ ___ ___ ____ ____ / __/__ _____  _____ ____");
            LogAndWrite(" / /|_/ / -_|_-<(_-</ _ `/ _ `/ -_)\\ \\/ -_) __/ |/ / -_) __/");
            LogAndWrite("/_/  /_/\\__/___/___/\\_,_/\\_, /\\__/___/\\__/_/  |___/\\__/_/   ");
            LogAndWrite("                        /___/                                by erwin");
        }

        public void setTextColor(byte color)
        {
            switch (color)
            {
                case 1:
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    break;
                case 2:
                    Console.ForegroundColor = ConsoleColor.Green;
                    break;
            }
        }

        // For the orange flash on the taskbar icon
        [DllImport("user32.dll")]
        static extern bool FlashWindow(IntPtr hwnd, bool bInvert);
    }

    internal class Program
    {
        static void Main(string[] args)
        {
            MessageServer messageServer = new MessageServer();
            messageServer.Run();
        }
    }
}

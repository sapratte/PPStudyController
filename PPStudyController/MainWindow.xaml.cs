using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using SOD_CS_Library;
using System.IO;
using System.Web.Script.Serialization;
using System.Drawing;


namespace PPStudyController
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DealWithSoD();
        }


       
        #region SoD Config

        private void DealWithSoD()
        {
            if (SoD == null)
            {
                configureSoD();
                configureDevice();
                registerSoDEvents();
                connectSoD();
                SoD.ConnectToProjector();

            }
        }

        #region SOD parameters
        static SOD_CS_Library.SOD SoD;
        // Device parameters. Set 
        // TODO: FILL THE FOLLOWING VARIABLES AND WITH POSSIBLE VALUES
        static int _deviceID = 57;                   // OPTIONAL. If it's not unique, it will be "randomly" assigned by locator.
        static string _deviceName = "Study Controller";   // You can name your device
        static string _deviceType = "CS Client";   // Cusomize device
        static bool _deviceIsStationary = true;     // If mobile device, assign false.
        static double _deviceWidthInM = 1          // Device width in metres
                        , _deviceHeightInM = 1.5   // Device height in metres
                        , _deviceLocationX = 8     // Distance in metres from the sensor which was first connected to the server
                        , _deviceLocationY = 1      // Distance in metres from the sensor which was first connected to the server
                        , _deviceLocationZ = 8      // Distance in metres from the sensor which was first connected to the server
                        , _deviceOrientation = 0    // Device orientation in Degrees, if mobile device, 0.
                        , _deviceFOV = 70;           // Device Field of View in degrees


        // observers can let device know who enters/leaves the observe area.
        static string _observerType = "rectangular";
        static double _observeHeight = 2;
        static double _observeWidth = 2;
        static double _observerDistance = 2;
        static double _observeRange = 2;
        /*
         * You can also do Radial type observer. Simply change _observerType to "radial": 
         *      static string _observerType = "radial";
         * Then observeRange will be taken as the radius of the observeRange.
        */

        // SOD connection parameters
        static string _SODAddress = "beastwin.marinhomoreira.com"; // LOCATOR URL or IP
        //static string _SODAddress = "192.168.0.144"; // Sydney's computer
        static int _SODPort = 3000; // Port of LOCATOR
        #endregion

        public static void configureSoD()
        {
            // Configure and instantiate SOD object
            string address = _SODAddress;
            int port = _SODPort;
            SoD = new SOD_CS_Library.SOD(address, port);

            // configure and connect
            configureDevice();
        }

        private static void configureDevice()
        {
            // This method takes all the parameters you specified above and set the properties accordingly in the SOD object.
            // Configure device with its dimensions (mm), location in physical space (X, Y, Z in meters, from sensor), orientation (degrees), Field Of View (FOV. degrees) and name
            SoD.ownDevice.SetDeviceInformation(_deviceWidthInM, _deviceHeightInM, _deviceLocationX, _deviceLocationY, _deviceLocationZ, _deviceType, _deviceIsStationary);
            //SoD.ownDevice.orientation = _deviceOrientation;
            SoD.ownDevice.FOV = _deviceFOV;
            if (_observerType == "rectangular")
            {
                SoD.ownDevice.observer = new SOD_CS_Library.observer(_observeWidth, _observeHeight, _observerDistance);
            }
            else if (_observerType == "radial")
            {
                SoD.ownDevice.observer = new SOD_CS_Library.observer(_observeRange);
            }

            // Name and ID of device - displayed in Locator
            SoD.ownDevice.ID = _deviceID;
            SoD.ownDevice.name = _deviceName;
        }

        /// <summary>
        /// Connect SOD to Server
        /// </summary>
        public static void connectSoD()
        {
            SoD.SocketConnect();
        }


        /// <summary>
        /// Disconnect SOD from locator.
        /// </summary>
        public static void disconnectSoD()
        {
            SoD.Close();
        }

        /// <summary>
        /// Reconnect SOD to the locator.
        /// </summary>
        public static void reconnectSoD()
        {
            SoD.ReconnectToServer();
        }


        #endregion

        #region SoD Events

        private void registerSoDEvents()
        {
            // register for 'connect' event with io server
            #region SOD Default Events
            SoD.On("connect", (data) =>
            {
                Console.WriteLine("\r\nConnected...");
                Console.WriteLine("Registering with server...\r\n");
                SoD.RegisterDevice();  //register the device with server everytime it connects or re-connects

            });

            // Sample event handler for when any device connects to server
            SoD.On("someDeviceConnected", (msgReceived) =>
            {
                Console.WriteLine("Some device connected to server: " + msgReceived.data);
            });

            

            // listener for event a person grab in the observeRange of another instance.
            SoD.On("grabInObserveRange", (msgReceived) =>
            {
                Console.WriteLine(" person " + msgReceived.data["payload"]["invader"] + " perform Grab gesture in a " + msgReceived.data["payload"]["observer"]["type"] + ": " + msgReceived.data["payload"]["observer"]["ID"]);
            });

            // listener for event a person leaves a device.
            SoD.On("leaveObserveRange", (msgReceived) =>
            {
                Dictionary<String, String> payload = new Dictionary<string, string>();
                SoD.SendToDevices.All("LeaveView", payload);
            });

            // Sample event handler for when any device disconnects from server
            SoD.On("someDeviceDisconnected", (msgReceived) =>
            {
                Console.WriteLine("Some device disconnected from server : " + msgReceived.data["name"]);
            });
            #endregion

            // Task One - Send Information Start logging
            SoD.On("SendInformationToDisplay", (msgReceived) =>
            {
                // get the round run
                CurrentRound = Convert.ToInt32(msgReceived.data["round"].ToString());

                // get random display
                CurrentDisplay = GetRandomDisplay();

                // log to file 
                string line = "Started Round - " + rounds[CurrentRound];
                Log(line);

                SendInformationToDisplay(CurrentDisplay);
            });

            SoD.On("UserResponse", (msgReceived) =>
            {
                string answer = msgReceived.data["answer"];

                Log("Answer for Round " + rounds[CurrentRound] + " - " + answer);

                SoD.SendToDevices.WithID(CurrentDisplay.Value.ID, "RemoveInformation", null);

                // if persistent round, remove the projection
                if (rounds[CurrentRound] == "persistent")
                    SoD.SODProjector.RemoveElementFromWindow(persistent.ID, persistent.owner, persistent.name);

            });

            SoD.On("NextTask", (msgReceived) =>
            {
                Log("End Task \n\n\n");

                CurrentTask++;

                this.Dispatcher.Invoke((Action)(() =>
                {
                    if (CurrentTask > 3)
                        Log("End Study for Participlant " + ParticipantID);
                }));
                
            });

            SoD.OnEnterObserverRange += SoD_OnEnterObserverRange;
            SoD.OnLeaveObserverRange += SoD_OnLeaveObserverRange;

            SoD.On("StartFindRound", (msgReceived) =>
            {
                // get the round run
                CurrentRound = Convert.ToInt32(msgReceived.data["round"].ToString());

                // log to file 
                string line = "Started Round - " + rounds[CurrentRound];
                Log(line);

                // get people
                GetPeopleFromServer((person) =>
                {
                    // draw at data location
                    if (rounds[CurrentRound] == "persistent")
                        DrawDataPoint();
                });

            });

            SoD.On("CantFindDataPoint", (msgReceived) =>
            {
                // get the answer for round
                string line = "Could not find information for Round - " + rounds[CurrentRound];
                Log(line);

                this.Dispatcher.Invoke((Action)(() =>
                {
                    task2_ready_button.IsEnabled = true;
                }));
                

                if (rounds[CurrentRound] == "persistent")
                    SoD.SODProjector.RemoveElementFromWindow(persistent.ID, persistent.owner, persistent.name);

            });

            SoD.On("StartReceiveRound", (msgReceived) =>
            {
                // get the round run
                CurrentRound = Convert.ToInt32(msgReceived.data["round"].ToString());

                // log to file 
                string line = "Started Round - " + rounds[CurrentRound];
                Log(line);

                this.Dispatcher.Invoke((Action)(() =>
                {
                    task3_send_button.IsEnabled = true;
                }));
                
            });

            SoD.On("Task3Response", (msgReceived) =>
            {
                string answer = msgReceived.data["answer"];

                Log("Answer for Round " + rounds[CurrentRound] + " - " + answer);

                this.Dispatcher.Invoke((Action)(() =>
                {
                    task3_send_button.IsEnabled = false;
                }));

                // if persistent round, remove the projection
                if (rounds[CurrentRound] == "persistent")
                    SoD.SODProjector.RemoveElementFromWindow(persistent.ID, persistent.owner, persistent.name);
            });

        }

        void SoD_OnLeaveObserverRange(object sender, SOD.ObserverVistiorEventArgs e)
        {
            if ((rounds[CurrentRound] == "persistent") || (rounds[CurrentRound] == "real-time"))
                SoD.SODProjector.RemoveElementFromWindow(persistent.ID, persistent.owner, persistent.name);

        }

        void SoD_OnEnterObserverRange(object sender, SOD.ObserverVistiorEventArgs e)
        {
            Console.WriteLine(e.observer.ID);
            if (e.observer.type == "dataPoint")
            {
                this.Dispatcher.Invoke((Action)(() =>
                {
                    task2_ready_button.IsEnabled = true;
                    if (rounds[CurrentRound] == "real-time")
                    {
                        DrawDataPoint();
                    }
                    Log("Found DataPoint for Round - " + rounds[CurrentRound]);
                    SoD.SendToDevices.WithID(ParticipantDevice.ID, "FoundDataPoint", null);
                }));
            }
        }

        



        #endregion

        #region Parameters 

        public JavaScriptSerializer js = new JavaScriptSerializer();

        string LogFilename = "";
        //string LogDirectory = @"C:\Users\ase\Desktop\PPStudyLogs\";
        string LogDirectory = @"C:\Users\sydneypratte\Development\PPStudyLogs\";
        string ParticipantID = "";
        int CurrentTask = 0;
        int CurrentRound = 0;
        int AttemptNumber = 0;
        string personID = "";

        bool demo_mode = false;

        // SoD coordinate offset
        double xoffset = 0.5;
        double yoffset = -0.7;

        List<string> rounds = new List<string>();

        Device TableTop;
        Device WallDisplay1;
        Device WallDisplay2;
        Dictionary<string, Device> Displays = new Dictionary<string, Device>();
        KeyValuePair<string, Device> CurrentDisplay;

        SOD_CS_Library.DataPoint CurrentDataPoint;

        Device ParticipantDevice;

        Person participant;

        System.Drawing.Image image;
        Projector.Element persistent;

        #endregion

        #region Logging Methods 

        private string GenerateFileName()
        {
            string filename = LogDirectory;
            if (demo_mode)
                filename += "DEMO-";
            filename += ParticipantID;
            filename += "-";
            filename += "T" + CurrentTask;
            filename += "-";
            filename += AttemptNumber;
            filename += ".txt";
            FileInfo file = new FileInfo(filename);
            if (file.Exists)
            {
                AttemptNumber++;
                return GenerateFileName();
            }
            return filename;
        }

        private void WriteToFile(string filename, string line)
        {
            using (StreamWriter writer = new StreamWriter(filename, true))
            {
                writer.WriteLine(line);
            }
        }

        private void Log(string str)
        {
            string time = DateTime.Now.ToString();
            string line = time + "\t";
            line += str;
            WriteToFile(LogFilename, line);

            this.Dispatcher.Invoke((Action)(() =>
            {
                participant_log.Text = participant_log.Text + "\n" + str;
                scrollviewer.ScrollToBottom();
            }));
        }

        #endregion

        #region Button Events 

        private void Start_Study_Click(object sender, RoutedEventArgs e)
        {
            // Load Task One
            participant_num.IsEnabled = false;
            Task1.IsEnabled = true;
            CurrentTask = 1;

            // Save the participant number and create log file.
            ParticipantID = participant_num.Text;
            LogFilename = GenerateFileName();

            // set up rounds
            rounds.Add("control");
            rounds.Add("real-time");
            rounds.Add("persistent");

            // get the current room set up and save it.
            SoD.SODProjector.room = SoD.SODProjector.GetRoom();

            // get displays info
            GetDisplayInofrmation();

            this.Dispatcher.Invoke((Action)(() =>
            {
                participant_log.Text = participant_log.Text + "\nGot room and displays.";
                scrollviewer.ScrollToBottom();
            }));
        }

        private void Setup_Click(object sender, RoutedEventArgs e)
        {
            // set up projection space.
            SetUpProjectionSpace();
        }

        private void Task1_Load_Click(object sender, RoutedEventArgs e)
        {
            CurrentTask = 1;
            LogFilename = GenerateFileName();

            SoD.SendToDevices.WithID(ParticipantDevice.ID, "loadTask1", null);
            Log("Begin Task 1  | Send Information Task");
        }

        private void Task2_Load_Click(object sender, RoutedEventArgs e)
        {
            CurrentTask = 2;
            LogFilename = GenerateFileName();

            SoD.SendToDevices.WithID(ParticipantDevice.ID, "loadTask2", null);
            Log("Begin Task 2 | Find Information Task");
        }

        private void Task2_Ready_Click(object sender, RoutedEventArgs e)
        {
            SoD.SendToDevices.WithID(ParticipantDevice.ID, "Task2Ready", null);
        }

        private void Task3_Load_Click(object sender, RoutedEventArgs e)
        {
            CurrentTask = 3;
            LogFilename = GenerateFileName();

            SoD.SendToDevices.WithID(ParticipantDevice.ID, "loadTask3", null);
            Log("Begin Task 3 | Receive Information Task");
        }

        private void Task3_Send_Click(object sender, RoutedEventArgs e)
        {
            // get random display 
            CurrentDisplay = GetRandomDisplay();

            // send from display 
            SendInformationFromDisplay();
        }

        #endregion 

        #region Projection Stuff 

        private double convert(double p, double offset)
        {
            return (p + offset);
        }

        private void SetUpProjectionSpace()
        {
            // add windows to the room for drawing
            Add_Windows();
        }

        // add a window to each surface
        private void Add_Windows()
        {
            foreach (KeyValuePair<string, Projector.Surface> s in SoD.SODProjector.room.Surfaces)
            {
                Projector.Surface surface = s.Value;
                SoD.SODProjector.AddWindow(surface.ID, 0, 0, surface.height, surface.width, surface.type + "_window");
                this.Dispatcher.Invoke((Action)(() =>
                {
                    participant_log.Text = participant_log.Text + "\nGot window with height: " + surface.height + " and width: " + surface.width;
                    scrollviewer.ScrollToBottom();
                }));
            }
        }

        private void SendInformationToDisplay(KeyValuePair<string, Device> display)
        {
            // get people
            GetPeopleFromServer((person) => 
            {
                // draw at participant location
                if (rounds[CurrentRound] == "real-time")
                {
                    DrawImageAtLocationAndMoveRealTime((participant.location.X + xoffset), (participant.location.Z + yoffset), (Convert.ToDouble(CurrentDisplay.Value.locationX) + xoffset), (Convert.ToDouble(CurrentDisplay.Value.locationZ) + yoffset));
                }
                else if (rounds[CurrentRound] == "persistent")
                {
                    // draw path
                    DrawInformationPath();
                }
                else if (rounds[CurrentRound] == "control")
                {
                    // send to display
                    SoD.SendToDevices.WithID(CurrentDisplay.Value.ID, "ShowInformation", null);

                    // log device sent to 
                    Log("Sent to display " + CurrentDisplay.Key);
                }
            });

            
        }

        private void DrawInformationPath()
        {
            foreach (KeyValuePair<string, Projector.Surface> s in SoD.SODProjector.room.Surfaces)
            {
                if (s.Value.type == "Floor")
                {
                    foreach (KeyValuePair<string, Projector.Window> window in s.Value.windows)
                    {
                        SoD.SODProjector.DrawLineOnWindow(window.Value.ID, (participant.location.Z + yoffset), (participant.location.X + xoffset), (Convert.ToDouble(CurrentDisplay.Value.locationZ) + yoffset), (Convert.ToDouble(CurrentDisplay.Value.locationX) + xoffset), "1:1:1:1", 10, "line", 
                            (elementID) => {
                            // send to display
                            if (CurrentTask == 1)
                            {
                                SoD.SendToDevices.WithID(CurrentDisplay.Value.ID, "ShowInformation", null);
                                // log device sent to 
                                Log("Sent to display " + CurrentDisplay.Key);
                            }
                            // sent from display to participant
                            else if (CurrentTask == 3)
                            {
                                SoD.SendToDevices.WithID(ParticipantDevice.ID, "ShowInformation", null);
                                // log device sent from 
                                Log("Sent from display " + CurrentDisplay.Key);
                            }

                            persistent = SoD.SODProjector.room.Elements[elementID];
                        });
                    }
                }
            }
        }

        private void RemoveImage(string elementID)
        {
            foreach (KeyValuePair<string, Projector.Element> element in SoD.SODProjector.room.Elements)
            {
                if (element.Value.ID == elementID)
                {
                    SoD.SODProjector.RemoveElementFromWindow(elementID, element.Value.owner, element.Value.name);

                    // send to display
                    if (CurrentTask == 1)
                    {
                        SoD.SendToDevices.WithID(CurrentDisplay.Value.ID, "ShowInformation", null);
                        // log device sent to 
                        Log("Sent to display " + CurrentDisplay.Key);
                    }
                    // sent from display to participant
                    else if (CurrentTask == 3)
                    {
                        SoD.SendToDevices.WithID(ParticipantDevice.ID, "ShowInformation", null);
                        // log device sent from 
                        Log("Sent from display " + CurrentDisplay.Key);
                    }
                }
            }
        }

        private void MoveImageToLocation(double? p1, double? p2)
        {
            foreach (KeyValuePair<string, Projector.Element> element in SoD.SODProjector.room.Elements)
            {
                if (element.Value.name == "info")
                {
                    SoD.SODProjector.TransferElementWithSpeed.Fast(element.Value.type, element.Value.ID, Convert.ToDouble(p1), Convert.ToDouble(p2), (data) => 
                    {
                        // remove
                        RemoveImage(data);
                    });
                }
            }
        }

        private void DrawImageAtLocationAndMoveRealTime(double p1, double p2, double p3, double p4)
        {
            foreach (KeyValuePair<string, Projector.Surface> s in SoD.SODProjector.room.Surfaces)
            {
                if (s.Value.type == "Floor")
                {
                    foreach (KeyValuePair<string, Projector.Window> window in s.Value.windows)
                    {
                        // draw a circle on the selected window
                        var path = System.IO.Path.GetFullPath("image.png");
                        System.Drawing.Image img = System.Drawing.Image.FromFile(path);
                        SoD.SODProjector.DrawImageOnWindow(window.Value.ID.ToString(), p2, p1, 0.5, 0.5, SoD.SODProjector.ImageToBase64(img, img.RawFormat), ".png", "info", 
                            (elementID) =>
                        {
                            // transfer to display location
                            MoveImageToLocation(p4, p3);
                        });
                    }
                }
            }
        }

        private void DrawDataPoint()
        {
            // get data point
            SoD.GetDataPoints((dataPoint) =>
            {
                CurrentDataPoint = dataPoint;

                // draw datapoint
                foreach (KeyValuePair<string, Projector.Surface> s in SoD.SODProjector.room.Surfaces)
                {
                    if (s.Value.type == "Floor")
                    {
                        foreach (KeyValuePair<string, Projector.Window> window in s.Value.windows)
                        {
                            // draw a image information at datapoint
                            var path = System.IO.Path.GetFullPath("image.png");
                            System.Drawing.Image img = System.Drawing.Image.FromFile(path);

                            SoD.SODProjector.DrawImageOnWindow(window.Value.ID.ToString(), (Convert.ToDouble(CurrentDataPoint.locationZ) + yoffset), (Convert.ToDouble(CurrentDataPoint.locationX) + xoffset), Convert.ToDouble(CurrentDataPoint.width), Convert.ToDouble(CurrentDataPoint.height), SoD.SODProjector.ImageToBase64(img, img.RawFormat), ".png", "info", 
                                (elementID) =>
                                {
                                    persistent = SoD.SODProjector.room.Elements[elementID];
                                });
                        }
                    }
                }
            });

        }

        private void SendInformationFromDisplay()
        {
            // get people
            GetPeopleFromServer((person) =>
            {
                // draw at participant location
                if (rounds[CurrentRound] == "real-time")
                    DrawImageAtLocationAndMoveRealTime((Convert.ToDouble(CurrentDisplay.Value.locationX) + xoffset), (Convert.ToDouble(CurrentDisplay.Value.locationZ) + yoffset), (participant.location.X + xoffset), (participant.location.Z + yoffset));
                else if (rounds[CurrentRound] == "persistent")
                {
                    // draw path
                    DrawInformationPath();
                }
                else if (rounds[CurrentRound] == "control")
                {
                    // send to display
                    SoD.SendToDevices.WithID(ParticipantDevice.ID, "ShowInformation", null);

                    // log device sent to 
                    Log("Sent from display " + CurrentDisplay.Key);
                }
            });

        }


        #endregion

        #region SOD Calls

        private void GetDisplayInofrmation()
        {
            // get the tabletop
            SoD.GetDeviceByID(21, (device) =>
            {
                TableTop = device;
                Displays.Add("TableTop", TableTop);
            });

            // get walldisplay 1
            SoD.GetDeviceByID(22, (device) =>
            {
                WallDisplay1 = device;
                Displays.Add("WallDisplay 1", WallDisplay1);
            });

            // get walldisplay 2
            SoD.GetDeviceByID(23, (device) =>
            {
                WallDisplay2 = device;
                Displays.Add("WallDisplay 2", WallDisplay2);
            });

            // get the participant
            SoD.GetDeviceByID(37, (device) =>
            {
                Console.WriteLine("Participant ID: " + device.ID);
                ParticipantDevice = device;
            });

            
        }

        private void GetPeopleFromServer(Action<Person> callback)
        {
            SoD.GetAllTrackedPeople((people) =>
            {
                if (people.Count != 0)
                {
                    // get participant location
                    for (int i = 0; i < people.Count; i++)
                    {
                        if (people[i].ID == personID)
                        {
                            participant = people[i];
                            callback(participant);
                        }
                    }
                }

            });
        }

        #endregion

        private KeyValuePair<string, Device> GetRandomDisplay()
        {
            Random rnd = new Random();
            int i = rnd.Next(0, 3);

            KeyValuePair<string, Device> display = new KeyValuePair<string,Device>();
            foreach (KeyValuePair<string, Device> d in Displays)
            {
                if (d.Key == Displays.Keys.ElementAt(i))
                    display = d;
            }

            return display;
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            disconnectSoD();
        }

        

        private void demo_button_Checked(object sender, RoutedEventArgs e)
        {
            demo_mode = true;
        }

        private void demo_button_Unchecked(object sender, RoutedEventArgs e)
        {
            demo_mode = false;
        }

        private void set_person_button_Click(object sender, RoutedEventArgs e)
        {
            personID = person_id.Text;
        }
    }
}

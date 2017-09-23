using System;
using System.Net;
using Windows.ApplicationModel.Background;
using Windows.System.Threading;
using FMdotNet__DataAPI;
using Windows.ApplicationModel.Resources;
using Windows.UI;
using System.Threading;
using GrovePi;
using GrovePi.Sensors;
using Raspberry;


// The Background Application template is documented at http://go.microsoft.com/fwlink/?LinkID=533884&clcid=0x409

namespace GrovePiToFms
{
    public sealed class StartupTask : IBackgroundTask
    {

        BackgroundTaskDeferral _deferral;

        FMS fmserver;
        DateTime tokenRecieved;
        string token;

        ILightSensor lightSensor;
        ISoundSensor soundSensor;
        ILedBar leds;

        DateTime start;
        DateTime end;

        public void Run(IBackgroundTaskInstance taskInstance)
        {
            // 
            // TODO: Insert code to perform background work
            //
            // If you start any asynchronous methods here, prevent the task
            // from closing prematurely by using BackgroundTaskDeferral as
            // described in http://aka.ms/backgroundtaskdeferral
            //

            _deferral = taskInstance.GetDeferral();

            // set the security for fms, and hook into fms
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;

            fmserver = GetFMSInstance();

            // get the sensors
            lightSensor = DeviceFactory.Build.LightSensor(Pin.AnalogPin0);
            soundSensor = DeviceFactory.Build.SoundSensor(Pin.AnalogPin2);
            leds = DeviceFactory.Build.BuildLedBar(Pin.DigitalPin8);
            leds.Initialize(GrovePi.Sensors.Orientation.GreenToRed);

            // start the timer
            ThreadPoolTimer timer = ThreadPoolTimer.CreatePeriodicTimer(Timer_Tick, TimeSpan.FromSeconds(1));
        }

        private FMS GetFMSInstance()
        {
            // hook into FMS from settings in the config file
            var resources = new ResourceLoader("config");
            var fm_server_address = resources.GetString("fm_server_address");
            var fm_file = resources.GetString("fm_file");
            var fm_layout = resources.GetString("fm_layout");
            var fm_account = resources.GetString("fm_account");
            var fm_pw = resources.GetString("fm_pw");
            var start_hour = resources.GetString("start_time");
            var end_hour = resources.GetString("end_time");

            // calculate the current day's start and end time

            FMS fms = new FMS(fm_server_address, fm_account, fm_pw);
            fms.SetFile(fm_file);
            fms.SetLayout(fm_layout);

            return fms;
        }

        private async void Timer_Tick(ThreadPoolTimer timer)
        {

            // record the start time
            DateTime start = DateTime.Now;

            // update the display
            leds.Initialize(GrovePi.Sensors.Orientation.GreenToRed);
            leds.SetLevel((byte)0);


            int lightValue = 0;
            string lightError = string.Empty;

            // read the light
            try
            {
                lightValue = lightSensor.SensorValue();
                // int between 0 (dark) and 1023 (bright) 
            }
            catch (Exception ex)
            {
                lightError = ex.Message;
            }

            int soundValue = 0;
            string soundError = String.Empty;
            try
            {
                soundValue = soundSensor.SensorValue();
            }
            catch(Exception ex)
            {
                soundError = ex.Message;
            }

            // check if token is still valid
            // get a new token every 12 minutes - no real need to since we'll usually keep the connection alive
            if ( token == null || token == string.Empty)
            {
                token = await fmserver.Authenticate();
                tokenRecieved = DateTime.Now;
            }
            else if (DateTime.Now > tokenRecieved.AddMinutes(12))
            {
                int logoutResponse = await fmserver.Logout();
                token = string.Empty;
                fmserver = GetFMSInstance();
                token = await fmserver.Authenticate();
                tokenRecieved = DateTime.Now;
            }
            if (token != string.Empty)
            {

                // get some data from the RPI itself
                var processorName = Raspberry.Board.Current.ProcessorName;
                var rpiModel = Raspberry.Board.Current.Model.ToString();

                // write it to FMS
                var request = fmserver.NewRecordRequest();

                if (processorName != null)
                    request.AddField("rpiProcessor", processorName);

                if (rpiModel != null)
                    request.AddField("rpiModel", rpiModel);

                request.AddField("when_start", start.ToString());
                request.AddField("sound", soundValue.ToString());
                request.AddField("sound_error", soundError);
                request.AddField("light", lightValue.ToString());
                request.AddField("light_error", lightError);
                request.AddField("when", DateTime.Now.ToString());


                var response = await request.Execute();
                if (response.errorCode != 0)
                {
                    leds.SetLevel((byte)10);
                    Thread.Sleep(TimeSpan.FromMilliseconds(250));

                }
                // no longer logging out after each call
                // await fmserver.Logout();
                // token = string.empty;
            }
            // clear the display again
            leds.SetLevel((byte)1);
            Thread.Sleep(TimeSpan.FromMilliseconds(250));

        }
    }
}

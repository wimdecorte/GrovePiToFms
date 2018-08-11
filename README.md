# GrovePiToFms
Sample Windows 10 IoT application to collect data from GrovePi sensors and send it to FileMaker Server 17 through the Data API.

Raspberry Pi 3 (not 3+) running Windows 10 IoT.  Project is created in Visual Studio 2017, using C#.
If you just want to see the code, go straight to this file:
GrovePiToFms/GrovePiToFms/StartupTask.cs

The code that is executed as part of the timed loop is this:
private async void Timer_Tick(ThreadPoolTimer timer)
on around line 80-85 or so.

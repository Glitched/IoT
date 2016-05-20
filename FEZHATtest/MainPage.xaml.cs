using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using GHIElectronics.UWP.Shields;
using System.Threading.Tasks;
using System.Diagnostics;
using Windows.System;

namespace RSRobot
{
    enum Motor { a, b }
    enum RobotAction
    {
        forward,
        turnleft,
        turnright,
        backward
    }

    public sealed partial class MainPage : Page
    {
        private FEZHAT hat;
        private DispatcherTimer timer;

        //Small LED Blinker
        private bool next;

        //Initialize general variables
        private RobotAction currentAction;
        private int speedmode = 0;
        private int steering_calibration = 0;
        private int debounce = 0;
        private UltrasonicDistanceSensor dist;
        private FEZHAT.Color Orange = new FEZHAT.Color(255, 60, 0);

        //Begin Stuck code
        private int secondpointer = 0;
        private double[] ax = new double[10];
        private double[] ay = new double[10];
        private double[] az = new double[10];
        private bool isStopped = true;
        private int accelDebounce = 0;
        private double accelDelta = 0;
        private int unstuck = 0;

        //Begin the wiggle
        private bool shouldWiggle = false;
        private int wiggleActivator = 0;
            
        public MainPage()
        {
            this.InitializeComponent();
            this.Setup();
        }

        private async void Setup()
        {
            this.hat = await FEZHAT.CreateAsync();
            this.dist = new UltrasonicDistanceSensor(hat);

            this.hat.S1.SetLimits(500, 2400, 0, 180);
            this.hat.S2.SetLimits(500, 2400, 0, 180);

            this.currentAction = RobotAction.forward;

            this.timer = new DispatcherTimer();
            this.timer.Interval = TimeSpan.FromMilliseconds(100);
            this.timer.Tick += this.OnTick;
            this.timer.Start();
        }

        private async void OnTick(object sender, object e)
        {
            //Read data from sensors (ultrasonic range and accelerometer)
            var distval = await dist.GetDistanceInCmAsync(50);
            this.hat.GetAcceleration(out ax[secondpointer], out ay[secondpointer], out az[secondpointer]);

            UpdateDisplay(distval);

            //Toggle small LED state
            this.next = !this.next;

            //Advance Acceleration Pointer & get current data
            secondpointer++;
            if (secondpointer >= 10) secondpointer = 0;
            accelDelta = ComputeDelta(ax) + ComputeDelta(ay) + ComputeDelta(az);

            //Set isStopped Threshold
            if (speedmode == 0) isStopped = false;
            else if (speedmode == 1 && accelDelta < 0.4) isStopped = true;
            else if (speedmode == 2 && accelDelta < 0.6) isStopped = true;
            else if (speedmode == 3 && accelDelta < 0.8) isStopped = true;
            else {isStopped = false; unstuck = 0; }
            if (isStopped) unstuck++;

            //General Functionality
            CheckButtons();
            if (accelDebounce > 0) accelDebounce--;
            PerformAction(distval);
        }

        private void PerformAction(double distval)
        {
            if (distval > 20 && currentAction == RobotAction.forward)
            {
                SetSpeed(Motor.a, speedmode * .30);
                SetSpeed(Motor.b, hat.MotorA.Speed + (speedmode * steering_calibration * .01));
            }
            if (shouldWiggle && distval > 20 && currentAction == RobotAction.forward)
            {
                SetSpeed(Motor.a, speedmode * .30 + (0.09 * speedmode * Math.Sin(secondpointer * (Math.PI / 5))));
                SetSpeed(Motor.b, speedmode * .30 + (0.09 * speedmode * Math.Sin((secondpointer * (Math.PI / 5)) + Math.PI)));
            }
            if (isStopped && currentAction == RobotAction.forward)
            {
                currentAction = RobotAction.turnleft;
                SetSpeed(Motor.a, -speedmode * .30);
                SetSpeed(Motor.b, -(hat.MotorA.Speed));
                accelDebounce = 10;
            }
            if (unstuck > 30)
            {
                currentAction = RobotAction.backward;
                SetSpeed(Motor.a, -speedmode * .30);
                SetSpeed(Motor.b, hat.MotorA.Speed);
            }
            if (unstuck > 50)
            {
                currentAction = RobotAction.forward;
                SetSpeed(Motor.a, speedmode * .30);
                SetSpeed(Motor.b, hat.MotorA.Speed + (speedmode * steering_calibration * .02));
            }
            if (unstuck > 70)
            {
                SetSpeed(Motor.a, speedmode * .30);
                SetSpeed(Motor.b, -(hat.MotorA.Speed));
                currentAction = RobotAction.turnright;
            }
            if (distval < 20 && currentAction == RobotAction.forward)
            {
                SetSpeed(Motor.a, speedmode * .30);
                SetSpeed(Motor.b, -(hat.MotorA.Speed));
                currentAction = RobotAction.turnright;
            }
            if (distval > 20 && accelDebounce == 0) currentAction = RobotAction.forward;
        }

        private void CheckButtons()
        {
            //If both buttons are pressed, shutdown
            if (this.hat.IsDIO18Pressed() && this.hat.IsDIO22Pressed())
            {
                this.hat.D2.Color = FEZHAT.Color.Black;
                this.hat.D3.Color = FEZHAT.Color.Black;
                SetSpeed(Motor.a, 0);
                SetSpeed(Motor.b, 0);
                Shutdown();
            }

            // Button 18 Manages speedmode
            if (this.hat.IsDIO18Pressed() && debounce == 0)
            {
                debounce = 3;

                //Cycle speedmode
                speedmode++;
                if (speedmode > 3) speedmode = 0;

                // Look for wiggle sequence
                if (wiggleActivator == 1) wiggleActivator++;
                else if (wiggleActivator == 2) wiggleActivator = 0;
                else if (wiggleActivator == 3) {
                    wiggleActivator = 0;
                    shouldWiggle = !shouldWiggle;
                    steering_calibration = 0;
                    speedmode = 0;
                };
            }
            else
            {
                debounce--;
                if (debounce < 0) debounce = 0;
            }

            // Button 22 manages calibration
            if (this.hat.IsDIO22Pressed() && debounce == 0)
            {
                debounce = 3;

                // Look for wiggle sequence
                if (wiggleActivator == 0) wiggleActivator++;
                else if (wiggleActivator == 1) wiggleActivator = 0;
                else if (wiggleActivator == 2) wiggleActivator++;
                else if (wiggleActivator == 3) wiggleActivator = 0;

                //Cycle thru 7 values of calibration
                if (!shouldWiggle)
                { 
                    steering_calibration++;
                    if (steering_calibration > 2) steering_calibration = -2;
                }

                //Flash both RGB LED's depending on current calibration
                if (steering_calibration == 0) this.hat.D3.Color = this.hat.D2.Color = FEZHAT.Color.Black;       
                else if (steering_calibration > 0) this.hat.D3.Color = this.hat.D2.Color = new FEZHAT.Color(0, (byte) ((255 / 3) * steering_calibration), 0);
                else if (steering_calibration < 0) this.hat.D3.Color = this.hat.D2.Color = new FEZHAT.Color((byte)((-255 / 3) * steering_calibration), 0, 0);
            }
            else
            {
                debounce--;
                if (debounce < 0) debounce = 0;
            }
        }

        private void UpdateDisplay(double distval)
        {
            double x, y, z;
            this.hat.GetAcceleration(out x, out y, out z);
            this.LightTextBox.Text = this.hat.GetLightLevel().ToString("P2");
            this.TempTextBox.Text = this.hat.GetTemperature().ToString("N2");
            this.AccelTextBox.Text = $"({x:N2}, {y:N2}, {z:N2})";
            this.Button18TextBox.Text = this.hat.IsDIO18Pressed().ToString();
            this.Button22TextBox.Text = this.hat.IsDIO22Pressed().ToString();
            this.AnalogTextBox.Text = this.hat.ReadAnalog(FEZHAT.AnalogPin.Ain1).ToString("N2");
            this.LedsTextBox.Text = ""; //String.Format("({0},{1},{2}) and ({3},{4},{5})", colors[0], colors[1], colors[2], colors[3], colors[4], colors[5]);
            this.DistanceTextBox.Text = distval.ToString();
            this.LedsTextBox.Text = this.next.ToString();

            //Debug Accel Tracker & Return Delta's
            this.PointerTextBox.Text = secondpointer.ToString();
            this.XSumTextBox.Text = ComputeDelta(ax).ToString();
            this.YSumTextBox.Text = ComputeDelta(ay).ToString();
            this.ZSumTextBox.Text = ComputeDelta(az).ToString();
            this.SumTextBox.Text = (ComputeDelta(ax) + ComputeDelta(ay) + ComputeDelta(az)).ToString();
            this.GuessTextBox.Text = isStopped.ToString();
            this.TimeTextBox.Text = unstuck.ToString();

            //Wiggle Code
            this.TrackerTextBox.Text = wiggleActivator.ToString();
            this.WiggleTextBox.Text = shouldWiggle.ToString();
            this.ASpeedTextBox.Text = this.hat.MotorA.Speed.ToString();
            this.BSpeedTextBox.Text = this.hat.MotorB.Speed.ToString();

            // Flash small red LED on tick
            this.hat.DIO24On = this.next;

            //Set RGB values to current speed & proximity
            this.hat.D2.Color = (distval < 20 ? FEZHAT.Color.Red : distval < 35 ? Orange : distval < 50 ? FEZHAT.Color.Yellow : FEZHAT.Color.Blue);
            this.hat.D3.Color = (speedmode == 0 ? FEZHAT.Color.Red : speedmode == 1 ? FEZHAT.Color.Yellow : speedmode == 2 ? FEZHAT.Color.Green : FEZHAT.Color.Magneta);

            //Set the LED's to white if the car is stuck or if it's wiggling
            if (isStopped) this.hat.D2.Color = this.hat.D3.Color = FEZHAT.Color.White;
            if (shouldWiggle) this.hat.D2.Color = FEZHAT.Color.Cyan;
        }

        private void Shutdown()
        {
            ShutdownManager.BeginShutdown(ShutdownKind.Shutdown, TimeSpan.FromSeconds(0.1));
        }

        private double ComputeDelta(double[] a)
        {
            //Sums the difference between consecutive items in an array
            double sum = 0.0;
            for (int i = 1; i < a.Length; i++) sum += Math.Abs(a[i] - a[i - 1]);
            return sum;
        }

        private void SetSpeed(Motor motor, double speed)
        {
            if (motor == Motor.a) this.hat.MotorA.Speed = (speed > 1 ? 1 : (speed < -1 ? -1 : speed));
            if (motor == Motor.b) this.hat.MotorB.Speed = (speed > 1 ? 1 : (speed < -1 ? -1 : speed));
        }
    }
}
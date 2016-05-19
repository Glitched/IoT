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

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace FEZHATtest
{
    enum RobotAction
    {
        forward,
        turnleft,
        turnright,
        backward
    }
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private FEZHAT hat;
        private DispatcherTimer timer;
        private bool next;
        private RobotAction currentAction;
        private int speedmode = 0;
        private int steering_calibration = 0;
        private int debounce = 0;
        private UltrasonicDistanceSensor dist;
        private double[] lastAccel;
        private int lastAccelPos = 0;
        public MainPage()
        {
            this.InitializeComponent();

            this.Setup();
        }

        private async void Setup()
        {
            lastAccel = new double[10];
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
        private double t = 0.0;
        private async void OnTick(object sender, object e)
        {
            var distval = await dist.GetDistanceInCmAsync(50);
            UpdateDisplay(distval);
            t += .3;
            this.next = !this.next;

            CheckButtons();

            PerformAction(distval);
        }
        // ternary( condition ? trueresult : falseresult )
        private void PerformAction(double distval)
        {
            if (distval > 20 && currentAction == RobotAction.forward)
            {
                // sinewave varriation
                var speed = speedmode * 0.4;
                var speeda = speed * -(Math.Sin(t) * 0.4) + speed;
                var speedb = (speed + (speedmode * steering_calibration * .02)) * (Math.Sin(t) * 0.4) + speed + (speedmode * steering_calibration * .02);
                hat.MotorA.Speed = (speeda<-1.0?-1.0:(speeda>1.0?1.0: speeda));
                hat.MotorB.Speed = (speedb < -1.0 ? -1.0 : speedb > 1.0 ? 1.0 : speedb);
            }
            if (distval < 20)
            {
                switch (currentAction)
                {
                    case RobotAction.forward:
                        hat.MotorA.Speed = speedmode * .40;
                        hat.MotorB.Speed = -hat.MotorA.Speed;
                        currentAction = RobotAction.turnright;
                        break;
                    case RobotAction.backward:
                        break;
                    case RobotAction.turnleft:
                        break;
                    case RobotAction.turnright:
                        break;
                    default:
                        break;
                }
            }
            if (distval > 20)
            {
                currentAction = RobotAction.forward;
            }
        }

        private void CheckButtons()
        {
            if (this.hat.IsDIO18Pressed() && this.hat.IsDIO22Pressed())
            {
                this.hat.D2.Color = FEZHAT.Color.Black;
                this.hat.D3.Color = FEZHAT.Color.Black;
                this.hat.MotorA.Speed = 0.0;
                this.hat.MotorB.Speed = 0.0;
                Shutdown();

            }
            if (this.hat.IsDIO18Pressed() && debounce == 0)
            {
                debounce = 3;
                speedmode++;
                if (speedmode > 2) speedmode = 0;

            }
            else
            {
                debounce--;
                if (debounce < 0) debounce = 0;
            }

            if (this.hat.IsDIO22Pressed() && debounce == 0)
            {
                debounce = 3;
                steering_calibration++;
                if (steering_calibration > 5) steering_calibration = -5;
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
            double totalAccel = 0.0;
            this.hat.GetAcceleration(out x, out y, out z);
            lastAccel[lastAccelPos++] = x*x + y*y + z*z;
            if (lastAccelPos == lastAccel.Length) lastAccelPos = 0;
            totalAccel = Math.Abs(lastAccel.Sum()-2.4)*100;
            this.LightTextBox.Text = this.hat.GetLightLevel().ToString("P2");
            this.TempTextBox.Text = this.hat.GetTemperature().ToString("N2");
            this.AccelTextBox.Text = totalAccel.ToString();// $"({x:N2}, {y:N2}, {z:N2})";
            this.Button18TextBox.Text = this.hat.IsDIO18Pressed().ToString();
            this.Button22TextBox.Text = this.hat.IsDIO22Pressed().ToString();
            this.AnalogTextBox.Text = this.hat.ReadAnalog(FEZHAT.AnalogPin.Ain1).ToString("N2");
            this.LedsTextBox.Text = ""; //String.Format("({0},{1},{2}) and ({3},{4},{5})", colors[0], colors[1], colors[2], colors[3], colors[4], colors[5]);
            this.DistanceTextBox.Text = distval.ToString();
            this.LedsTextBox.Text = this.next.ToString();
            this.hat.DIO24On = this.next;

            this.hat.D2.Color = (distval < 20 ? FEZHAT.Color.Red : distval < 40 ? FEZHAT.Color.Yellow : FEZHAT.Color.Blue);
            this.hat.D2.Color = new FEZHAT.Color((byte)(this.hat.D2.Color.R * totalAccel / 10),
                                    (byte)(this.hat.D2.Color.G * totalAccel/10),
                                    (byte)(this.hat.D2.Color.B * totalAccel/10));

            this.hat.D3.Color = (speedmode == 0 ? FEZHAT.Color.Red : speedmode == 1 ? FEZHAT.Color.Yellow : FEZHAT.Color.Green);
            this.hat.D3.Color = new FEZHAT.Color((byte)(this.hat.D3.Color.R * totalAccel / 10),
                                    (byte)(this.hat.D3.Color.G * totalAccel / 10),
                                    (byte)(this.hat.D3.Color.B * totalAccel / 10));
        }

        private void Shutdown()
        {
            ShutdownManager.BeginShutdown(ShutdownKind.Shutdown, TimeSpan.FromSeconds(1.0));
        }

    }
}

using GHIElectronics.UWP.Shields;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace FEZHATtest
{
    //
    // MATH:
    //   Distance is (Speed of Sound) * Time
    //   Sound is 340.3 m/s
    //   Round Trip Distance = 340.3 m/s * Seconds
    //   Distance to Object in cm = (34030 cm/s * Seconds)/2
    //   Distance to Object in cm = (34030 cm/s * ticks/(ticks per second))/2
    //   resolution  in ticks is = (resolution in cm)/34030*(ticks per second)*2
    //
    // WIRING:
    //   Trigger (white wire) on GPIO 26
    //   Echo (black wire) on GPIO 16
    //
    public class UltrasonicDistanceSensor
    {
        private readonly Stopwatch _stopwatch;
        private readonly FEZHAT _hat;
        private readonly long _tick_resolution;
        private readonly long _ticks_per_second;
        private const double PULSE_WIDTH_ms = 3.0; // sound from 1m away should be back in 3ms
        private const double SPEED_OF_SOUND_cmps = 34030.0; // cm/sec
        private const double RESOLUTION_cm = 0.5; // cm
        private const double MAX_DISTANCE_cm = 10000.0;
        private struct Edge
        {
            public const bool leading = true;
            public const bool trailing = false;
        }
        private long _ticks;
        //private bool _complete;

        public UltrasonicDistanceSensor(FEZHAT hat)
        {
            _stopwatch = new Stopwatch();
            _ticks_per_second = Stopwatch.Frequency;
            _tick_resolution = (long)( RESOLUTION_cm * _ticks_per_second / SPEED_OF_SOUND_cmps * 2.0 );
            _hat = hat;
            _hat.WriteDigital(FEZHAT.DigitalPin.DIO26, false); // clear trigger
        }
        public async Task<double> GetDistanceInCmAsync(int timeoutInMilliseconds)
        {
            double distance = MAX_DISTANCE_cm;
            _ticks=0L;
            long ticktimeout = timeoutInMilliseconds * _ticks_per_second / 1000;
            bool find_edge = Edge.leading;
            try
            {
                _stopwatch.Reset();
                await SendPulse(PULSE_WIDTH_ms);  
                _stopwatch.Start();
                while (_ticks < ticktimeout)
                {
                    if(find_edge == _hat.ReadDigital(FEZHAT.DigitalPin.DIO16))
                    {
                        if (find_edge) find_edge = Edge.trailing;
                        else break;
                    }
                    await Task.Delay(TimeSpan.FromTicks(_tick_resolution)); 
                    _ticks = _stopwatch.ElapsedTicks;
                }
            }
            finally
            {
                _stopwatch.Stop();
                distance = _ticks * SPEED_OF_SOUND_cmps / _ticks_per_second / 2.0;
            }
            return Math.Round(distance*10.0)/10.0; // round to 1 decimal place
        }

        private async Task SendPulse(double milliseconds)
        {
            _hat.WriteDigital(FEZHAT.DigitalPin.DIO26, true); // trigger
            await Task.Delay(TimeSpan.FromMilliseconds(milliseconds));
            _hat.WriteDigital(FEZHAT.DigitalPin.DIO26, false); // trigger
        }
    }
}

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using NAudio.Wave; // installed with nuget
using NAudio.CoreAudioApi;
using System.Numerics;
using System.Windows;
using System.Windows.Threading;
using System.Timers;
using System.Diagnostics;
using System.Linq;

namespace AudioServer
{
    /// <summary>
    /// Interakční logika pro MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private int RATE = 44100;
        private int BUFFERSIZE = (int)Math.Pow(2, 10);

        WasapiLoopbackCapture capture;

        public BufferedWaveProvider bwp;

        public MainWindow()
        {
            InitializeComponent();

            Closing += OnClose;

            MMDevice device = (new MMDeviceEnumerator().EnumerateAudioEndPoints(DataFlow.All, DeviceState.Active))[0];

            capture = new WasapiLoopbackCapture();

            //capture.WaveFormat = new NAudio.Wave.WaveFormat(RATE, 1);
            capture.DataAvailable += new EventHandler<WaveInEventArgs>(wi_DataAvailable);

            bwp = new BufferedWaveProvider(capture.WaveFormat);
            bwp.BufferLength = BUFFERSIZE * 2;
            bwp.DiscardOnBufferOverflow = true;

            capture.StartRecording();


            System.Timers.Timer aTimer = new System.Timers.Timer();
            aTimer.Elapsed += Update;
            aTimer.Interval = 10;
            aTimer.Enabled = true;
        }

        private void OnClose(object sender, CancelEventArgs e)
        {
            capture.StopRecording();
        }


        void wi_DataAvailable(object sender, WaveInEventArgs e)
        {
            Int32 sample_count = e.BytesRecorded / (capture.WaveFormat.BitsPerSample / 8);
            byte[] data = new byte[sample_count];

            // 8 bits. 4 bits per channel.
            for (int i = 0; i < sample_count; i++)
            {

                Single sample = BitConverter.ToSingle(e.Buffer, i * 4);

                /*
                if (sample > 0f) {
                    data[i] = (byte)(sample*256);
                }*/
                data[i] = (byte)(sample * 256);
            }
            bwp.AddSamples(data, 0, data.Length);
        }

        public void Update(object source, ElapsedEventArgs e)
        {
            // read the bytes from the stream
            int frameSize = BUFFERSIZE;
            var frames = new byte[frameSize];
            bwp.Read(frames, 0, frameSize);

            if (frames.Length == 0) return;
            if (frames[frameSize - 2] == 0) return;

            // convert it to int32 manually (and a double for scottplot)
            int SAMPLE_RESOLUTION = 16;
            int BYTES_PER_POINT = SAMPLE_RESOLUTION / 8;
            Int32[] vals = new Int32[frames.Length / BYTES_PER_POINT];
            double[] Ys = new double[frames.Length / BYTES_PER_POINT];
            double[] Xs = new double[frames.Length / BYTES_PER_POINT];
            double[] Ys2 = new double[frames.Length / BYTES_PER_POINT];
            double[] Xs2 = new double[frames.Length / BYTES_PER_POINT];
            for (int i = 0; i < vals.Length; i++)
            {
                // bit shift the byte buffer into the right variable format
                byte hByte = frames[i * 2 + 1];
                byte lByte = frames[i * 2 + 0];
                vals[i] = (int)(short)((hByte << 8) | lByte);
                Xs[i] = i;
                Ys[i] = vals[i];
                Xs2[i] = (double)i / Ys.Length * RATE / 1000.0; // units are in kHz
            }

            //update scottplot (FFT, frequency domain)
            Ys2 = FFT(Ys);

            int count = 0;
            int[] bands = { 0, 0, 0, 0, 0, 0, 0, 0 };

            // Separate bands
            for (int i = 0; i < 8; i++)
            {
                int average = 0;
                int sampleCount = (int)Math.Pow(2, i);

                for (int j = 0; j < sampleCount; j++)
                {
                    average += (int)Ys2[count] * (count + 1);
                    count++;
                }

                average /= count;
                bands[i] = average * 10;
            }
            /*
            for (int i = 0; i < 8; i++)
            {
                Debug.Write((int)bands[i] + " ");
            }*/

            int maxValue = bands.Max();
            int maxIndex = bands.ToList().IndexOf(maxValue);

            Debug.WriteLine(maxValue + " " + maxIndex);
        }

        public double[] FFT(double[] data)
        {
            double[] fft = new double[data.Length]; // this is where we will store the output (fft)
            Complex[] fftComplex = new Complex[data.Length]; // the FFT function requires complex format
            for (int i = 0; i < data.Length; i++)
            {
                fftComplex[i] = new Complex(data[i], 0.0); // make it complex format (imaginary = 0)
            }
            //Accord.Math.FourierTransform2.FFT(fftComplex, Accord.Math.FourierTransform.Direction.Forward);
            Accord.Math.Transforms.FourierTransform2.FFT(fftComplex, Accord.Math.FourierTransform.Direction.Forward);
            for (int i = 0; i < data.Length; i++)
            {
                fft[i] = fftComplex[i].Magnitude; // back to double
                //fft[i] = Math.Log10(fft[i]); // convert to dB
            }
            return fft;
            //todo: this could be much faster by reusing variables
        }
    }
}

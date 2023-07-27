using NAudio.Wave;
using MathNet.Numerics.IntegralTransforms;
using System.Numerics;

public class Program
{
    private static int fftLength = 4096; // NAudio defaults to 1024, adjust as needed
    private static Complex[] fftBuffer;
    private static object lockObject = new object();
    private static WaveInEvent waveIn;

    static void Main()
    {
        fftBuffer = new Complex[fftLength];

        waveIn = new WaveInEvent();
        waveIn.WaveFormat = new WaveFormat(44100, 1);
        waveIn.BufferMilliseconds = (int)((double)fftLength / waveIn.WaveFormat.SampleRate * 1000.0);
        waveIn.DataAvailable += OnDataAvailable;

        waveIn.StartRecording();
        Console.WriteLine("Listening... Press any key to stop");

        Console.ReadKey(true);

        waveIn.StopRecording();
    }

    private static string[] noteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

    private static string GetNoteName(float frequency)
    {
        int noteNum = (int)Math.Round(12 * (Math.Log(frequency / 440) / Math.Log(2)));
        noteNum += 49; // adjust to the standard piano keyboard (starts at A0, which is the 1st key)
        string noteName = noteNames[noteNum % 12];
        int octave = noteNum / 12;
        return noteName + octave;
    }

    private static (string NoteName, float NoteFrequency, float Difference) GetNoteAndFrequency(float frequency)
    {
        int noteNum = (int)Math.Round(12 * (Math.Log(frequency / 440) / Math.Log(2)));
        noteNum += 49; // adjust to the standard piano keyboard (starts at A0, which is the 1st key)
        string noteName = noteNames[noteNum % 12];
        int octave = noteNum / 12;
        float noteFrequency = 440 * (float)Math.Pow(2, (noteNum - 49) / 12.0);
        float difference = frequency - noteFrequency;
        return (noteName + octave, noteFrequency, difference);
    }


    private static void OnDataAvailable(object sender, WaveInEventArgs e)
    {
        lock (lockObject)
        {
            // Calculate RMS for volume
            double sum = 0;
            for (int i = 0; i < e.BytesRecorded; i += 2)
            {
                short sample = (short)((e.Buffer[i + 1] << 8) | e.Buffer[i + 0]);
                sum += sample * sample; // sum square samples
            }
            double rms = Math.Sqrt(sum / (e.BytesRecorded / 2));

            // Skip if volume is below threshold
            if (rms < 200) // Adjust threshold as needed
            {
                Console.Write("\rVolume below threshold                                                                                  ");
                return;
            }

            // Clear the fftBuffer
            Array.Clear(fftBuffer, 0, fftBuffer.Length);

            // Gather samples from data
            for (int i = 0; i < e.BytesRecorded; i += 2)
            {
                short sample = (short)((e.Buffer[i + 1] << 8) | e.Buffer[i + 0]);
                fftBuffer[i / 2] = new Complex(sample / 32768f, 0); // no imaginary component
            }

            // Hanning window function
            for (int i = 0; i < fftLength; i++)
            {
                var window = 0.5 * (1 - Math.Cos(2 * Math.PI * i / (fftLength - 1)));
                fftBuffer[i] = new Complex(fftBuffer[i].Real * window, 0);
            }

            // Apply FFT
            Fourier.Forward(fftBuffer, FourierOptions.NoScaling);

            float max = 0;
            int maxIndex = 0;

            // Find max FFT value and index
            for (int i = 0; i < fftLength / 2; i++) // only need to go halfway (nyquist)
            {
                var power = (float)Math.Sqrt((fftBuffer[i].Real * fftBuffer[i].Real) + (fftBuffer[i].Imaginary * fftBuffer[i].Imaginary));
                if (power > max)
                {
                    max = power;
                    maxIndex = i;
                }
            }

            float freq = maxIndex * (float)waveIn.WaveFormat.SampleRate / (float)fftLength;
            (string NoteName, float NoteFrequency, float Difference) = GetNoteAndFrequency(freq);
            Console.Write("\rFrequency: {0} Hz, Closest Note: {1}, Note Frequency: {2} Hz, Difference: {3} Hz          ", freq, NoteName, NoteFrequency, Difference);

        }
    }


}

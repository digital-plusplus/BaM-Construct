using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class AI_WAV : MonoBehaviour
{
    public MemoryStream stream;                       //Global variable to store the data into


    //Helper functions to convert float[] mic input into a WAV byte[] buffer
    public Stream ConvertClipToWav(AudioClip clip)
    {
        var data = new float[clip.samples * clip.channels];
        clip.GetData(data, 0);

        if (stream!=null) stream.Dispose();         //Cleanup
        stream = new MemoryStream();                //Start with a clean stream

        var bitsPerSample = (ushort)16;
        var chunkID = "RIFF";
        var format = "WAVE";
        var subChunk1ID = "fmt ";
        var subChunk1Size = (uint)16;
        var audioFormat = (ushort)1;
        var numChannels = (ushort)clip.channels;
        var sampleRate = (uint)clip.frequency;
        var byteRate = (uint)(sampleRate * clip.channels * bitsPerSample / 8);  // SampleRate * NumChannels * BitsPerSample/8
        var blockAlign = (ushort)(numChannels * bitsPerSample / 8); // NumChannels * BitsPerSample/8
        var subChunk2ID = "data";
        var subChunk2Size = (uint)(data.Length * clip.channels * bitsPerSample / 8); // NumSamples * NumChannels * BitsPerSample/8
        var chunkSize = (uint)(36 + subChunk2Size); // 36 + SubChunk2Size

        WriteString(stream, chunkID);
        WriteUInt(stream, chunkSize);
        WriteString(stream, format);
        WriteString(stream, subChunk1ID);
        WriteUInt(stream, subChunk1Size);
        WriteShort(stream, audioFormat);
        WriteShort(stream, numChannels);
        WriteUInt(stream, sampleRate);
        WriteUInt(stream, byteRate);
        WriteShort(stream, blockAlign);
        WriteShort(stream, bitsPerSample);
        WriteString(stream, subChunk2ID);
        WriteUInt(stream, subChunk2Size);

        foreach (var sample in data)
        {
            // De-normalize the samples to 16 bits.
            var deNormalizedSample = (short)0;
            if (sample > 0)
            {
                var temp = sample * short.MaxValue;
                if (temp > short.MaxValue)
                    temp = short.MaxValue;
                deNormalizedSample = (short)temp;
            }
            if (sample < 0)
            {
                var temp = sample * (-short.MinValue);
                if (temp < short.MinValue)
                    temp = short.MinValue;
                deNormalizedSample = (short)temp;
            }
            WriteShort(stream, (ushort)deNormalizedSample);
        }

        return stream;
    }


    //Helper functions to send data into the stream
    private void WriteUInt(Stream stream, uint data)
    {
        stream.WriteByte((byte)(data & 0xFF));
        stream.WriteByte((byte)((data >> 8) & 0xFF));
        stream.WriteByte((byte)((data >> 16) & 0xFF));
        stream.WriteByte((byte)((data >> 24) & 0xFF));
    }

    private void WriteShort(Stream stream, ushort data)
    {
        stream.WriteByte((byte)(data & 0xFF));
        stream.WriteByte((byte)((data >> 8) & 0xFF));
    }

    private void WriteString(Stream stream, string value)
    {
        foreach (var character in value)
            stream.WriteByte((byte)character);
    }

}

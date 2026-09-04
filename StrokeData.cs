using System.Buffers.Binary;

namespace RowSenseWindows.Models;

public sealed class StrokeData
{
    public byte AthleteNumber { get; init; }
    public byte Two { get; init; }
    public ushort Count { get; init; }
    public float[] Forces { get; init; } = new float[15];
    public ushort Tempo { get; init; }
    public ushort ReservedAfterTempo { get; init; }
    public float Power { get; init; }
    public float StrokeTime { get; init; }

    public static bool TryParse(ReadOnlySpan<byte> data, out StrokeData? stroke)
    {
        stroke = null;
        if (data.Length != 76) return false;

        byte athlete = data[0];
        byte two = data[1];
        ushort count = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(2, 2));
        ushort tempo = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(64, 2));
        ushort reserved = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(66, 2));
        float power = BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(data.Slice(68, 4)));
        float strokeTime = BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(data.Slice(72, 4)));

        if (athlete is < 1 or > 8 || two != 0 || count != 15 || reserved != 0) return false;
        if (!float.IsFinite(power) || !float.IsFinite(strokeTime)) return false;

        var forces = new float[15];
        for (int i = 0; i < 15; i++)
        {
            forces[i] = BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(data.Slice(4 + i * 4, 4)));
            if (!float.IsFinite(forces[i])) return false;
        }

        stroke = new StrokeData
        {
            AthleteNumber = athlete,
            Two = two,
            Count = count,
            Forces = forces,
            Tempo = tempo,
            ReservedAfterTempo = reserved,
            Power = power,
            StrokeTime = strokeTime
        };
        return true;
    }
}

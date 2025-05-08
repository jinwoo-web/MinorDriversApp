using System.Text;

namespace MinorDriversApp.Parser;

internal abstract class TlvParser
{
    static readonly Encoding Enc = Encoding.GetEncoding("iso-2022-jp");
    internal record Tlv(int Tag, int Length, byte[] Value);

    protected static byte[] TrimEndZeros(byte[] source)
    {
        if (source == null || source.Length == 0)
            return [];

        int lastIndex = source.Length - 1;

        while (lastIndex >= 0 && source[lastIndex] == 0x00)
            lastIndex--;

        // 모두 0이면 빈 배열 반환
        if (lastIndex < 0)
            return [];

        byte[] result = new byte[lastIndex + 1];
        Buffer.BlockCopy(source, 0, result, 0, lastIndex + 1);
        return result;
    }

    protected static string GetStringFrom0208(byte[] data)
    {
        if (data == null || data.Length == 0)
            return string.Empty;

        // 홀수 바이트 처리
        if (data.Length % 2 != 0)
        {
            byte[] adjusted = new byte[data.Length + 1];
            Array.Copy(data, adjusted, data.Length);
            data = adjusted;
        }

        try
        {
            // ISO-2022-JP 인코딩으로 변환
            byte[] iso2022jpData = new byte[data.Length + 6];
            iso2022jpData[0] = 0x1B; // ESC
            iso2022jpData[1] = 0x24; // $
            iso2022jpData[2] = 0x42; // B (JIS X 0208로 전환)

            Array.Copy(data, 0, iso2022jpData, 3, data.Length);

            iso2022jpData[data.Length + 3] = 0x1B; // ESC
            iso2022jpData[data.Length + 4] = 0x28; // (
            iso2022jpData[data.Length + 5] = 0x42; // B (ASCII로 복귀)

            return Enc.GetString(iso2022jpData).Trim('\0');
        }
        catch (Exception)
        {
            return "?";
        }
    }

    internal static List<Tlv> Parse(byte[] data, bool multiTag)
    {
        List<Tlv> tlvs = [];

        for (int i = 0; i < data.Length; i++)
        {
            int tag = multiTag ? data[i++] << 8 | data[i++] : data[i++];
            if (tag == 0xFFFF || tag == 0x00) break;

            int length = data[i];
            if (length == 0x82)
            {
                length = data[++i] << 8 | data[++i];
            }

            byte[] value = new byte[length];

            try
            {
                Array.Copy(data, i + 1, value, 0, length);
            }
            catch
            {
                break;
            }

            tlvs.Add(new Tlv(tag, length, value));
            i += length;
        }

        return tlvs;
    }
}
